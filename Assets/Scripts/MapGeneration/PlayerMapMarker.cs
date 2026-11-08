using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows in which room the player is on the map overlay by moving a marker onto that room's cell.
/// Put it on any object (e.g. the player), assign the player Transform (or tag the player "Player").
///
/// How it finds the room: the player's position is converted to a grid cell using the RoomManager's transform and
/// offsetX / offsetY (one grid cell = offsetX x offsetY world units, rooms centered on their grid position),
/// then the Cell that contains that grid cell is looked up. If the player is in a spot no room covers
/// (e.g. the empty corner of an L room) the closest room is used.
/// </summary>
public class PlayerMapMarker : MonoBehaviour
{
    [Tooltip("The player. If empty, the object tagged 'Player' is used")]
    [SerializeField] private Transform player;

    [Header("Marker")]
    [Tooltip("Optional UI prefab for the marker. If empty, a simple colored square is created")]
    [SerializeField] private RectTransform markerPrefab;
    [SerializeField] private Color markerColor = new Color(1f, 0.85f, 0.1f, 1f);
    [Tooltip("Size of the generated marker as a fraction of one map cell")]
    [SerializeField, Range(0.1f, 1f)] private float markerScale = 0.35f;

    [Header("Room highlight (optional)")]
    [SerializeField] private bool tintCurrentRoom = true;
    [SerializeField] private Color currentRoomTint = new Color(1f, 1f, 0.6f, 1f);

    private readonly Dictionary<int, Cell> cellByGridIndex = new Dictionary<int, Cell>();
    private RectTransform marker;
    private Cell currentCell;
    private Color currentCellOriginalColor;
    private MapGenerationBase generator;

    private void Start()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;
        }

        generator = MapGenerationBase.instance;
        if (generator == null)
        {
            Debug.LogWarning("PlayerMapMarker: no map generator in the scene.");
            enabled = false;
            return;
        }

        generator.MapGenerated += RebuildLookup;
        if (generator.getSpawnedCells != null && generator.getSpawnedCells.Count > 0) RebuildLookup();
    }

    private void OnDestroy()
    {
        if (generator != null) generator.MapGenerated -= RebuildLookup;
    }

    // A new map was generated: forget the old cells and index the new ones by every grid cell they cover
    private void RebuildLookup()
    {
        cellByGridIndex.Clear();
        currentCell = null;

        foreach (Cell cell in generator.getSpawnedCells)
            foreach (int index in cell.cellList)
                cellByGridIndex[index] = cell;

        if (marker != null) marker.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (player == null || cellByGridIndex.Count == 0 || RoomManager.instance == null) return;

        Cell cell = FindCellAt(player.position);
        if (cell == null || cell == currentCell) return;

        SetCurrentCell(cell);
    }

    private Cell FindCellAt(Vector3 worldPosition)
    {
        RoomManager rm = RoomManager.instance;
        if (Mathf.Approximately(rm.offsetX, 0f) || Mathf.Approximately(rm.offsetY, 0f)) return null;

        // Rooms are placed at (gridX * offsetX, -gridY * offsetY) in the RoomManager's local space
        Vector3 local = rm.transform.InverseTransformPoint(worldPosition);
        int gx = Mathf.RoundToInt(local.x / rm.offsetX);
        int gy = Mathf.RoundToInt(-local.y / rm.offsetY);
        int width = MapGenerationBase.GridWidth;

        if (gx >= 0 && gx < width && gy >= 0 && gy < MapGenerationBase.GridHeight &&
            cellByGridIndex.TryGetValue(gy * width + gx, out Cell exact))
            return exact;

        // Not inside any room cell: use the closest one
        Cell best = null;
        float bestDist = float.MaxValue;
        foreach (var pair in cellByGridIndex)
        {
            int dx = pair.Key % width - gx;
            int dy = pair.Key / width - gy;
            float d = dx * dx + dy * dy;
            if (d < bestDist) { bestDist = d; best = pair.Value; }
        }
        return best;
    }

    private void SetCurrentCell(Cell cell)
    {
        if (tintCurrentRoom) RestoreTint();

        currentCell = cell;
        RectTransform cellRect = (RectTransform)cell.transform;

        EnsureMarker(cellRect);
        marker.SetParent(cellRect.parent, false);
        marker.anchorMin = marker.anchorMax = cellRect.anchorMin;
        marker.anchoredPosition = cellRect.anchoredPosition;
        marker.gameObject.SetActive(true);
        marker.SetAsLastSibling();   // draw above every room

        if (tintCurrentRoom && cell.spriteRenderer != null)
        {
            currentCellOriginalColor = cell.spriteRenderer.color;
            cell.spriteRenderer.color = currentRoomTint;
        }
    }

    private void RestoreTint()
    {
        if (currentCell != null && currentCell.spriteRenderer != null)
            currentCell.spriteRenderer.color = currentCellOriginalColor;
    }

    private void EnsureMarker(RectTransform cellRect)
    {
        if (marker != null) return;

        if (markerPrefab != null)
        {
            marker = Instantiate(markerPrefab);
        }
        else
        {
            var go = new GameObject("PlayerMapMarker", typeof(RectTransform), typeof(Image));
            marker = (RectTransform)go.transform;
            var image = go.GetComponent<Image>();
            image.color = markerColor;
            image.raycastTarget = false;

            // Same size for every room: a fraction of one map cell (measured on a 1x1 room)
            RectTransform reference = cellRect;
            foreach (Cell c in generator.getSpawnedCells)
                if (c.roomShape == MapGenerationBase.RoomShape.Single) { reference = (RectTransform)c.transform; break; }
            float cellSize = Mathf.Min(reference.sizeDelta.x, reference.sizeDelta.y);
            marker.sizeDelta = Vector2.one * cellSize * markerScale;
        }
        marker.pivot = new Vector2(0.5f, 0.5f);
    }
}