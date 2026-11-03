using UnityEngine;

/// <summary>
/// Boss map: the final room holds the boss (and the exit). The rest of the rooms can still hold a range of
/// normal enemies; set minEnemy / maxEnemy to 0 for a map with only the boss.
/// Tip: raise minEndDistance so the boss room is far away from the start.
/// </summary>
public class BossMapGenerator : MapGenerationBase
{
    protected override void Reset()
    {
        gridWidth = 24; gridHeight = 12;
        horizontalBias = 0.4f;
        maxRows = 0;

        minRooms = 22; maxRooms = 40;
        chanceToSpawnRoom = 0.55f; chanceToSpawnLargeRoom = 0.3f;

        minItemDistance = 2; maxItemDistance = 9;
        minEndDistance = 12; maxEndDistance = 999;

        minEnemy = 6; maxEnemy = 12; maxEnemiesPerRoom = 4;
    }

    protected override void PopulateRooms()
    {
        base.PopulateRooms();

        Cell finalCell = GetFinalCell();
        if (finalCell == null)
        {
            Debug.LogError($"{GetType().Name}: there is no final room to place the boss in.");
            return;
        }
        finalCell.hasBoss = true;
    }
}