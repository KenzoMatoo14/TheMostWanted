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
            Debug.Log("Destroy");
            GameObject.Destroy(r.gameObject);
        }

        createdRooms.Clear();
        int i = 0;
        foreach(Cell cell in spawnedCells)
        {
            // Find the first acceptable room variation for the required criteria in the Cell object
            ScriptableRooms foundRoom = rooms.FirstOrDefault(x => x.roomShape == cell.roomShape && x.roomType == cell.roomType && DoesTileMatchCell(x.occupiedTiles, cell));

            Vector2 currentPosition = cell.transform.position;
            Vector2 convertedPosition = new Vector2(currentPosition.x *  offsetX, currentPosition.y * offsetY);

            Room spawnedRoom = Instantiate(roomPrefab, convertedPosition, Quaternion.identity);
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

        // Transform the relative position of the grid of the neighbor tiles to global position (from 0 to 100)
        foreach (int index in cell.cellList)
        {
            int dx = (index % 10) - (minIndex % 10);
            int dy = (index / 10) - (minIndex / 10);

            normalizedCell.Add(dy * 10 + dx);
        }

        normalizedCell.Sort();
        int[] sortedOccupied = (int[])occupiedTiles.Clone();
        Array.Sort(sortedOccupied);

        // See if the tiles match with the SO
        return normalizedCell.SequenceEqual(sortedOccupied);
    }
}
