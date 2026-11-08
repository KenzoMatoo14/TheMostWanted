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

    [Header("Rock tile")]
    [Tooltip("Tile painted as rock. Leave empty to detect it from the room's Walls tilemap (prefers a RuleTile, then the most used tile). Assign your wall RuleTile here if a room shows wrong sprites")]
    public TileBase rockTile;

    [Header("Less rock / no trapped gaps")]
    [Tooltip("Max share of the floor that can end up as rock (0.35 = 35%). The extra rock is peeled away from the edges that face open space, thin spikes first. 1 = no limit")]
    [Range(0f, 1f)] public float maxRockCoverage = 0.35f;

    [Tooltip("Free-floating rock islands (not touching any wall) smaller than this many tiles are removed. 0 = keep them all")]
    [Range(0, 30)] public int minRockIslandSize = 6;

    [Tooltip("Empty pockets completely enclosed by rock (unreachable from the doors) are filled with rock, so no blank spaces stay trapped inside the noise")]
    public bool fillTrappedGaps = true;

    [Header("Rock fill")]
    [Tooltip("Paint a solid color inside the rock so it doesn't look like the background")]
    public bool fillRock = true;
    public Color rockColor = new Color(0.38f, 0.32f, 0.29f, 1f);
}

/// <summary>What the cave generator leaves behind, so other systems (spawning) can read the final layout of the room.</summary>
public class CaveResult
{
    public Tilemap walls;
    public BoundsInt bounds;
    public int width, height;

    /// <summary>true = walkable floor with no rock on it (doorways are not included).</summary>
    public bool[,] freeFloor;

    /// <summary>Door tiles in local coordinates.</summary>
    public List<Vector2Int> doorCells = new List<Vector2Int>();

    public Vector3 CellToWorld(int x, int y)
    {
        return walls.GetCellCenterWorld(new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0));
    }
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

    private enum CellKind : byte { Outside, Wall, Floor, Door }

    public static CaveResult Generate(GameObject roomInstance, IReadOnlyList<GameObject> openDoors, CaveSettings settings, System.Random rng)
    {
        Tilemap walls = FindTilemap(roomInstance, "Walls");
        Tilemap background = FindTilemap(roomInstance, "Background");
        if (walls == null || background == null)
        {
            Debug.LogWarning($"CaveGenerator: '{roomInstance.name}' needs 'Walls' and 'Background' tilemaps, skipping caves.");
            return null;
        }

        walls.CompressBounds();
        background.CompressBounds();
        BoundsInt bounds = Union(walls.cellBounds, background.cellBounds);
        int w = bounds.size.x, h = bounds.size.y;
        if (w < 5 || h < 5) return null;

        bool carve = settings != null && settings.enabled;
        TileBase rockTile = ChooseRockTile(walls, settings);

        // 0) Classify every cell of the room
        CellKind[,] map = new CellKind[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var p = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                if (walls.HasTile(p)) map[x, y] = CellKind.Wall;
                else if (background.HasTile(p)) map[x, y] = CellKind.Floor;
                else map[x, y] = CellKind.Outside;
            }

        bool[,] protectedCell = new bool[w, h];
        var anchors = new List<Vector2Int>();
        var result = new CaveResult { walls = walls, bounds = bounds, width = w, height = h, freeFloor = new bool[w, h] };

        // 1) Door openings + a corridor in front of each open door
        foreach (GameObject doorObject in openDoors)
        {
            Tilemap doorMap = doorObject != null ? doorObject.GetComponent<Tilemap>() : null;
            if (doorMap == null) continue;

            List<Vector2Int> doorCells = GetDoorCells(doorMap, walls, bounds);
            doorCells.RemoveAll(c => !InBounds(c.x, c.y, w, h));
            if (doorCells.Count == 0) continue;

            foreach (var c in doorCells) { map[c.x, c.y] = CellKind.Door; protectedCell[c.x, c.y] = true; }
            result.doorCells.AddRange(doorCells);

            if (carve && TryProtectCorridor(doorCells, map, protectedCell, w, h, settings.doorCorridorDepth, out Vector2Int anchor))
                anchors.Add(anchor);
        }

        bool[,] rock = new bool[w, h];

        if (carve && anchors.Count > 0 && rockTile != null)
        {
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
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    rock[x, y] = map[x, y] == CellKind.Floor && !protectedCell[x, y] && rng.NextDouble() < settings.fillPercent;

            for (int i = 0; i < settings.smoothIterations; i++)
                rock = Smooth(rock, map, protectedCell, w, h);

            // 3b) Fill every blank pocket that is trapped inside the rock (unreachable from the doors)
            if (settings.fillTrappedGaps) FillTrappedGaps(rock, map, result.doorCells, w, h);

            // 3c) Make the noise occupy less space
            if (settings.maxRockCoverage < 1f) ErodeRock(rock, map, settings.maxRockCoverage, w, h, rng);
            if (settings.minRockIslandSize > 1) RemoveRockIslands(rock, map, settings.minRockIslandSize, w, h);

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

        // Free floor = floor that is not rock (door tiles are excluded on purpose, so nothing spawns in a doorway)
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                result.freeFloor[x, y] = map[x, y] == CellKind.Floor && !rock[x, y];

        return result;
    }

    private static readonly Vector2Int[] Dirs4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    /// <summary>
    /// Flood fills the open space starting at the door tiles. Any open floor that was not reached is a pocket
    /// trapped inside the rock, so it becomes rock too.
    /// </summary>
    private static void FillTrappedGaps(bool[,] rock, CellKind[,] map, List<Vector2Int> seeds, int w, int h)
    {
        var reached = new bool[w, h];
        var queue = new Queue<Vector2Int>();
        foreach (var s in seeds)
        {
            if (!InBounds(s.x, s.y, w, h) || reached[s.x, s.y]) continue;
            reached[s.x, s.y] = true;
            queue.Enqueue(s);
        }

        int reachedFloor = 0;
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            foreach (var d in Dirs4)
            {
                int nx = c.x + d.x, ny = c.y + d.y;
                if (!InBounds(nx, ny, w, h) || reached[nx, ny]) continue;
                bool passable = map[nx, ny] == CellKind.Door || (map[nx, ny] == CellKind.Floor && !rock[nx, ny]);
                if (!passable) continue;
                reached[nx, ny] = true;
                if (map[nx, ny] == CellKind.Floor) reachedFloor++;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        // Safety: if nothing was reached something is wrong with the doors, don't fill the whole room
        if (reachedFloor == 0) return;

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (map[x, y] == CellKind.Floor && !rock[x, y] && !reached[x, y]) rock[x, y] = true;
    }

    /// <summary>
    /// Peels rock until it covers at most maxCoverage of the floor. Only rock that touches open space is removed,
    /// and the cells with the fewest rock neighbours go first (spikes), so the cave stays smooth and the
    /// rock attached to the walls is the last to go. Never opens new trapped pockets.
    /// </summary>
    private static void ErodeRock(bool[,] rock, CellKind[,] map, float maxCoverage, int w, int h, System.Random rng)
    {
        int floorCount = 0, rockCount = 0;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] == CellKind.Floor) floorCount++;
                if (rock[x, y]) rockCount++;
            }

        int target = Mathf.FloorToInt(maxCoverage * floorCount);
        var boundary = new List<(int x, int y, float key)>();

        for (int pass = 0; pass < 200 && rockCount > target; pass++)
        {
            boundary.Clear();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!rock[x, y]) continue;

                    bool touchesOpen = false;
                    foreach (var d in Dirs4)
                        if (!IsSolid(rock, map, x + d.x, y + d.y, w, h)) { touchesOpen = true; break; }
                    if (!touchesOpen) continue;

                    int n = 0;
                    for (int i = x - 1; i <= x + 1; i++)
                        for (int j = y - 1; j <= y + 1; j++)
                            if ((i != x || j != y) && IsSolid(rock, map, i, j, w, h)) n++;

                    boundary.Add((x, y, n + (float)rng.NextDouble()));
                }

            if (boundary.Count == 0) break;
            boundary.Sort((a, b) => a.key.CompareTo(b.key));

            int remove = Mathf.Min(boundary.Count, Mathf.Max(1, (rockCount - target + 1) / 2));
            for (int i = 0; i < remove; i++)
            {
                rock[boundary[i].x, boundary[i].y] = false;
                rockCount--;
            }
        }
    }

    /// <summary>Removes small rock islands that float in the open (rock that does not touch a wall).</summary>
    private static void RemoveRockIslands(bool[,] rock, CellKind[,] map, int minSize, int w, int h)
    {
        var visited = new bool[w, h];
        var stack = new Stack<Vector2Int>();
        var island = new List<Vector2Int>();

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (!rock[x, y] || visited[x, y]) continue;

                island.Clear();
                bool touchesWall = false;
                visited[x, y] = true;
                stack.Push(new Vector2Int(x, y));

                while (stack.Count > 0)
                {
                    Vector2Int c = stack.Pop();
                    island.Add(c);
                    foreach (var d in Dirs4)
                    {
                        int nx = c.x + d.x, ny = c.y + d.y;
                        if (!InBounds(nx, ny, w, h) || map[nx, ny] == CellKind.Wall || map[nx, ny] == CellKind.Outside)
                        {
                            touchesWall = true;
                            continue;
                        }
                        if (rock[nx, ny] && !visited[nx, ny])
                        {
                            visited[nx, ny] = true;
                            stack.Push(new Vector2Int(nx, ny));
                        }
                    }
                }

                if (!touchesWall && island.Count < minSize)
                    foreach (var c in island) rock[c.x, c.y] = false;
            }
    }

    private static bool[,] Smooth(bool[,] rock, CellKind[,] map, bool[,] protectedCell, int w, int h)
    {
        bool[,] next = new bool[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] != CellKind.Floor || protectedCell[x, y]) continue;

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
    private static bool IsSolid(bool[,] rock, CellKind[,] map, int x, int y, int w, int h)
    {
        if (!InBounds(x, y, w, h)) return true;
        switch (map[x, y])
        {
            case CellKind.Floor: return rock[x, y];
            case CellKind.Door: return false;
            default: return true;
        }
    }

    /// <summary>
    /// Marks a corridor going into the room from the door. The inward side is the one with floor,
    /// so it also works for inner doors (L rooms). Returns the inner end of the corridor.
    /// </summary>
    private static bool TryProtectCorridor(List<Vector2Int> doorCells, CellKind[,] map, bool[,] protectedCell, int w, int h, int depth, out Vector2Int anchor)
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
    private static List<Vector2Int> FindPath(CellKind[,] map, float[,] cost, Vector2Int from, Vector2Int to, int w, int h)
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
                if (map[nx, ny] != CellKind.Floor && map[nx, ny] != CellKind.Door) continue;
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

    private static Vector2Int NearestFloor(CellKind[,] map, Vector2Int target, int w, int h, int margin)
    {
        Vector2Int best = target;
        int bestDist = int.MaxValue;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (map[x, y] != CellKind.Floor) continue;
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

    private static void ProtectCircle(bool[,] protectedCell, CellKind[,] map, Vector2Int c, int radius, int w, int h)
    {
        for (int x = c.x - radius; x <= c.x + radius; x++)
            for (int y = c.y - radius; y <= c.y + radius; y++)
                if ((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= radius * radius)
                    ProtectFloor(protectedCell, map, x, y, w, h);
    }

    private static void ProtectFloor(bool[,] protectedCell, CellKind[,] map, int x, int y, int w, int h)
    {
        if (InBounds(x, y, w, h) && map[x, y] == CellKind.Floor) protectedCell[x, y] = true;
    }

    private static bool IsFloor(CellKind[,] map, Vector2Int p, int w, int h) => InBounds(p.x, p.y, w, h) && map[p.x, p.y] == CellKind.Floor;

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

    /// <summary>
    /// The tile used for the rock. The first tile of the tilemap can't be used: it is usually a wall corner, and a plain
    /// corner tile painted everywhere ignores the RuleTile rules. So: the override from the settings, else a RuleTile
    /// found in the Walls tilemap, else the most used tile.
    /// </summary>
    private static TileBase ChooseRockTile(Tilemap walls, CaveSettings settings)
    {
        if (settings != null && settings.rockTile != null) return settings.rockTile;

        var counts = new Dictionary<TileBase, int>();
        foreach (Vector3Int p in walls.cellBounds.allPositionsWithin)
        {
            TileBase tile = walls.GetTile(p);
            if (tile == null) continue;
            counts.TryGetValue(tile, out int n);
            counts[tile] = n + 1;
        }

        TileBase best = null;
        bool bestIsRule = false;
        int bestCount = 0;
        foreach (var pair in counts)
        {
            bool isRule = pair.Key.GetType().Name.Contains("RuleTile");
            if (best == null || (isRule && !bestIsRule) || (isRule == bestIsRule && pair.Value > bestCount))
            {
                best = pair.Key;
                bestIsRule = isRule;
                bestCount = pair.Value;
            }
        }
        return best;
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