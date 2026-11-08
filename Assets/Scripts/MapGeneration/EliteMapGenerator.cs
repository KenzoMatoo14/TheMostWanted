using UnityEngine;

/// <summary>
/// Elite map: a random amount of normal enemies (minEnemy..maxEnemy) plus EXACTLY 'eliteCount' elite enemies.
/// </summary>
public class EliteMapGenerator : MapGenerationBase
{
    [Header("Elite enemies")]
    [Tooltip("Exact amount of elite enemies in the whole map")]
    [SerializeField, Min(0)] private int eliteCount = 3;
    [Tooltip("Max elites in one room (ignored only if there are not enough rooms, the total is always exact)")]
    [SerializeField, Min(1)] private int maxElitesPerRoom = 1;

    // End condition: KillAllEnemies
    protected override ObjectiveType ObjectiveKind => ObjectiveType.KillAllEnemies;

    protected override void Reset()
    {
        gridWidth = 22; gridHeight = 12;
        horizontalBias = 0.4f;
        maxRows = 0;

        minRooms = 16; maxRooms = 30;
        chanceToSpawnRoom = 0.55f; chanceToSpawnLargeRoom = 0.3f;

        minItemDistance = 2; maxItemDistance = 8;
        minEndDistance = 8; maxEndDistance = 999;

        minEnemy = 6; maxEnemy = 12; maxEnemiesPerRoom = 4;

        eliteCount = 3; maxElitesPerRoom = 1;
    }

    protected override void PopulateRooms()
    {
        // Normal enemies: random amount inside the range
        base.PopulateRooms();

        // Elite enemies: exactly eliteCount
        DistributeEnemies(GetEnemyRooms(), eliteCount, maxElitesPerRoom, c => c.eliteCount++);
    }
}