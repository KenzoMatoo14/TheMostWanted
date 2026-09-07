using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// This script serves as the base for the evolutionary algorithm.
/// It includes the content representation and the evolution.
/// Fitness and asset creation will be done in the children classes.
/// </summary>
public class MapGenerationBase : MonoBehaviour
{
    public enum RoomTypes { Base, Final, Item }
    public enum RoomShape { Single, Long_2x1, Long_1x2, L_Shape_original, L_Shape_minus90, L_Shape_90, L_Shape_180, Big }

    ///Genotype of the algorithm
    protected int[] mapGeneration;

    public int[] getMapGeneration => mapGeneration;

    private int id = 0;

    protected int count;
    protected List<int> endRooms;
    protected List<int> bigRoomIndexes;

    // TODO: Create the children map generation classes for: normal, elite, and platformer map generators
    // Use <variable>.Item1 for the min value, and <variable>.Item2 for the max value.
    protected (int, int) enemyCountRange;
    protected (int, int) endRoomDistanceRange;
    protected (int, int) itemDistanceRange;
    protected (int, int) roomAmountRange;
    // Fitness Function params
    protected int finalRoomIndex;
    protected int itemRoomIndex;

    // Evo Algorithm params
    public Cell cellPrefab;
    protected float cellSize;
    protected Queue<int> cellQueue;
    protected List<Cell> spawnedCells;

    public List<Cell> getSpawnedCells => spawnedCells;

    // Assets
    // TODO: Generate the pre-generated rooms. There will be 8 types of rooms
    [Header("Sprites")]
    [SerializeField] private Sprite itemCell;
    [SerializeField] private Sprite endCell;
    [SerializeField] private Sprite smallRoom;
    [SerializeField] private Sprite bigRoom;
    [SerializeField] private Sprite largeRoom;
    [SerializeField] private Sprite lRoom;

    // Room Generation attributes
    [Header("Attributes")]
    [SerializeField] protected int minEnemy;
    [SerializeField] protected int maxEnemy;
    [SerializeField] protected int minEndDistance;
    [SerializeField] protected int maxEndDistance;
    [SerializeField] protected int minItemDistance;
    [SerializeField] protected int maxItemDistance;
    [SerializeField] protected int minRooms;
    [SerializeField] protected int maxRooms;
    [SerializeField][Range(0,1)] protected float chanceToSpawnRoom = 0.5f;
    [SerializeField][Range(0, 1)] protected float chanceToSpawnLargeRoom = 0.3f;

    public static MapGenerationBase instance;

    // Possible configurations of the rooms
    public static readonly List<int[]> roomShapes = new List<int[]>
    {
        new int[]{ -1},
        new int[] { 1},

        new int[] { 10},
        new int[] { -10},

        // Los comentarios es para saber que tanto rotar
        new int[]{ 1, 10}, // 0
        new int[] { 1, 11}, // -90
        new int[] { 10, 11}, // 90

        new int[] { 9, 10}, // 180
        new int[]{ -1, 9}, // 0
        new int[] { -1, 10}, // -90

        new int[]{ -1, -10}, // 180
        new int[] { -1, -11}, // 90
        new int[] { -10, -11}, // -90

        new int[] { 1, -10}, // 90
        new int[]{ 1, -9}, // 180
        new int[] { -9, -10}, // 0

        new int[] { 1,10, 11},
        new int[] { 1,-9,-10},
        new int[]{ -1,9,10},
        new int[] { -1,-10,-11},
    };
    void Start()
    {
        instance = this;

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
        id = 0;
        for(int i=0; i<spawnedCells.Count; i++) Destroy(spawnedCells[i].gameObject);

        spawnedCells.Clear();
        mapGeneration = new int[100];
        count = default;
        cellQueue = new Queue<int> ();
        endRooms = new List<int> ();
        bigRoomIndexes = new List<int> ();

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
            if (x > 3) visited |= VisitCell(pos - 1);
            if (x < 7) visited |= VisitCell(pos + 1);
            if (pos > 30) visited |= VisitCell(pos - 10);
            if (pos < 70) visited |= VisitCell(pos + 10);

            if(!visited) endRooms.Add(pos);
        }

        //TODO: Like in the max range check, this should be checked by the mutation algo. This is provisional to check if the generation is correct.
        if (count < roomAmountRange.Item1)
        {
            SetMap();
            return;
        }
        CleanEndRoomList();
        SetSpecialRooms();
        //string s = "";
        //int c = 0;
        //for(int i=0; i<10; i++)
        //{
        //    for(int j=0; j<10; j++)
        //    {
        //        s += mapGeneration[c].ToString() + " ";
        //        c++;
        //    }
        //    s += "\n";
        //}
        //Debug.Log(s);
    }

    private void CleanEndRoomList()
    {
        endRooms.RemoveAll(item => bigRoomIndexes.Contains(item) || GetNeighborCount(item) > 1);
    }

    // Prioritize the end of paths the agent took
    public void SetSpecialRooms()
    {
        finalRoomIndex = endRooms.Count > 0 ? endRooms[endRooms.Count - 1] : -1;

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
        RoomManager.instance.SetupRooms(spawnedCells);
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
        if (Random.value < chanceToSpawnRoom) return false;

        if (Random.value < chanceToSpawnLargeRoom)
        {
            // Utilize a random value to order so we go through the list in different order every time
            foreach(var shape in roomShapes.OrderBy(_ => Random.value))
            {
                if(TryRoom(index, shape)) return true;
            }
        }

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
        newCell.name = newCell.name + id;
        newCell.roomType = RoomTypes.Base;
        newCell.index = index;
        newCell.roomShape = RoomShape.Single;
        newCell.cellList = new List<int>{ index };
        newCell.cellList.Sort();
        spawnedCells.Add(newCell);
        id++;
    }
    /// <summary>
    /// Check for offset positions so we can place a bigger room than a 1x1
    /// </summary>
    /// <param name="origin">Current room index</param>
    /// <param name="offset">Adjacent cells</param>
    /// <returns></returns>
    private bool TryRoom(int origin, int[] offset)
    {
        List<int> currentIndexes = new List<int> { origin };

        int originX = origin % 10;
        int originY = origin / 10;

        foreach (int offsetIndex in offset)
        {
            int indexToCheck = origin + offsetIndex;

            // Make sure the index is inside the map
            if (indexToCheck < 0 || indexToCheck >= mapGeneration.Length)
                return false;

            if (mapGeneration[indexToCheck] == 1)
                return false;

            if (indexToCheck == origin)
                continue;

            currentIndexes.Add(indexToCheck);
        }

        // A room needs at least 2 cells
        if (currentIndexes.Count == 1)
            return false;

        // Reserve all cells
        foreach (int index in currentIndexes)
        {
            mapGeneration[index] = 1;
            count++;
            cellQueue.Enqueue(index);
            bigRoomIndexes.Add(index);
        }

        SpawnLargeRoom(origin, currentIndexes);

        return true;
    }


    private void SpawnLargeRoom(int origin, List<int> indexes)
    {
        // Calculate the geometric center of the occupied cells
        float originX = (float)origin % 10;
        float originY = origin / 10;

        float increaseX = originX;
        float increaseY = originY;

        foreach (int index in indexes)
        {
            float x = (float)index % 10;
            float y = index / 10;

            if(Mathf.Abs(originX - x) > 1e-6) increaseX = x;
            if (Mathf.Abs(originY - y) > 1e-6) increaseY = y;
        }

        float centerX = originX + increaseX;
        centerX /= 2.0f;
        float centerY = originY + increaseY;
        centerY /= 2.0f;


        Vector2 position = new Vector2(
            centerX * cellSize,
            -centerY * cellSize
        );

        Cell newCell = Instantiate(
            cellPrefab,
            position,
            Quaternion.identity
        );

        newCell.name = newCell.name + id;

        newCell.roomType = RoomTypes.Base;

        if (indexes.Count == 4)
        {
            // 2x2 room
            newCell.SetRoomSprite(bigRoom);
            newCell.roomShape = RoomShape.Big;
        }
        else if (indexes.Count == 3)
        {
            // L-shaped room
            newCell.SetRoomSprite(lRoom);
            newCell.roomShape = RoomShape.L_Shape_original;

            newCell.RotateRoom(origin, indexes);
        }
        else if (indexes.Count == 2)
        {
            // Long room
            newCell.SetRoomSprite(largeRoom);
            newCell.roomShape = RoomShape.Long_2x1;

            bool vertical =
                Mathf.Abs(indexes[0] - indexes[1]) == 10;

            if (vertical)
            {
                newCell.roomShape = RoomShape.Long_1x2;
                newCell.transform.rotation =
                    Quaternion.Euler(0, 0, 90);
            }
        }

        // Give the Cell a meaningful index
        newCell.index = origin;
        newCell.cellList = indexes;
        newCell.cellList.Sort();
        id++;

        spawnedCells.Add(newCell);
    }
}
