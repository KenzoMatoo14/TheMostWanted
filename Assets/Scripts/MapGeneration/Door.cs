using UnityEngine;
using System.Collections.Generic;
    
public class Door : MonoBehaviour
{
    [Header("First Quadrant")]
    [SerializeField] private GameObject Q1_nDoor;
    [SerializeField] private GameObject Q1_sDoor;
    [SerializeField] private GameObject Q1_eDoor;
    [SerializeField] private GameObject Q1_wDoor;

    [Header("Second Quadrant")]
    [SerializeField] private GameObject Q2_nDoor;
    [SerializeField] private GameObject Q2_sDoor;
    [SerializeField] private GameObject Q2_eDoor;
    [SerializeField] private GameObject Q2_wDoor;

    [Header("Third Quadrant")]
    [SerializeField] private GameObject Q3_nDoor;
    [SerializeField] private GameObject Q3_sDoor;
    [SerializeField] private GameObject Q3_eDoor;
    [SerializeField] private GameObject Q3_wDoor;

    [Header("Fourth Quadrant")]
    [SerializeField] private GameObject Q4_nDoor;
    [SerializeField] private GameObject Q4_sDoor;
    [SerializeField] private GameObject Q4_eDoor;
    [SerializeField] private GameObject Q4_wDoor;
    public void TryPlaceDoor(int fromIndex, int quadrant,EdgeDirection direction, int[] mapGeneration, List<Cell> cellList, Cell currentCell)
    {
        int neighborIndex = fromIndex + GetOffset(direction);

        if (neighborIndex < 0 || neighborIndex >= mapGeneration.Length) return;
        if (mapGeneration[neighborIndex] != 1) return;

        SetupDoor(quadrant, direction);
    }

    /// <summary>
    /// Finds for the corresponding quadrant (starting from the north-west corner as Q1 and the south-east corner as Q4) the doors that should be opened up
    /// </summary>
    /// <param name="quadrant"></param>
    /// <param name="direction"></param>
    private void SetupDoor(int quadrant, EdgeDirection direction)
    {
        switch (quadrant)
        {
            case 1:
                SetupQ1(direction);
                break;
            case 2:
                SetupQ2(direction);
                break;
            case 3:
                SetupQ3(direction);
                break;
            case 4:
                SetupQ4(direction);
                break;
            default:
                break;
        }
    }

    private void SetupQ1(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.Up:
                OpenDoor(Q1_nDoor, nameof(Q1_nDoor));
                break;
            case EdgeDirection.Right:
                OpenDoor(Q1_eDoor, nameof(Q1_eDoor));
                break;
            case EdgeDirection.Left:
                OpenDoor(Q1_wDoor, nameof(Q1_wDoor));
                break;
            case EdgeDirection.Down:
                OpenDoor(Q1_sDoor, nameof(Q1_sDoor));
                break;
            default:
                break;
        }
    }

    private void SetupQ2(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.Up:
                OpenDoor(Q2_nDoor, nameof(Q2_nDoor));
                break;
            case EdgeDirection.Right:
                OpenDoor(Q2_eDoor, nameof(Q2_eDoor));
                break;
            case EdgeDirection.Left:
                OpenDoor(Q2_wDoor, nameof(Q2_wDoor));
                break;
            case EdgeDirection.Down:
                OpenDoor(Q2_sDoor, nameof(Q2_sDoor));
                break;
            default:
                break;
        }
    }

    private void SetupQ3(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.Up:
                OpenDoor(Q3_nDoor, nameof(Q3_nDoor));
                break;
            case EdgeDirection.Right:
                OpenDoor(Q3_eDoor, nameof(Q3_eDoor));
                break;
            case EdgeDirection.Left:
                OpenDoor(Q3_wDoor, nameof(Q3_wDoor));
                break;
            case EdgeDirection.Down:
                OpenDoor(Q3_sDoor, nameof(Q3_sDoor));
                break;
            default:
                break;
        }
    }

    private void SetupQ4(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.Up:
                OpenDoor(Q4_nDoor, nameof(Q4_nDoor));
                break;
            case EdgeDirection.Right:
                OpenDoor(Q4_eDoor, nameof(Q4_eDoor));
                break;
            case EdgeDirection.Left:
                OpenDoor(Q4_wDoor, nameof(Q4_wDoor));
                break;
            case EdgeDirection.Down:
                OpenDoor(Q4_sDoor, nameof(Q4_sDoor));
                break;
            default:
                break;
        }
    }

    private readonly List<GameObject> openedDoors = new List<GameObject>();

    /// <summary>Door objects that were opened (disabled) for this room.</summary>
    public IReadOnlyList<GameObject> OpenedDoors => openedDoors;

    // Doors are walls that get disabled to open the passage
    private void OpenDoor(GameObject door, string fieldName)
    {
        if (door == null)
        {
            Debug.LogWarning($"Door: '{fieldName}' is not assigned on '{name}'. Assign it in the prefab.", this);
            return;
        }
        door.SetActive(false);
        if (!openedDoors.Contains(door)) openedDoors.Add(door);
    }

    private int GetOffset(EdgeDirection direction)
    {
        switch (direction)
        {
            case EdgeDirection.Up:
                return -10;
            case EdgeDirection.Right:
                return 1;
            case EdgeDirection.Left:
                return -1;
            case EdgeDirection.Down:
                return 10;
            default:
                return 0;
        }
    }
}
