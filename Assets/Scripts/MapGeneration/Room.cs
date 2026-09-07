using UnityEngine;
using System.Collections.Generic;

public enum EdgeDirection
{
    Up, Down, Left, Right
}

public class Room : MonoBehaviour
{
    private Door roomDoor;
    public void SetupRoom(Cell currentCell, ScriptableRooms room)
    {
        GameObject gameObject = Instantiate(room.roomVariations[Random.Range(0, room.roomVariations.Length)]);
        gameObject.transform.SetParent(this.gameObject.transform);
        gameObject.transform.localPosition = Vector3.zero;

        roomDoor = gameObject.GetComponent<Door>();

        int[] mapGeneration = MapGenerationBase.instance.getMapGeneration;
        List<Cell> cellList = MapGenerationBase.instance.getSpawnedCells;

        switch (currentCell.roomShape)
        {
            case MapGenerationBase.RoomShape.Single:
                Setup1x1(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.Big:
                Setup2x2(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.Long_2x1:
                Setup2x1(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.Long_1x2:
                Setup1x2(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.L_Shape_original:
                SetupL_original(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.L_Shape_minus90:
                SetupL_minus90(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.L_Shape_90:
                SetupL_90(currentCell, mapGeneration, cellList);
                break;

            case MapGenerationBase.RoomShape.L_Shape_180:
                SetupL_180(currentCell, mapGeneration, cellList);
                break;

            default:
                break;
        }
    }

    public void Setup1x1(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Right, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Down, mapGeneration, cellList, cell);
    }
    public void Setup2x1(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void Setup1x2(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Right, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void Setup2x2(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Right, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Down, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 4, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 4, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void SetupL_original(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Right, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void SetupL_minus90(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Down, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Right, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void SetupL_90(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Right, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Down, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
    public void SetupL_180(Cell cell, int[] mapGeneration, List<Cell> cellList)
    {
        int currentCell = cell.cellList[0];

        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Left, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 1, EdgeDirection.Right, mapGeneration, cellList, cell);

        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Up, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 2, EdgeDirection.Left, mapGeneration, cellList, cell);
        
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Down, mapGeneration, cellList, cell);
        roomDoor.TryPlaceDoor(currentCell, 3, EdgeDirection.Right, mapGeneration, cellList, cell);
    }
}
