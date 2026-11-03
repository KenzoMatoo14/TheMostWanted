using UnityEngine;

/// <summary>
/// Normal map: a random amount of normal enemies (between minEnemy and maxEnemy, set in the Inspector)
/// is spread over the rooms. Everything else comes from <see cref="MapGenerationBase"/>.
/// </summary>
public class NormalMapGenerator : MapGenerationBase
{
    // Defaults for a new component (use the Reset option of the component to get them back)
    protected override void Reset()
    {
        gridWidth = 20; gridHeight = 12;
        horizontalBias = 0.35f;
        maxRows = 0;

        minRooms = 12; maxRooms = 24;
        chanceToSpawnRoom = 0.55f; chanceToSpawnLargeRoom = 0.3f;

        minItemDistance = 1; maxItemDistance = 6;
        minEndDistance = 6; maxEndDistance = 999;

        minEnemy = 6; maxEnemy = 12; maxEnemiesPerRoom = 4;
    }

    // PopulateRooms is inherited: it already places a random amount of normal enemies
}