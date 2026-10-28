using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Serializable]
public class CaveSettings
{
    public bool enabled = true;

    [Tooltip("Chance for each floor tile to start as rock")]
    [Range(0f, 1f)] public float fillPercent = 0.45f;

    [Tooltip("Cellular automata passes. More passes = smoother, rounder caves")]
    [Range(0, 10)] public int smoothIterations = 5;

    [Tooltip("How many tiles inward the area in front of each open door is kept clear")]
    [Range(1, 15)] public int doorCorridorDepth = 5;

    [Tooltip("Radius (in tiles) of the guaranteed path between the open doors")]
    [Range(1, 6)] public int pathRadius = 2;

    [Tooltip("Max random offset (in tiles) of the point where the paths meet")]
    [Range(0, 10)] public int pathWobble = 4;

    [Tooltip("How much the paths can twist (0 = shortest path)")]
    [Range(0f, 5f)] public float pathTwist = 3f;

    [Header("Rock fill")]
    [Tooltip("Paint a solid color inside the rock so it doesn't look like the background")]
    public bool fillRock = true;
    public Color rockColor = new Color(0.38f, 0.32f, 0.29f, 1f);
}

/// <summary>
/// Fills the floor of a room with cave rock using cellular automata.
/// Rock is painted on the room's "Walls" tilemap, so the RuleTile joins it with the walls
/// and it gets the same collider, layer and sorting. The inside of the rock (where the RuleTile
/// shows the dark background sprite) is covered with a solid color layer.
/// Floor = cells with Background and without Walls, so it works for every room shape (including L).
/// The area in front of every open door and a path between them is always kept clear.
/// </summary>
public static class CaveGenerator
{
    private const string FillObjectName = "CaveFill";
    private static Tile fillTile;

    private enum Cell : byte { Outside, Wall, Floor, Door }

    public static void Generate(GameObject roomInstance, IReadOnlyList<GameObject> openDoors, CaveSettings settings, System.Random rng)
    {
        if (settings == null || !settings.enabled) return;

        Tilemap walls = FindTilemap(roomInstance, "Walls");
        Tilemap background = FindTilemap(roomInstance, "Background");
        if (walls == null || background == null)
        {
            Debug.LogWarning($"CaveGenerator: '{roomInstance.name}' needs 'Walls' and 'Background' tilemaps, skipping caves.");
            return;
        }

        walls.CompressBounds();
        background.CompressBounds();
        BoundsInt bounds = Union(walls.cellBounds, background.cellBounds);
        int w = bounds.size.x, h = bounds.size.y;
        if (w < 5 || h < 5) return;

        TileBase rockTile = FindAnyTile(walls, walls.cellBounds);
        if (rockTile == null) return;

        // 0) Classify every cell of the room
        Cell[,] map = new Cell[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                if (walls.HasTile(p)) map[x, y] = Cell.Wall;
                else if (background.HasTile(p)) map[x, y] = Cell.Floor;
                else map[x, y] = Cell.Outside;
            }

        bool[,] protectedCell = new bool[w, h];
        var anchors = new List<Vector2Int>();

        // 1) Door openings + a corridor in front of each open door
        foreach (GameObject doorObject in openDoors)
        {
            Tilemap doorMap = doorObject != null ? doorObject.GetComponent<Tilemap>() : null;
            if (doorMap == null) continue;

            List<Vector2Int> doorCells = GetDoorCells(doorMap, walls, bounds);
            doorCells.RemoveAll(c => !InBounds(c.x, c.y, w, h));
            if (doorCells.Count == 0) continue;

            foreach (var c in doorCells) { map[c.x, c.y] = Cell.Door; protectedCell[c.x, c.y] = true; }
            if (TryProtectCorridor(doorCells, map, protectedCell, w, h, settings.doorCorridorDepth, out Vector2Int anchor))
                anchors.Add(anchor);
        }

        if (anchors.Count == 0) return;

        // 2) Guarantee a path: every door corridor connects to a meeting point near the room center
        Vector2Int target = new Vector2Int(w / 2, h / 2);
        if (settings.pathWobble > 0)
        {
            target.x += rng.Next(-settings.pathWobble, settings.pathWobble + 1);
            target.y += rng.Next(-settings.pathWobble, settings.pathWobble + 1);
        }
        Vector2Int meeting = NearestFloor(map, target, w, h, settings.pathRadius);

        float[,] cost = new float[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                cost[x, y] = 1f + (float)rng.NextDouble() * settings.pathTwist;

        foreach (Vector2Int anchor in anchors)
        {
            List<Vector2Int> path = FindPath(map, cost, anchor, meeting, w, h);
            foreach (var p in path) ProtectCircle(protectedCell, map, p, settings.pathRadius, w, h);
        }

        // 3) Random fill + cellular automata smoothing on the floor
        bool[,] rock = new bool[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                rock[x, y] = map[x, y] == Cell.Floor && !protectedCell[x, y] && rng.NextDouble() < settings.fillPercent;

        for (int i = 0; i < settings.smoothIterations; i++)
            rock = Smooth(rock, map, protectedCell, w, h);

        // 4) Paint the rock on the Walls tilemap in one batch
        var positions = new List<Vector3Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (rock[x, y]) positions.Add(new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0));

        var tiles = new TileBase[positions.Count];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = rockTile;
        walls.SetTiles(positions.ToArray(), tiles);

        // 5) Solid color inside the rock
        if (settings.fillRock) PaintFill(walls, background, bounds, w, h, settings.rockColor);
    }

    private static bool[,] Smooth(bool[,] rock, Cell[,] map, bool[,] protectedCell, int w, int h)
    {
        bool[,] next = new bool[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] != Cell.Floor || protectedCell[x, y]) continue;

                int n = 0;
                for (int i = x - 1; i <= x + 1; i++)
                    for (int j = y - 1; j <= y + 1; j++)
                    {
                        if (i == x && j == y) continue;
                        if (IsSolid(rock, map, i, j, w, h)) n++;
                    }

                if (n > 4) next[x, y] = true;
                else if (n < 4) next[x, y] = false;
                else next[x, y] = rock[x, y];
            }
        }
        return next;
    }

    // Walls and outside the room count as rock; open doors count as empty
    private static bool IsSolid(bool[,] rock, Cell[,] map, int x, int y, int w, int h)
    {
        if (!InBounds(x, y, w, h)) return true;
        switch (map[x, y])
        {
            case Cell.Floor: return rock[x, y];
            case Cell.Door: return false;
            default: return true;
        }
    }

    /// <summary>
    /// Marks a corridor going into the room from the door. The inward side is the one with floor,
    /// so it also works for inner doors (L rooms). Returns the inner end of the corridor.
    /// </summary>
    private static bool TryProtectCorridor(List<Vector2Int> doorCells, Cell[,] map, bool[,] protectedCell, int w, int h, int depth, out Vector2Int anchor)
    {
        int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
        foreach (var c in doorCells)
        {
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }
        Vector2Int mid = new Vector2Int((minX + maxX) / 2, (minY + maxY) / 2);
        bool horizontal = (maxX - minX) >= (maxY - minY);   // door line along x -> corridor goes up/down

        Vector2Int a = horizontal ? Vector2Int.up : Vector2Int.right;
        Vector2Int inward = IsFloor(map, mid + a, w, h) ? a : (IsFloor(map, mid - a, w, h) ? -a : Vector2Int.zero);
        anchor = mid;
        if (inward == Vector2Int.zero) return false;

        for (int step = 1; step <= depth; step++)
        {
            if (horizontal)
                for (int x = minX - 1; x <= maxX + 1; x++) ProtectFloor(protectedCell, map, x, minY + inward.y * step, w, h);
            else
                for (int y = minY - 1; y <= maxY + 1; y++) ProtectFloor(protectedCell, map, minX + inward.x * step, y, w, h);
        }

        // Walk back if the corridor end isn't floor (e.g. a very short room)
        for (int step = depth; step >= 1; step--)
        {
            Vector2Int p = mid + inward * step;
            if (IsFloor(map, p, w, h)) { anchor = p; return true; }
        }
        return false;
    }

    /// <summary>Cheapest path over floor/door cells (Dijkstra with random costs so it twists a bit).</summary>
    private static List<Vector2Int> FindPath(Cell[,] map, float[,] cost, Vector2Int from, Vector2Int to, int w, int h)
    {
        var dist = new float[w, h];
        var prev = new Vector2Int[w, h];
        for (int x = 0; x < w; x++) for (int y = 0; y < h; y++) dist[x, y] = float.MaxValue;

        var open = new SortedSet<(float, int, int)>();
        dist[from.x, from.y] = 0;
        open.Add((0f, from.x, from.y));
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (open.Count > 0)
        {
            var (d, cx, cy) = open.Min;
            open.Remove(open.Min);
            if (cx == to.x && cy == to.y) break;
            if (d > dist[cx, cy]) continue;

            foreach (var dir in dirs)
            {
                int nx = cx + dir.x, ny = cy + dir.y;
                if (!InBounds(nx, ny, w, h)) continue;
                if (map[nx, ny] != Cell.Floor && map[nx, ny] != Cell.Door) continue;
                float nd = d + cost[nx, ny];
                if (nd < dist[nx, ny])
                {
                    if (dist[nx, ny] != float.MaxValue) open.Remove((dist[nx, ny], nx, ny));
                    dist[nx, ny] = nd;
                    prev[nx, ny] = new Vector2Int(cx, cy);
                    open.Add((nd, nx, ny));
                }
            }
        }

        var path = new List<Vector2Int>();
        if (dist[to.x, to.y] == float.MaxValue) return path;
        for (Vector2Int p = to; p != from; p = prev[p.x, p.y]) path.Add(p);
        path.Add(from);
        return path;
    }

    private static Vector2Int NearestFloor(Cell[,] map, Vector2Int target, int w, int h, int margin)
    {
        Vector2Int best = target;
        int bestDist = int.MaxValue;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] != Cell.Floor) continue;
                // prefer cells with room around them for the path radius
                if (!IsFloor(map, new Vector2Int(x - margin, y), w, h) || !IsFloor(map, new Vector2Int(x + margin, y), w, h) ||
                    !IsFloor(map, new Vector2Int(x, y - margin), w, h) || !IsFloor(map, new Vector2Int(x, y + margin), w, h)) continue;
                int d = (x - target.x) * (x - target.x) + (y - target.y) * (y - target.y);
                if (d < bestDist) { bestDist = d; best = new Vector2Int(x, y); }
            }
        return best;
    }

    private static void PaintFill(Tilemap walls, Tilemap background, BoundsInt bounds, int w, int h, Color color)
    {
        // The RuleTile draws the inside of the rock (cells with no matching edge rule) with its
        // default sprite, which is the same dark sprite as the background. Cover exactly those cells.
        Sprite backgroundSprite = FindAnySprite(background, background.cellBounds);
        if (backgroundSprite == null) return;

        var positions = new List<Vector3Int>();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                if (walls.HasTile(p) && walls.GetSprite(p) == backgroundSprite) positions.Add(p);
            }
        if (positions.Count == 0) return;

        // Separate tilemap drawn on top of the walls (only on the inside cells, so the outlines stay visible)
        var fillObject = new GameObject(FillObjectName);
        fillObject.layer = walls.gameObject.layer;
        fillObject.transform.SetParent(walls.transform.parent, false);
        fillObject.transform.localPosition = walls.transform.localPosition;

        var fillMap = fillObject.AddComponent<Tilemap>();
        fillMap.tileAnchor = walls.tileAnchor;
        fillMap.color = color;
        var fillRenderer = fillObject.AddComponent<TilemapRenderer>();

        var wallRenderer = walls.GetComponent<TilemapRenderer>();
        if (wallRenderer != null)
        {
            fillRenderer.sortingLayerID = wallRenderer.sortingLayerID;
            fillRenderer.sharedMaterial = wallRenderer.sharedMaterial;
            fillRenderer.sortingOrder = wallRenderer.sortingOrder + 1;
        }

        var tiles = new TileBase[positions.Count];
        Tile tile = GetFillTile();
        for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
        fillMap.SetTiles(positions.ToArray(), tiles);
    }

    private static Tile GetFillTile()
    {
        if (fillTile != null) return fillTile;
        var texture = new Texture2D(4, 4) { filterMode = FilterMode.Point, name = "CaveFillTexture" };
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        fillTile = ScriptableObject.CreateInstance<Tile>();
        fillTile.name = "CaveFillTile";
        fillTile.sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        fillTile.colliderType = Tile.ColliderType.None;
        return fillTile;
    }

    private static void ProtectCircle(bool[,] protectedCell, Cell[,] map, Vector2Int c, int radius, int w, int h)
    {
        for (int x = c.x - radius; x <= c.x + radius; x++)
            for (int y = c.y - radius; y <= c.y + radius; y++)
                if ((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= radius * radius)
                    ProtectFloor(protectedCell, map, x, y, w, h);
    }

    private static void ProtectFloor(bool[,] protectedCell, Cell[,] map, int x, int y, int w, int h)
    {
        if (InBounds(x, y, w, h) && map[x, y] == Cell.Floor) protectedCell[x, y] = true;
    }

    private static bool IsFloor(Cell[,] map, Vector2Int p, int w, int h) => InBounds(p.x, p.y, w, h) && map[p.x, p.y] == Cell.Floor;

    private static bool InBounds(int x, int y, int w, int h) => x >= 0 && y >= 0 && x < w && y < h;

    /// <summary>Door tiles converted to local cell coordinates of the walls tilemap.</summary>
    private static List<Vector2Int> GetDoorCells(Tilemap doorMap, Tilemap walls, BoundsInt bounds)
    {
        var cells = new List<Vector2Int>();
        doorMap.CompressBounds();
        foreach (Vector3Int p in doorMap.cellBounds.allPositionsWithin)
        {
            if (!doorMap.HasTile(p)) continue;
            Vector3Int c = walls.WorldToCell(doorMap.GetCellCenterWorld(p));
            cells.Add(new Vector2Int(c.x - bounds.xMin, c.y - bounds.yMin));
        }
        return cells;
    }

    private static BoundsInt Union(BoundsInt a, BoundsInt b)
    {
        Vector3Int min = Vector3Int.Min(a.min, b.min);
        Vector3Int max = Vector3Int.Max(a.max, b.max);
        return new BoundsInt(min, max - min);
    }

    private static Tilemap FindTilemap(GameObject root, string name)
    {
        foreach (Tilemap t in root.GetComponentsInChildren<Tilemap>(true))
            if (t.gameObject.name == name) return t;
        return null;
    }

    private static Sprite FindAnySprite(Tilemap map, BoundsInt bounds)
    {
        foreach (Vector3Int p in bounds.allPositionsWithin)
        {
            Sprite s = map.GetSprite(p);
            if (s != null) return s;
        }
        return null;
    }

    private static TileBase FindAnyTile(Tilemap map, BoundsInt bounds)
    {
        foreach (Vector3Int p in bounds.allPositionsWithin)
        {
            TileBase t = map.GetTile(p);
            if (t != null) return t;
        }
        return null;
    }
}
