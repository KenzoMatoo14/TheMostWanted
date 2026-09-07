using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ScriptableRooms", menuName = "Map Generation/ScriptableRooms")]
public class ScriptableRooms : ScriptableObject
{
    public MapGenerationBase.RoomTypes roomType;
    public MapGenerationBase.RoomShape roomShape;

    public int[] occupiedTiles;
    public GameObject[] roomVariations;
}
