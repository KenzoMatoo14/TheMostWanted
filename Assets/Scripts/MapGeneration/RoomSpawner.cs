using System.Collections.Generic;
using UnityEngine;

public enum SpawnPlacement
{
    Random,         // any valid spot
    FarFromDoors,   // the valid spot farthest from every door
    NearCenter      // the valid spot closest to the middle of the free space
}

/// <summary>One thing that can be spawned, plus the free space it needs around it.</summary>
[System.Serializable]
public class SpawnEntry
{
    public GameObject prefab;

    [Tooltip("Free tiles required around the spawn point in every direction. 0 = only its own tile, 1 = 3x3, 2 = 5x5... Use a bigger value for bigger enemies")]
    [Range(0, 10)] public int margin = 1;

    [Tooltip("Only used by the enemy lists: higher weight = picked more often")]
    [Min(0f)] public float weight = 1f;
}

[System.Serializable]
public class SpawnSettings
{
    public bool enabled = true;

    [Header("Enemies")]
    public SpawnEntry[] normalEnemies;
    public SpawnEntry[] eliteEnemies;

    [Header("Boss (rooms flagged by the boss map)")]
    public SpawnEntry boss = new SpawnEntry { margin = 3 };
    public SpawnPlacement bossPlacement = SpawnPlacement.NearCenter;

    [Header("Item (item room, only if the map's 'spawnItem' is true)")]
    public SpawnEntry item = new SpawnEntry { margin = 1 };
    public SpawnPlacement itemPlacement = SpawnPlacement.Random;

    [Header("Exit / endpoint (final room)")]
    public SpawnEntry exit = new SpawnEntry { margin = 2 };
    public SpawnPlacement exitPlacement = SpawnPlacement.FarFromDoors;
    [Tooltip("Spawn the exit disabled, so your own script (e.g. boss death) can enable it")]
    public bool exitStartsInactive = false;

    [Header("General")]
    [Tooltip("Nothing is spawned closer than this many tiles to a door (so doorways never get blocked)")]
    [Range(0, 15)] public int doorSafeRadius = 4;

    [Tooltip("Extra empty tiles kept between two spawned things, on top of their margins")]
    [Range(0, 10)] public int spacingBetweenSpawns = 1;
}

/// <summary>
/// Finds suitable places in the finished cave and spawns things there.
/// A spot is valid when every tile inside its margin is free floor (no rock, no wall, no doorway),
/// it is far enough from the doors and it doesn't overlap something that was already spawned.
/// If no spot is found the margin is reduced step by step (down to 0), then the door radius is dropped,
/// so important things (exit, boss, item, elites) are placed whenever there is any free tile at all.
/// </summary>
public static class RoomSpawner
{
    public static void Populate(GameObject roomInstance, CaveResult cave, Cell cell, SpawnSettings settings, System.Random rng)
    {
        if (settings == null || !settings.enabled || cave == null || cell == null) return;

        var map = new SpawnMap(cave, settings, rng);

        // Most important things first, so they get the best spots
        if (cell.roomType == MapGenerationBase.RoomTypes.Final)
            Spawn(map, roomInstance, settings.exit, settings.exitPlacement, !settings.exitStartsInactive);

        if (cell.hasBoss)
            Spawn(map, roomInstance, settings.boss, settings.bossPlacement, true);

        if (cell.roomType == MapGenerationBase.RoomTypes.Item && cell.hasItem)
            Spawn(map, roomInstance, settings.item, settings.itemPlacement, true);

        if (cell.eliteCount > 0 && !HasAny(settings.eliteEnemies))
            Debug.LogWarning($"RoomSpawner: '{roomInstance.name}' needs {cell.eliteCount} elite(s) but 'eliteEnemies' has no prefab assigned.");
        for (int i = 0; i < cell.eliteCount; i++)
            Spawn(map, roomInstance, PickEntry(settings.eliteEnemies, rng), SpawnPlacement.Random, true);

        if (cell.enemyCount > 0 && !HasAny(settings.normalEnemies))
            Debug.LogWarning($"RoomSpawner: '{roomInstance.name}' needs {cell.enemyCount} enemies but 'normalEnemies' has no prefab assigned.");
        for (int i = 0; i < cell.enemyCount; i++)
            Spawn(map, roomInstance, PickEntry(settings.normalEnemies, rng), SpawnPlacement.Random, true);
    }

    private static void Spawn(SpawnMap map, GameObject parent, SpawnEntry entry, SpawnPlacement placement, bool active)
    {
        if (entry == null || entry.prefab == null) return;

        if (!map.TryFindSpot(entry.margin, placement, out Vector2Int spot))
        {
            Debug.LogWarning($"RoomSpawner: no free spot for '{entry.prefab.name}' in '{parent.name}' (room too full or too small).");
            return;
        }

        GameObject spawned = Object.Instantiate(entry.prefab, map.cave.CellToWorld(spot.x, spot.y), Quaternion.identity, parent.transform);
        if (!active) spawned.SetActive(false);
    }

    private static bool HasAny(SpawnEntry[] entries)
    {
        if (entries == null) return false;
        foreach (var e in entries) if (e != null && e.prefab != null) return true;
        return false;
    }

    private static SpawnEntry PickEntry(SpawnEntry[] entries, System.Random rng)
    {
        if (!HasAny(entries)) return null;

        float total = 0f;
        foreach (var e in entries) if (e != null && e.prefab != null) total += Mathf.Max(0f, e.weight);

        SpawnEntry last = null;
        if (total <= 0f)
        {
            foreach (var e in entries) if (e != null && e.prefab != null) return e;
        }

        double roll = rng.NextDouble() * total;
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null) continue;
            last = e;
            roll -= Mathf.Max(0f, e.weight);
            if (roll <= 0.0) return e;
        }
        return last;
    }

    /// <summary>Per-room data used to search for spawn spots.</summary>
    private class SpawnMap
    {
        public readonly CaveResult cave;
        private readonly SpawnSettings settings;
        private readonly System.Random rng;
        private readonly int w, h;
        private readonly int[,] clearance;      // Chebyshev distance to the nearest blocked tile (blocked = 0)
        private readonly float[,] doorDistSq;   // squared distance (tiles) to the nearest door
        private readonly bool[,] taken;
        private readonly Vector2 centroid;

        public SpawnMap(CaveResult cave, SpawnSettings settings, System.Random rng)
        {
            this.cave = cave;
            this.settings = settings;
            this.rng = rng;
            w = cave.width;
            h = cave.height;
            taken = new bool[w, h];

            clearance = ComputeClearance();

            doorDistSq = new float[w, h];
            Vector2 sum = Vector2.zero;
            int freeCount = 0;
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float best = 1e9f;
                    foreach (var d in cave.doorCells)
                    {
                        float dx = x - d.x, dy = y - d.y;
                        best = Mathf.Min(best, dx * dx + dy * dy);
                    }
                    doorDistSq[x, y] = best;

                    if (cave.freeFloor[x, y]) { sum += new Vector2(x, y); freeCount++; }
                }
            centroid = freeCount > 0 ? sum / freeCount : new Vector2(w / 2f, h / 2f);
        }

        // Multi-source BFS from every blocked tile; the 1-tile border around the room counts as blocked
        private int[,] ComputeClearance()
        {
            int pw = w + 2, ph = h + 2;
            var dist = new int[pw, ph];
            var queue = new Queue<Vector2Int>();

            for (int px = 0; px < pw; px++)
                for (int py = 0; py < ph; py++)
                {
                    bool border = px == 0 || py == 0 || px == pw - 1 || py == ph - 1;
                    bool blocked = border || !cave.freeFloor[px - 1, py - 1];
                    dist[px, py] = blocked ? 0 : int.MaxValue;
                    if (blocked) queue.Enqueue(new Vector2Int(px, py));
                }

            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                int next = dist[c.x, c.y] + 1;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = c.x + dx, ny = c.y + dy;
                        if (nx < 0 || ny < 0 || nx >= pw || ny >= ph) continue;
                        if (dist[nx, ny] <= next) continue;
                        dist[nx, ny] = next;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
            }

            var result = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    result[x, y] = dist[x + 1, y + 1];
            return result;
        }

        public bool TryFindSpot(int margin, SpawnPlacement placement, out Vector2Int spot)
        {
            int[] doorRadii = settings.doorSafeRadius > 0 ? new[] { settings.doorSafeRadius, 0 } : new[] { 0 };

            foreach (int doorRadius in doorRadii)
            {
                for (int m = margin; m >= 0; m--)
                {
                    List<Vector2Int> candidates = Collect(m, doorRadius);
                    if (candidates.Count == 0) continue;

                    spot = Choose(candidates, placement);
                    MarkTaken(spot, m + settings.spacingBetweenSpawns);
                    return true;
                }
            }

            spot = default;
            return false;
        }

        private List<Vector2Int> Collect(int margin, int doorRadius)
        {
            var list = new List<Vector2Int>();
            float doorSq = doorRadius * doorRadius;

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!cave.freeFloor[x, y]) continue;
                    if (clearance[x, y] < margin + 1) continue;      // every tile within the margin must be free
                    if (doorDistSq[x, y] < doorSq) continue;
                    if (AnyTaken(x, y, margin)) continue;
                    list.Add(new Vector2Int(x, y));
                }
            return list;
        }

        private bool AnyTaken(int cx, int cy, int radius)
        {
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(w - 1, cx + radius); x++)
                for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(h - 1, cy + radius); y++)
                    if (taken[x, y]) return true;
            return false;
        }

        private void MarkTaken(Vector2Int c, int radius)
        {
            for (int x = Mathf.Max(0, c.x - radius); x <= Mathf.Min(w - 1, c.x + radius); x++)
                for (int y = Mathf.Max(0, c.y - radius); y <= Mathf.Min(h - 1, c.y + radius); y++)
                    taken[x, y] = true;
        }

        private Vector2Int Choose(List<Vector2Int> candidates, SpawnPlacement placement)
        {
            switch (placement)
            {
                case SpawnPlacement.FarFromDoors:
                    {
                        float best = 0f;
                        foreach (var c in candidates) best = Mathf.Max(best, doorDistSq[c.x, c.y]);
                        var top = candidates.FindAll(c => doorDistSq[c.x, c.y] >= best * 0.85f);
                        return top[rng.Next(top.Count)];
                    }
                case SpawnPlacement.NearCenter:
                    {
                        float best = float.MaxValue;
                        foreach (var c in candidates) best = Mathf.Min(best, (new Vector2(c.x, c.y) - centroid).sqrMagnitude);
                        var top = candidates.FindAll(c => (new Vector2(c.x, c.y) - centroid).sqrMagnitude <= best + 4f);
                        return top[rng.Next(top.Count)];
                    }
                default:
                    return candidates[rng.Next(candidates.Count)];
            }
        }
    }
}