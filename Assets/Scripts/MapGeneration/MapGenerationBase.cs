using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Base of the map generators (Normal, Elite, Boss, Platformer).
/// It owns the grid, the room growth, the distance rules for the item / final rooms and the room objects.
/// Children decide what goes inside the rooms (<see cref="PopulateRooms"/>), which room shapes are allowed
/// (<see cref="IsShapeAllowed"/>), where the map starts (<see cref="GetStartIndex"/>) and any extra check (<see cref="ValidateMap"/>).
///
/// Grid index = y * GridWidth + x. Rooms can only use the inner area of the grid (the outer ring stays empty),
/// so neighbor checks (index +-1, +-GridWidth) never leave the grid.
/// </summary>
public abstract class MapGenerationBase : MonoBehaviour
{
    public enum RoomTypes { Base, Final, Item }
    public enum RoomShape { Single, Long_2x1, Long_1x2, L_Shape_original, L_Shape_minus90, L_Shape_90, L_Shape_180, Big }

    // Static so Cell, Door and RoomManager use the same grid size without needing a reference
    public static int GridWidth { get; private set; } = 10;
    public static int GridHeight { get; private set; } = 10;

    ///Genotype of the algorithm
    protected int[] mapGeneration;
    public int[] getMapGeneration => mapGeneration;

    private int id = 0;

    protected int count;
    protected List<int> endRooms;
    protected List<int> bigRoomIndexes;

    // Use <variable>.Item1 for the min value, and <variable>.Item2 for the max value.
    protected (int, int) enemyCountRange;
    protected (int, int) endRoomDistanceRange;
    protected (int, int) itemDistanceRange;
    protected (int, int) roomAmountRange;

    // Fitness Function params
    protected int startIndex;
    protected int finalRoomIndex;
    protected int itemRoomIndex;

    /// <summary>Distance (in grid cells, walking through connected rooms) from the start to every cell. -1 = not reachable.</summary>
    protected int[] distanceFromStart;
    public int[] getDistanceFromStart => distanceFromStart;

    // Evo Algorithm params
    public Cell cellPrefab;
    protected float cellSize;
    protected Queue<int> cellQueue;
    protected List<Cell> spawnedCells;

    public List<Cell> getSpawnedCells => spawnedCells;

    // GameObjects Structure
    [Header("Attachment GameObjects")]
    public GameObject overlayParent;

    [Header("UI Layout")]
    [SerializeField] private float uiCellSize = 40f;                // pixels per grid cell
    [SerializeField] private Vector2 uiMargin = new Vector2(50, 50); // distance from the screen corner

    // Assets
    [Header("Sprites")]
    [SerializeField] private Sprite itemCell;
    [SerializeField] private Sprite endCell;
    [SerializeField] private Sprite smallRoom;
    [SerializeField] private Sprite bigRoom;
    [SerializeField] private Sprite largeRoom;
    [SerializeField] private Sprite lRoom;

    // ------------------------------------------------------------------ Parameters (editable in the Inspector)

    [Header("Grid")]
    [Tooltip("Grid width in cells. The outer ring of cells is never used")]
    [SerializeField, Min(5)] protected int gridWidth = 20;
    [Tooltip("Grid height in cells. The outer ring of cells is never used")]
    [SerializeField, Min(5)] protected int gridHeight = 12;
    [Tooltip("0 = grows the same in every direction. 1 = rooms almost only grow left/right. Raises the chance of horizontal neighbors and lowers the vertical ones")]
    [SerializeField, Range(0, 1)] protected float horizontalBias = 0.35f;
    [Tooltip("Max number of grid rows the map can use, centered on the start. 0 = no limit")]
    [SerializeField, Min(0)] protected int maxRows = 0;

    [Header("Room amount (counted in grid cells)")]
    [SerializeField] protected int minRooms = 12;
    [SerializeField] protected int maxRooms = 24;
    [Tooltip("Chance that a neighbor cell becomes a room (before the horizontal bias)")]
    [SerializeField, Range(0, 1)] protected float chanceToSpawnRoom = 0.55f;
    [Tooltip("Chance that a new room tries to be a bigger shape (2x1, L, 2x2)")]
    [SerializeField, Range(0, 1)] protected float chanceToSpawnLargeRoom = 0.3f;

    [Header("Distances from the start room (in cells)")]
    [Tooltip("The item room must be at least this far from the start")]
    [SerializeField] protected int minItemDistance = 1;
    [Tooltip("The item room must be at most this far from the start")]
    [SerializeField] protected int maxItemDistance = 6;
    [Tooltip("The final room must be at least this far from the start")]
    [SerializeField] protected int minEndDistance = 6;
    [Tooltip("The final room must be at most this far from the start")]
    [SerializeField] protected int maxEndDistance = 999;

    [Header("Item")]
    [Tooltip("The item room always exists, this decides whether the item is actually spawned in it")]
    public bool spawnItem = true;

    [Header("Enemies")]
    [SerializeField] protected int minEnemy = 6;
    [SerializeField] protected int maxEnemy = 12;
    [Tooltip("Max normal enemies in a single room (the limit is ignored if there are not enough rooms)")]
    [SerializeField, Min(1)] protected int maxEnemiesPerRoom = 6;

    public static MapGenerationBase instance;

    // Shapes of the big rooms as (dx, dy) offsets from the origin cell. The comment is how much the L shapes are rotated.
    private static readonly Vector2Int[][] shapeDeltas =
    {
        D(-1, 0),
        D(1, 0),
        D(0, 1),
        D(0, -1),

        D(1, 0, 0, 1),     // 0
        D(1, 0, 1, 1),     // -90
        D(0, 1, 1, 1),     // 90

        D(-1, 1, 0, 1),    // 180
        D(-1, 0, -1, 1),   // 0
        D(-1, 0, 0, 1),    // -90

        D(-1, 0, 0, -1),   // 180
        D(-1, 0, -1, -1),  // 90
        D(0, -1, -1, -1),  // -90

        D(1, 0, 0, -1),    // 90
        D(1, 0, 1, -1),    // 180
        D(1, -1, 0, -1),   // 0

        D(1, 0, 0, 1, 1, 1),
        D(1, 0, 1, -1, 0, -1),
        D(-1, 0, -1, 1, 0, 1),
        D(-1, 0, 0, -1, -1, -1),
    };

    private static Vector2Int[] D(params int[] xy)
    {
        var result = new Vector2Int[xy.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = new Vector2Int(xy[i * 2], xy[i * 2 + 1]);
        return result;
    }

    // The shapes allowed for the current map, converted to index offsets for the current grid width
    private List<int[]> shapeOffsets;

    private class PendingRoom
    {
        public int origin;
        public List<int> indexes;
    }
    // Rooms are only collected while generating. Cell objects are created once the map is valid.
    private List<PendingRoom> pendingRooms;

    private int minRow, maxRow;

    // ------------------------------------------------------------------ Hooks for the children

    /// <summary>Called once the map is valid and the cells exist. Assign enemies / boss / item to the cells here.</summary>
    protected virtual void PopulateRooms()
    {
        AssignNormalEnemies();
    }

    /// <summary>Extra validation of a candidate map. Return false to throw it away and generate another one.</summary>
    protected virtual bool ValidateMap() { return true; }

    /// <summary>Lets a child restrict the big room shapes. Offsets are (dx, dy) from the origin cell, y grows downwards.</summary>
    protected virtual bool IsShapeAllowed(Vector2Int[] offsets) { return true; }

    /// <summary>Grid index of the first room. Must be inside the inner area of the grid.</summary>
    protected virtual int GetStartIndex()
    {
        return ((GridHeight - 1) / 2) * GridWidth + GridWidth / 2;
    }

    /// <summary>Which of the end rooms becomes the final room. Default: the farthest one from the start.</summary>
    protected virtual int PickFinalRoom(List<int> candidates)
    {
        return candidates.OrderByDescending(i => distanceFromStart[i]).ThenBy(_ => Random.value).First();
    }

    /// <summary>Children set their own default values here (only runs when the component is added or reset in the editor).</summary>
    protected virtual void Reset() { }

    protected virtual void OnValidate()
    {
        maxRooms = Mathf.Max(minRooms, maxRooms);
        maxEnemy = Mathf.Max(minEnemy, maxEnemy);
        maxItemDistance = Mathf.Max(minItemDistance, maxItemDistance);
        maxEndDistance = Mathf.Max(minEndDistance, maxEndDistance);
    }

    // ------------------------------------------------------------------ Flow

    protected virtual void Awake()
    {
        instance = this;
    }

    protected virtual void Start()
    {
        instance = this;
        if (overlayParent == null) overlayParent = GameObject.Find("Overlay");

        spawnedCells = new List<Cell>();
        cellSize = 1;

        SetMap();
    }

    // Safety limit so a bad configuration (e.g. minRooms too high) can't freeze the game
    private const int MaxGenerationAttempts = 500;

    private void RefreshParameters()
    {
        GridWidth = Mathf.Max(5, gridWidth);
        GridHeight = Mathf.Max(5, gridHeight);

        enemyCountRange = (minEnemy, Mathf.Max(minEnemy, maxEnemy));
        endRoomDistanceRange = (minEndDistance, Mathf.Max(minEndDistance, maxEndDistance));
        itemDistanceRange = (minItemDistance, Mathf.Max(minItemDistance, maxItemDistance));
        roomAmountRange = (minRooms, Mathf.Max(minRooms, maxRooms));

        shapeOffsets = new List<int[]>();
        foreach (Vector2Int[] shape in shapeDeltas)
        {
            if (!IsShapeAllowed(shape)) continue;
            shapeOffsets.Add(shape.Select(v => v.y * GridWidth + v.x).ToArray());
        }
    }

    /// <summary>
    /// Initialization of the map. Retries in a loop until a valid map is generated.
    /// </summary>
    public void SetMap()
    {
        if (spawnedCells == null) spawnedCells = new List<Cell>();
        RefreshParameters();

        for (int attempt = 0; attempt < MaxGenerationAttempts; attempt++)
        {
            if (TryGenerateMap())
            {
                BuildCells();
                PopulateRooms();
                RoomManager.instance.SetupRooms(spawnedCells);
                SetParent();

                Debug.Log($"{GetType().Name}: map ready after {attempt + 1} attempt(s) - {count} cells, " +
                          $"item room at distance {distanceFromStart[itemRoomIndex]}, final room at distance {distanceFromStart[finalRoomIndex]}.");
                return;
            }
        }

        Debug.LogError($"{GetType().Name}: could not generate a valid map after {MaxGenerationAttempts} attempts. " +
                       "Check the room amount, the distances, the grid size and chanceToSpawnRoom.");
    }

    private void ResetMap()
    {
        id = 0;
        for (int i = 0; i < spawnedCells.Count; i++)
            if (spawnedCells[i] != null) Destroy(spawnedCells[i].gameObject);

        spawnedCells.Clear();
        mapGeneration = new int[GridWidth * GridHeight];
        count = default;
        cellQueue = new Queue<int>();
        endRooms = new List<int>();
        bigRoomIndexes = new List<int>();
        pendingRooms = new List<PendingRoom>();

        startIndex = GetStartIndex();

        // Rows the map is allowed to use (always inside the inner area)
        minRow = 1;
        maxRow = GridHeight - 2;
        if (maxRows > 0)
        {
            int startY = startIndex / GridWidth;
            minRow = Mathf.Clamp(startY - (maxRows - 1) / 2, 1, GridHeight - 2);
            maxRow = Mathf.Min(GridHeight - 2, minRow + maxRows - 1);
        }
    }

    private bool InPlayableArea(int x, int y)
    {
        return x >= 1 && x <= GridWidth - 2 && y >= minRow && y <= maxRow;
    }

    private bool InPlayableArea(int index)
    {
        if (index < 0 || index >= mapGeneration.Length) return false;
        return InPlayableArea(index % GridWidth, index / GridWidth);
    }

    /// <summary>
    /// Generates one candidate map (data only, no objects). Returns false if it doesn't meet the requirements.
    /// </summary>
    private bool TryGenerateMap()
    {
        ResetMap();

        if (!InPlayableArea(startIndex))
        {
            Debug.LogError($"{GetType().Name}: the start index {startIndex} is outside the usable area of the grid.");
            return false;
        }
        PlaceStartCell();

        // The horizontal bias makes left/right neighbors more likely and up/down neighbors less likely
        float horizontalChance = Mathf.Clamp01(chanceToSpawnRoom * (1f + horizontalBias));
        float verticalChance = Mathf.Clamp01(chanceToSpawnRoom * (1f - horizontalBias));

        while (cellQueue.Count > 0)
        {
            int pos = cellQueue.Dequeue();

            bool visited = false;
            // |= is an operator that will ignore the value of the right, if the value of the 'visited' variable is true already.
            // It is an OR operator, but unlike || it always evaluates both sides, so every neighbor is tried.
            visited |= VisitCell(pos - 1, horizontalChance);
            visited |= VisitCell(pos + 1, horizontalChance);
            visited |= VisitCell(pos - GridWidth, verticalChance);
            visited |= VisitCell(pos + GridWidth, verticalChance);

            if (!visited) endRooms.Add(pos);
        }

        if (count < roomAmountRange.Item1) return false;

        CleanEndRoomList();
        if (!SetSpecialRooms()) return false;
        return ValidateMap();
    }

    private void CleanEndRoomList()
    {
        endRooms.RemoveAll(item => item == startIndex || bigRoomIndexes.Contains(item) || GetNeighborCount(item) > 1);
    }

    private static bool InRange(int value, (int, int) range)
    {
        return value >= 0 && value >= range.Item1 && value <= range.Item2;
    }

    /// <summary>
    /// Walks through the connected cells from 'from' and returns the number of steps to every cell (-1 = unreachable).
    /// Doors open between any two neighbor cells, so a step is just a move to an adjacent room cell.
    /// </summary>
    protected int[] ComputeDistances(int from)
    {
        int[] dist = new int[mapGeneration.Length];
        for (int i = 0; i < dist.Length; i++) dist[i] = -1;

        int[] steps = { -1, 1, -GridWidth, GridWidth };
        var queue = new Queue<int>();
        dist[from] = 0;
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (int step in steps)
            {
                int next = current + step;
                if (next < 0 || next >= dist.Length) continue;
                if (mapGeneration[next] != 1 || dist[next] != -1) continue;
                dist[next] = dist[current] + 1;
                queue.Enqueue(next);
            }
        }
        return dist;
    }

    /// <summary>
    /// Picks the final and the item room among the dead ends, respecting the distance ranges from the start.
    /// Returns false if no dead end fits, so the map is generated again.
    /// </summary>
    protected virtual bool SetSpecialRooms()
    {
        distanceFromStart = ComputeDistances(startIndex);

        List<int> finalCandidates = endRooms.Where(i => InRange(distanceFromStart[i], endRoomDistanceRange)).ToList();
        if (finalCandidates.Count == 0) return false;

        finalRoomIndex = PickFinalRoom(finalCandidates);
        endRooms.Remove(finalRoomIndex);

        List<int> itemCandidates = endRooms.Where(i => InRange(distanceFromStart[i], itemDistanceRange)).ToList();
        if (itemCandidates.Count == 0) return false;

        itemRoomIndex = itemCandidates[Random.Range(0, itemCandidates.Count)];
        endRooms.Remove(itemRoomIndex);
        return true;
    }

    /// <summary>Creates the Cell objects of the valid map.</summary>
    private void BuildCells()
    {
        foreach (PendingRoom room in pendingRooms)
        {
            if (room.indexes.Count == 1) SpawnRoom(room.origin);
            else SpawnLargeRoom(room.origin, room.indexes);
        }

        SetSpecialRoomsVisuals();

        foreach (Cell cell in spawnedCells)
            cell.isStartRoom = cell.cellList.Contains(startIndex);
    }

    public void SetSpecialRoomsVisuals()
    {
        foreach (var cell in spawnedCells)
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
                cell.hasItem = spawnItem;
            }
        }
    }

    // ------------------------------------------------------------------ Enemy helpers for the children

    /// <summary>Rooms where enemies can be placed: normal rooms, never the start room.</summary>
    protected List<Cell> GetEnemyRooms()
    {
        return spawnedCells.Where(c => c.roomType == RoomTypes.Base && !c.isStartRoom).ToList();
    }

    protected Cell GetFinalCell()
    {
        return spawnedCells.FirstOrDefault(c => c.roomType == RoomTypes.Final);
    }

    /// <summary>Random amount of normal enemies (inside the enemy range) spread over the enemy rooms.</summary>
    protected void AssignNormalEnemies()
    {
        int total = Random.Range(enemyCountRange.Item1, enemyCountRange.Item2 + 1);
        DistributeEnemies(GetEnemyRooms(), total, maxEnemiesPerRoom, c => c.enemyCount++);
    }

    /// <summary>
    /// Hands out exactly 'total' units among the rooms. Bigger rooms are more likely to get them.
    /// maxPerRoom is a soft limit: if it can't be respected, the extra units still get placed so the total is exact.
    /// </summary>
    protected void DistributeEnemies(List<Cell> pool, int total, int maxPerRoom, System.Action<Cell> addOne)
    {
        if (total <= 0) return;
        if (pool == null || pool.Count == 0)
        {
            Debug.LogWarning($"{GetType().Name}: there are no rooms to place {total} enemies in.");
            return;
        }

        var counts = new Dictionary<Cell, int>();
        foreach (Cell c in pool) counts[c] = 0;

        bool warned = false;
        for (int n = 0; n < total; n++)
        {
            List<Cell> candidates = pool.Where(c => counts[c] < maxPerRoom).ToList();
            if (candidates.Count == 0)
            {
                if (!warned)
                {
                    Debug.LogWarning($"{GetType().Name}: not enough rooms for the per-room limit ({maxPerRoom}), ignoring it for the rest.");
                    warned = true;
                }
                candidates = pool;
            }

            Cell chosen = PickWeighted(candidates);
            counts[chosen]++;
            addOne(chosen);
        }
    }

    private static Cell PickWeighted(List<Cell> cells)
    {
        float totalWeight = 0f;
        foreach (Cell c in cells) totalWeight += c.cellList.Count;

        float roll = Random.value * totalWeight;
        foreach (Cell c in cells)
        {
            roll -= c.cellList.Count;
            if (roll <= 0f) return c;
        }
        return cells[cells.Count - 1];
    }

    // ------------------------------------------------------------------ UI

    private void SetParent()
    {
        float minX = float.MaxValue;
        float maxY = float.MinValue;
        foreach (Cell cell in spawnedCells)
        {
            Vector3 p = cell.transform.position;
            minX = Mathf.Min(minX, p.x);
            maxY = Mathf.Max(maxY, p.y);
        }

        foreach (Cell cell in spawnedCells)
        {
            Vector3 gridPos = cell.transform.position;
            RectTransform rt = (RectTransform)cell.transform;

            rt.SetParent(overlayParent.transform, false);

            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); // top-left anchor
            rt.pivot = new Vector2(0.5f, 0.5f);              // center pivot (positions are room centers)
            rt.localScale = Vector3.one;

            // Size of the room in grid cells, in the un-rotated frame
            Vector2 cells;
            switch (cell.roomShape)
            {
                case RoomShape.Long_2x1:
                case RoomShape.Long_1x2:   // rotated 90 on the root, so the same un-rotated size
                    cells = new Vector2(2, 1); break;
                case RoomShape.Big:
                case RoomShape.L_Shape_original:
                case RoomShape.L_Shape_90:
                case RoomShape.L_Shape_minus90:
                case RoomShape.L_Shape_180:
                    cells = new Vector2(2, 2); break;
                default:
                    cells = Vector2.one; break;
            }
            rt.sizeDelta = cells * uiCellSize;

            // Icon keeps one cell of size, centered
            if (cell.iconRenderer != null)
            {
                RectTransform icon = cell.iconRenderer.rectTransform;
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = Vector2.zero;
                icon.sizeDelta = Vector2.one * uiCellSize * 0.6f;
            }

            rt.anchoredPosition = new Vector2(
                (gridPos.x - minX) * uiCellSize + uiMargin.x,
                (gridPos.y - maxY) * uiCellSize - uiMargin.y);
        }
    }

    // ------------------------------------------------------------------ Room growth

    private int GetNeighborCount(int index)
    {
        return mapGeneration[index - GridWidth] + mapGeneration[index - 1] + mapGeneration[index + 1] + mapGeneration[index + GridWidth];
    }

    /// <summary>The first room: always created, always a single cell.</summary>
    private void PlaceStartCell()
    {
        cellQueue.Enqueue(startIndex);
        count++;
        mapGeneration[startIndex] = 1;
        pendingRooms.Add(new PendingRoom { origin = startIndex, indexes = new List<int> { startIndex } });
    }

    /// <summary>
    /// Check if the cell is valid and assign it for the map.
    /// </summary>
    /// <param name="index">Cell index (y * GridWidth + x)</param>
    /// <param name="chance">Probability that this cell becomes a room</param>
    private bool VisitCell(int index, float chance)
    {
        if (!InPlayableArea(index)) return false;
        if (mapGeneration[index] != 0) return false;
        if (GetNeighborCount(index) > 1) return false;
        if (count >= roomAmountRange.Item2) return false;
        if (Random.value > chance) return false;

        if (Random.value < chanceToSpawnLargeRoom)
        {
            // Utilize a random value to order so we go through the list in different order every time
            foreach (var shape in shapeOffsets.OrderBy(_ => Random.value))
            {
                if (TryRoom(index, shape)) return true;
            }
        }

        cellQueue.Enqueue(index);
        count++;
        mapGeneration[index] = 1;
        pendingRooms.Add(new PendingRoom { origin = index, indexes = new List<int> { index } });

        return true;
    }

    /// <summary>
    /// Instantiate the cell at the correct position and initialize the cell object
    /// </summary>
    private void SpawnRoom(int index)
    {
        int x = index % GridWidth;
        int y = index / GridWidth;

        Vector2 position = new Vector2(x * cellSize, -y * cellSize);

        Cell newCell = Instantiate(cellPrefab, position, Quaternion.identity);
        newCell.name = newCell.name + id;
        newCell.roomType = RoomTypes.Base;
        newCell.index = index;
        newCell.roomShape = RoomShape.Single;
        newCell.cellList = new List<int> { index };
        newCell.cellList.Sort();
        spawnedCells.Add(newCell);
        id++;
    }

    /// <summary>
    /// Check for offset positions so we can place a bigger room than a 1x1
    /// </summary>
    /// <param name="origin">Current room index</param>
    /// <param name="offset">Adjacent cells</param>
    private bool TryRoom(int origin, int[] offset)
    {
        List<int> currentIndexes = new List<int> { origin };

        int originX = origin % GridWidth;

        foreach (int offsetIndex in offset)
        {
            int indexToCheck = origin + offsetIndex;

            // Make sure the index is inside the map
            if (indexToCheck < 0 || indexToCheck >= mapGeneration.Length)
                return false;

            // Stay inside the usable area and don't wrap around to another row
            int checkX = indexToCheck % GridWidth;
            int checkY = indexToCheck / GridWidth;
            if (!InPlayableArea(checkX, checkY))
                return false;
            if (Mathf.Abs(checkX - originX) > 1)
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

        // Respect the max room amount (count is per grid cell)
        if (count + currentIndexes.Count > roomAmountRange.Item2)
            return false;

        // Reserve all cells
        foreach (int index in currentIndexes)
        {
            mapGeneration[index] = 1;
            count++;
            cellQueue.Enqueue(index);
            bigRoomIndexes.Add(index);
        }

        pendingRooms.Add(new PendingRoom { origin = origin, indexes = currentIndexes });

        return true;
    }

    private void SpawnLargeRoom(int origin, List<int> indexes)
    {
        // Calculate the geometric center of the occupied cells
        float originX = origin % GridWidth;
        float originY = origin / GridWidth;

        float increaseX = originX;
        float increaseY = originY;

        foreach (int index in indexes)
        {
            float x = index % GridWidth;
            float y = index / GridWidth;

            if (Mathf.Abs(originX - x) > 1e-6) increaseX = x;
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

            bool vertical = Mathf.Abs(indexes[0] - indexes[1]) == GridWidth;

            if (vertical)
            {
                newCell.roomShape = RoomShape.Long_1x2;
                newCell.transform.rotation = Quaternion.Euler(0, 0, 90);
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