using UnityEngine;

public class Cell : MonoBehaviour
{
    public int index;
    public MapGenerationBase.RoomTypes roomType;
    public int enemyCount;
    public SpriteRenderer iconRenderer;

    //TODO: Remove this when we add the different assets for each room layout
    public void SetSpecialRoomSprite(Sprite s)
    {
        iconRenderer.sprite = s;
    }
}
