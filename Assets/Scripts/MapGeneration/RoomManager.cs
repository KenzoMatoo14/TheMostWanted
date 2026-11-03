using UnityEngine;

using System.Collections.Generic;
using System.Linq;
using System;

public class RoomManager : MonoBehaviour
{
    private List<Room> createdRooms;

    [Header("Offset Variables")]
    public float offsetX;
    public float offsetY;

    [Header("Prefab References")]
    public Room roomPrefab;

    [Header("SO References")]
    public ScriptableRooms[] rooms;

    [Header("Cave Generation")]
    public CaveSettings caveSettings = new CaveSettings();

    [Header("Spawning (enemies, boss, item, exit)")]
    public SpawnSettings spawnSettings = new SpawnSettings();

    public static RoomManager instance;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        instance = this;
        createdRooms = new List<Room>();
    }

    public void SetupRooms(List<Cell> spawnedCells)
    {
        foreach (Room r in createdRooms)
        {
            GameObject.Destroy(r.gameObject);
        }

        createdRooms.Clear();
        int i = 0;
        foreach (Cell cell in spawnedCells)
        {
            // Find the first acceptable room variation for the required criteria in the Cell object
            ScriptableRooms foundRoom = rooms.FirstOrDefault(x => x != null && x.roomShape == cell.roomShape && x.roomType == cell.roomType && DoesTileMatchCell(x.occupiedTiles, cell));

            if (foundRoom == null)
            {
                Debug.LogError($"RoomManager: no ScriptableRooms matches {cell.roomType} / {cell.roomShape} " +
                               $"(cells: {string.Join(",", cell.cellList)}). Skipping room for {cell.name}.");
                continue;
            }

            Vector2 currentPosition = cell.transform.position;
            Vector2 convertedPosition = new Vector2(currentPosition.x * offsetX, currentPosition.y * offsetY);

            Room spawnedRoom = Instantiate(roomPrefab, convertedPosition, Quaternion.identity);
            spawnedRoom.transform.SetParent(this.transform, false);
            spawnedRoom.name = spawnedRoom.name + i;
            i++;
            spawnedRoom.SetupRoom(cell, foundRoom);

            createdRooms.Add(spawnedRoom);
        }
    }

    private bool DoesTileMatchCell(int[] occupiedTiles, Cell cell)
    {
        if (occupiedTiles.Length != cell.cellList.Count) return false;

        int minIndex = cell.cellList.Min();
        List<int> normalizedCell = new List<int>();

        // Transform the cells to positions relative to the first one. The real grid width is used to read the cells,
        // but the result always uses a stride of 10, which is how ScriptableRooms.occupiedTiles are authored,
        // so the existing assets keep working with any grid size.
        int w = MapGenerationBase.GridWidth;
        foreach (int index in cell.cellList)
        {
            int dx = (index % w) - (minIndex % w);
            int dy = (index / w) - (minIndex / w);

            normalizedCell.Add(dy * 10 + dx);
        }

        normalizedCell.Sort();
        int[] sortedOccupied = (int[])occupiedTiles.Clone();
        Array.Sort(sortedOccupied);

        // See if the tiles match with the SO
        return normalizedCell.SequenceEqual(sortedOccupied);
    }
}