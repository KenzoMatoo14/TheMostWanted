using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
                Q1_nDoor.SetActive(false);
                break;
            case EdgeDirection.Right:
                Q1_eDoor.SetActive(false);
                break;
            case EdgeDirection.Left:
                Q1_wDoor.SetActive(false);
                break;
            case EdgeDirection.Down:
                Q1_sDoor.SetActive(false);
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
                Q2_nDoor.SetActive(false);
                break;
            case EdgeDirection.Right:
                Q2_eDoor.SetActive(false);
                break;
            case EdgeDirection.Left:
                Q2_wDoor.SetActive(false);
                break;
            case EdgeDirection.Down:
                Q2_sDoor.SetActive(false);
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
                Q3_nDoor.SetActive(false);
                break;
            case EdgeDirection.Right:
                Q3_eDoor.SetActive(false);
                break;
            case EdgeDirection.Left:
                Q3_wDoor.SetActive(false);
                break;
            case EdgeDirection.Down:
                Q3_sDoor.SetActive(false);
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
                Q4_nDoor.SetActive(false);
                break;
            case EdgeDirection.Right:
                Q4_eDoor.SetActive(false);
                break;
            case EdgeDirection.Left:
                Q4_wDoor.SetActive(false);
                break;
            case EdgeDirection.Down:
                Q4_sDoor.SetActive(false);
                break;
            default:
                break;
        }
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
