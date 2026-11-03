using UnityEngine;

/// <summary>
/// Platformer map: a wide map that grows mostly left to right, only a few rows tall, built from rooms that are fun
/// to travel across (single rooms and long horizontal rooms by default).
/// The map starts near the left edge, so the final room (the farthest dead end) ends up on the right side.
/// </summary>
public class PlatformerMapGenerator : MapGenerationBase
{
    [Header("Platformer layout")]
    [Tooltip("Column where the map starts (1 = leftmost usable column)")]
    [SerializeField, Min(1)] private int startColumn = 1;
    [Tooltip("The map must be at least this many columns wide")]
    [SerializeField, Min(1)] private int minHorizontalSpan = 14;

    [Header("Allowed room shapes (single rooms and horizontal 2x1 rooms are always allowed)")]
    [SerializeField] private bool allowVerticalRooms = false;
    [SerializeField] private bool allowLShapes = false;
    [SerializeField] private bool allowBigRooms = false;

    protected override void Reset()
    {
        gridWidth = 32; gridHeight = 9;
        horizontalBias = 0.6f;
        maxRows = 3;

        minRooms = 20; maxRooms = 40;
        chanceToSpawnRoom = 0.6f; chanceToSpawnLargeRoom = 0.5f;

        minItemDistance = 3; maxItemDistance = 12;
        minEndDistance = 16; maxEndDistance = 999;

        minEnemy = 8; maxEnemy = 16; maxEnemiesPerRoom = 3;

        startColumn = 1; minHorizontalSpan = 14;
        allowVerticalRooms = false; allowLShapes = false; allowBigRooms = false;
    }

    protected override int GetStartIndex()
    {
        int x = Mathf.Clamp(startColumn, 1, GridWidth - 2);
        return ((GridHeight - 1) / 2) * GridWidth + x;
    }

    protected override bool IsShapeAllowed(Vector2Int[] offsets)
    {
        int cells = offsets.Length + 1;

        if (cells == 2)
        {
            bool vertical = offsets[0].x == 0;
            return !vertical || allowVerticalRooms;
        }
        if (cells == 3) return allowLShapes;
        return allowBigRooms;   // 2x2
    }

    protected override bool ValidateMap()
    {
        int minX = int.MaxValue, maxX = int.MinValue;
        for (int i = 0; i < mapGeneration.Length; i++)
        {
            if (mapGeneration[i] != 1) continue;
            int x = i % GridWidth;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }
        return maxX - minX + 1 >= minHorizontalSpan;
    }
}