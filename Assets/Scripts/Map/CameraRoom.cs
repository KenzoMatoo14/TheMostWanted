using Unity.Cinemachine;
using UnityEngine;

// One room of a hand-made level. The BoxCollider2D is the room's area: the camera stays inside it
// while the player is in the room, and slides to the next room when the player crosses into it.
// Create rooms with Tools > Tutorial > Nuevo cuarto de camara. If the room has no camera yet,
// it makes one in Play mode by copying the scene's CinemachineCamera.
[RequireComponent(typeof(BoxCollider2D))]
public class CameraRoom : MonoBehaviour
{
    [Tooltip("Camera that only sees this room. Leave empty and it is created when you press Play.")]
    public CinemachineCamera roomCamera;

    static CameraRoom current;
    BoxCollider2D area;

    void Awake()
    {
        area = GetComponent<BoxCollider2D>();
        area.isTrigger = true;
        if (roomCamera == null) roomCamera = CreateCamera();
        if (roomCamera != null) roomCamera.gameObject.SetActive(false);
    }

    // Copy of the main camera (the one that isn't inside a room), kept inside this room's box.
    // The main camera is turned off so only room cameras are used.
    CinemachineCamera CreateCamera()
    {
        CinemachineCamera template = null;
        foreach (var c in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (c.GetComponentInParent<CameraRoom>(true) == null) { template = c; break; }
        if (template == null)
        {
            Debug.LogWarning($"[CameraRoom] {name}: no CinemachineCamera in the scene to copy.", this);
            return null;
        }

        var go = Instantiate(template.gameObject, transform);
        go.name = "Camara " + name;
        var confiner = go.GetComponent<CinemachineConfiner2D>();
        if (confiner == null) confiner = go.AddComponent<CinemachineConfiner2D>();
        confiner.BoundingShape2D = area;
        confiner.InvalidateBoundingShapeCache();
        template.gameObject.SetActive(false);
        return go.GetComponent<CinemachineCamera>();
    }

    void Start()
    {
        // Pick the room the player starts in.
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null && area.OverlapPoint(player.transform.position)) Enter();
    }

    // Switch when the player's center crosses into this room, so standing in a doorway doesn't flicker.
    void OnTriggerStay2D(Collider2D other)
    {
        if (current == this) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null && area.OverlapPoint(player.transform.position)) Enter();
    }

    void Enter()
    {
        if (current != null && current.roomCamera != null) current.roomCamera.gameObject.SetActive(false);
        current = this;
        if (roomCamera != null) roomCamera.gameObject.SetActive(true);
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }
}
