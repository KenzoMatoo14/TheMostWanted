using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Cell : MonoBehaviour
{
    public int index;
    public MapGenerationBase.RoomTypes roomType;
    public MapGenerationBase.RoomShape roomShape;
    public int enemyCount;

    // Filled by the map generator, read by the room spawner
    public int eliteCount;
    public bool hasBoss;
    public bool hasItem;
    public bool isStartRoom;

    public Image iconRenderer;
    public Image spriteRenderer;

    public List<int> cellList = new List<int>();

    //TODO: Remove this when we add the different assets for each room layout
    public void SetSpecialRoomSprite(Sprite s)
    {
        if (s == null)
        {
            iconRenderer.gameObject.SetActive(false);
            return;
        }
        iconRenderer.gameObject.SetActive(true);
        iconRenderer.sprite = s;
    }

    public void SetRoomSprite(Sprite s)
    {
        spriteRenderer.sprite = s;
    }

    public void RotateRoom(int origin, List<int> connectedRooms)
    {
        // Neighbor offsets for the current grid width (they used to be hardcoded for a width of 10)
        int w = MapGenerationBase.GridWidth;

        bool rotate90 =
            (connectedRooms.Contains(origin - 1) && connectedRooms.Contains(origin - w - 1)) ||
            (connectedRooms.Contains(origin + w) && connectedRooms.Contains(origin + w + 1)) ||
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin - w));

        bool rotateMinus90 =
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin + w + 1)) ||
            (connectedRooms.Contains(origin - 1) && connectedRooms.Contains(origin + w)) ||
            (connectedRooms.Contains(origin - w) && connectedRooms.Contains(origin - w - 1));

        bool rotate180 =
            (connectedRooms.Contains(origin + w - 1) && connectedRooms.Contains(origin + w)) ||
            (connectedRooms.Contains(origin - 1) && connectedRooms.Contains(origin - w)) ||
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin - w + 1));

        if (rotate90)
        {
            this.roomShape = MapGenerationBase.RoomShape.L_Shape_90;
            ApplyRotation(90);
        }
        else if (rotateMinus90)
        {
            this.roomShape = MapGenerationBase.RoomShape.L_Shape_minus90;
            ApplyRotation(-90);
        }
        else if (rotate180)
        {
            this.roomShape = MapGenerationBase.RoomShape.L_Shape_180;
            ApplyRotation(180);
        }
    }

    private void ApplyRotation(int angle)
    {
        gameObject.transform.rotation = Quaternion.Euler(0, 0, angle);
    }
}