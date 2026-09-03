using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int index;
    public MapGenerationBase.RoomTypes roomType;
    public MapGenerationBase.RoomShape roomShape;
    public int enemyCount;

    public SpriteRenderer iconRenderer;
    public SpriteRenderer spriteRenderer;

    //TODO: Remove this when we add the different assets for each room layout
    public void SetSpecialRoomSprite(Sprite s)
    {
        iconRenderer.sprite = s;
    }

    public void SetRoomSprite(Sprite s)
    {
        spriteRenderer.sprite = s;
    }

    public void RotateRoom(int origin, List<int> connectedRooms)
    {
        bool rotate90 =
            (connectedRooms.Contains(origin - 1) && connectedRooms.Contains(origin - 11)) ||
            (connectedRooms.Contains(origin + 10) && connectedRooms.Contains(origin + 11)) ||
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin - 10));

        bool rotateMinus90 =
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin + 11)) ||
            (connectedRooms.Contains(origin - 10) && connectedRooms.Contains(origin + 10)) ||
            (connectedRooms.Contains(origin - 10) && connectedRooms.Contains(origin - 11));

        bool rotate180 =
            (connectedRooms.Contains(origin + 9) && connectedRooms.Contains(origin + 10)) ||
            (connectedRooms.Contains(origin - 1) && connectedRooms.Contains(origin - 10)) ||
            (connectedRooms.Contains(origin + 1) && connectedRooms.Contains(origin - 9));

        if (rotate90)
        {
            ApplyRotation(90);
        }
        else if (rotateMinus90)
        {
            ApplyRotation(-90);
        }
        else if (rotate180)
        {
            ApplyRotation(180);
        }
    }

    private void ApplyRotation(int angle)
    {
        gameObject.transform.rotation = Quaternion.Euler(0,0,angle);
    }
}
