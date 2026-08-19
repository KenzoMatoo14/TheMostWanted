using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This script serves as the base for the evolutionary algorithm.
/// It includes the content representation and the evolution.
/// Fitness and asset creation will be done in the children classes.
/// </summary>
public class MapGenerationBase : MonoBehaviour
{
    public enum RoomTypes { Base, Final, Item }

    ///Genotype of the algorithm
    private int[] mapGeneration;
    private int count;
    private List<int> endRooms;

    // TODO: Create the children map generation classes for: normal, elite, and platformer map generators
    // Use <variable>.Item1 for the min value, and <variable>.Item2 for the max value.
    private (int, int) enemyCountRange;
    private (int, int) endRoomDistanceRange;
    private (int, int) itemDistanceRange;
    private (int, int) roomAmountRange;
    // Fitness Function params
    protected int finalRoomIndex;
    protected int itemRoomIndex;

    // Evo Algorithm params
    public Cell cellPrefab;
    private float cellSize;
    private Queue<int> cellQueue;
    private List<Cell> spawnedCells;

    // Assets
    [Header("Sprites")]
    [SerializeField] private Sprite itemCell;
    [SerializeField] private Sprite endCell;

    [Header("Attributes")]
    [SerializeField] protected int minEnemy;
    [SerializeField] protected int maxEnemy;
    [SerializeField] protected int minEndDistance;
    [SerializeField] protected int maxEndDistance;
    [SerializeField] protected int minItemDistance;
    [SerializeField] protected int maxItemDistance;
    [SerializeField] protected int minRooms;
    [SerializeField] protected int maxRooms;

    void Start()
    {
        spawnedCells = new List<Cell>();
        cellSize = 1;
        enemyCountRange = (minEnemy, maxEnemy);
        endRoomDistanceRange = (minEndDistance, maxEndDistance);
        itemDistanceRange = (minItemDistance, maxItemDistance);
        roomAmountRange = (minRooms,maxRooms);

        SetMap();
    }

    /// <summary>
    /// Initialization of the map
    /// </summary>
    public void SetMap()
    {
        for(int i=0; i<spawnedCells.Count; i++) Destroy(spawnedCells[i]);

        spawnedCells.Clear();
        mapGeneration = new int[100];
        count = default;
        cellQueue = new Queue<int> ();
        endRooms = new List<int> ();

        VisitCell(45);
        GenerateMap();
    }

    public void GenerateMap()
    {
        while (cellQueue.Count > 0)
        {
            int pos = cellQueue.Dequeue();
            int x = pos % 10;

            bool visited = false;
            // |= is an operator that will ignore the value of the right, if the value of the 'visited' variable is true already.
            // It is an OR operator.
            if (x > 1) visited |= VisitCell(pos - 1);
            if (x < 8) visited |= VisitCell(pos + 1);
            if (pos > 20) visited |= VisitCell(pos - 10);
            if (pos < 80) visited |= VisitCell(pos + 10);

            if(!visited) endRooms.Add(pos);
        }

        //TODO: Like in the max range check, this should be checked by the mutation algo. This is provisional to check if the generation is correct.
        if (count < roomAmountRange.Item1)
        {
            SetMap();
            return;
        }
        SetSpecialRooms();
    }

    public void SetSpecialRooms()
    {
        finalRoomIndex = endRooms.Count > 0 ? endRooms[^1] : -1;

        if(finalRoomIndex != -1)
        {
            endRooms.RemoveAt(endRooms.Count - 1);
        }

        itemRoomIndex = RandomEndRoom();

        if (finalRoomIndex == -1 || itemRoomIndex == -1)
        {
            SetMap();
            return;
        }
        SetSpecialRoomsVisuals();
    }

    public void SetSpecialRoomsVisuals()
    {
        foreach(var cell in spawnedCells)
        {
            if (cell.index == finalRoomIndex)
            {
                cell.SetSpecialRoomSprite(endCell);
                cell.roomType = RoomTypes.Final;
            }
            if (cell.index == itemRoomIndex)
            {
                cell.SetSpecialRoomSprite(itemCell);
                cell.roomType = RoomTypes.Item;
            }
        }
    }

    public int RandomEndRoom()
    {
        if(endRooms.Count  == 0) return -1;

        int randomRoom = Random.Range(0, endRooms.Count);
        int index = endRooms[randomRoom];

        endRooms.RemoveAt(randomRoom);

        return index;
    }

    private int GetNeighborCount(int index)
    {
        return mapGeneration[index - 10] + mapGeneration[index-1] + mapGeneration[index + 1] + mapGeneration[index + 10];
    }

    /// <summary>
    /// Check if the cell is valid and assign it for the map.
    /// </summary>
    /// <param name="index">Cell index, two digits, the tens for the Ys and the ones for the Xs</param>
    /// <returns></returns>
    private bool VisitCell(int index)
    {
        if (mapGeneration[index] != 0) return false;
        if (GetNeighborCount(index) > 1) return false;
        // TODO: See if the algorithm will work without this limit, the mutation should set the room amount in the room range.
        if (count >= roomAmountRange.Item2) return false;
        if (Random.value < 0.5f) return false;

        cellQueue.Enqueue(index);
        count++;
        mapGeneration[index] = 1;

        SpawnRoom(index);

        return true;
    }

    /// <summary>
    /// Instantiate the cell at the correct position and initialize the cell object
    /// </summary>
    /// <param name="index"></param>
    private void SpawnRoom(int index)
    {
        int x = index % 10;
        int y = index / 10;

        Vector2 position = new Vector2(x * cellSize, -y * cellSize);
        
        Cell newCell = Instantiate(cellPrefab, position, Quaternion.identity);
        newCell.roomType = RoomTypes.Base;
        newCell.index = index;

        spawnedCells.Add(newCell);
    }


}
