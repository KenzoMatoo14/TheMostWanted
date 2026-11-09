using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Fixes a hand-made level scene (e.g. TutorialScene) in one click:
// - Tilemaps are drawn behind the player and the whip (which sit at sorting order 0 and -1).
// - Adds a Cinemachine camera that follows the player (same settings as gamegen)
//   and stays inside the area covered by the tilemaps.
// Menu: Tools > Tutorial > Arreglar camara y latigo (works on the open scene; safe to run again).
public static class TutorialSceneSetup
{
    const int HighestTilemapOrder = -2; // whip is -1, player is 0

    [MenuItem("Tools/Tutorial/Arreglar camara y latigo")]
    public static void Fix()
    {
        var player = Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).FirstOrDefault();
        if (player == null) { Debug.LogError("[TutorialSetup] No player (PlayerController) in the open scene."); return; }

        FixSorting();
        var bounds = SetupBounds();
        var vcam = SetupCamera(player.transform, bounds);
        SetupRooms(vcam);

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("[TutorialSetup] Done. Save the scene (Ctrl+S).");
    }

    // Shift every tilemap so the top one ends at -2, keeping their order relative to each other.
    static void FixSorting()
    {
        var renderers = Object.FindObjectsByType<TilemapRenderer>(FindObjectsSortMode.None);
        if (renderers.Length == 0) return;
        int max = renderers.Max(r => r.sortingOrder);
        if (max <= HighestTilemapOrder) return;
        int shift = max - HighestTilemapOrder;
        foreach (var r in renderers)
        {
            Undo.RecordObject(r, "Fix tilemap sorting");
            r.sortingOrder -= shift;
        }
    }

    // A disabled BoxCollider2D around all tilemaps. The confiner only reads its shape,
    // so it never collides with anything.
    static BoxCollider2D SetupBounds()
    {
        Bounds? total = null;
        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            if (tm.GetUsedTilesCount() == 0) continue;
            var lb = tm.localBounds;
            var b = new Bounds(tm.transform.TransformPoint(lb.center), Vector3.Scale(lb.size, tm.transform.lossyScale));
            if (total == null) total = b; else { var t = total.Value; t.Encapsulate(b); total = t; }
        }
        if (total == null) return null;

        var go = GameObject.Find("CameraBounds");
        if (go == null) { go = new GameObject("CameraBounds"); Undo.RegisterCreatedObjectUndo(go, "Camera bounds"); }
        go.transform.position = Vector3.zero;
        var box = go.GetComponent<BoxCollider2D>();
        if (box == null) box = Undo.AddComponent<BoxCollider2D>(go);
        Undo.RecordObject(box, "Camera bounds");
        box.offset = total.Value.center;
        box.size = total.Value.size;
        box.isTrigger = true;
        box.enabled = false;
        return box;
    }

    static CinemachineCamera SetupCamera(Transform player, BoxCollider2D bounds)
    {
        var main = Camera.main;
        if (main != null)
        {
            main.orthographic = true;
            var brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(main.gameObject);
            Undo.RecordObject(brain, "Camera blend");
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, RoomBlendTime);
        }

        // The main camera (not one of the room cameras), even if it was disabled by SetupRooms.
        var vcam = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.GetComponentInParent<CameraRoom>(true) == null && c.gameObject.scene.IsValid());
        if (vcam == null)
        {
            var go = new GameObject("CinemachineCamera");
            Undo.RegisterCreatedObjectUndo(go, "Cinemachine camera");
            vcam = go.AddComponent<CinemachineCamera>();
            vcam.Lens.OrthographicSize = 8;
            var composer = go.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10;
            composer.Damping = Vector3.one;
            composer.TargetOffset = new Vector3(0, 2, 0);
        }
        Undo.RecordObject(vcam, "Camera follow");
        vcam.Follow = player;
        vcam.transform.position = player.position + new Vector3(0, 2, -10);

        if (bounds != null)
        {
            var confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner == null) confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
            Undo.RecordObject(confiner, "Camera confiner");
            confiner.BoundingShape2D = bounds;
            confiner.InvalidateBoundingShapeCache();

            // The confiner can't move the camera along an axis where the level is smaller than the view.
            float viewH = vcam.Lens.OrthographicSize * 2f;
            float viewW = viewH * 16f / 9f;
            if (bounds.size.y < viewH || bounds.size.x < viewW)
                Debug.LogWarning($"[TutorialSetup] El nivel mide {bounds.size.x:0}x{bounds.size.y:0} tiles y la camara ve ~{viewW:0}x{viewH:0}. " +
                                 "En el eje donde el nivel es mas chico la camara no se puede mover: haz el nivel mas grande.");
        }
        return vcam;
    }

    const float RoomBlendTime = 0.6f; // seconds the camera takes to slide to the next room

    // Each CameraRoom gets its own copy of the main camera, confined to the room's box.
    // CameraRoom turns the right one on at runtime; the main camera is turned off so it doesn't compete.
    static void SetupRooms(CinemachineCamera template)
    {
        var rooms = Object.FindObjectsByType<CameraRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (rooms.Length == 0 || template == null) return;

        float viewH = template.Lens.OrthographicSize * 2f;
        float viewW = viewH * 16f / 9f;
        foreach (var room in rooms)
        {
            var box = room.GetComponent<BoxCollider2D>();
            Undo.RecordObject(box, "Room area");
            box.isTrigger = true;
            room.gameObject.layer = 2; // Ignore Raycast: ground checks and the hook never hit the room area

            if (room.roomCamera != null) Undo.DestroyObjectImmediate(room.roomCamera.gameObject);
            var go = Object.Instantiate(template.gameObject, room.transform);
            go.name = "Camara " + room.name;
            Undo.RegisterCreatedObjectUndo(go, "Room camera");
            var cam = go.GetComponent<CinemachineCamera>();
            var confiner = go.GetComponent<CinemachineConfiner2D>();
            if (confiner == null) confiner = go.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = box;
            confiner.InvalidateBoundingShapeCache();
            go.SetActive(false);

            Undo.RecordObject(room, "Room camera");
            room.roomCamera = cam;
            EditorUtility.SetDirty(room);

            var size = Vector2.Scale(box.size, room.transform.lossyScale);
            if (size.y < viewH || size.x < viewW)
                Debug.LogWarning($"[TutorialSetup] {room.name} mide {size.x:0}x{size.y:0} y la camara ve ~{viewW:0}x{viewH:0}: " +
                                 "en el eje mas chico la camara no se movera dentro de ese cuarto.", room);
        }

        Undo.RecordObject(template.gameObject, "Main camera off");
        template.gameObject.SetActive(false);
        Debug.Log($"[TutorialSetup] {rooms.Length} cuartos con camara propia.");
    }

    // Creates a room area at the center of the Scene view. Resize it with the BoxCollider2D's "Edit Collider" button
    // so it covers the room from wall to wall, then run "Arreglar camara y latigo".
    [MenuItem("Tools/Tutorial/Nuevo cuarto de camara")]
    public static void NewRoom()
    {
        var parent = GameObject.Find("Cuartos");
        if (parent == null) { parent = new GameObject("Cuartos"); Undo.RegisterCreatedObjectUndo(parent, "Rooms"); }

        int n = parent.transform.childCount + 1;
        var go = new GameObject("Cuarto " + n);
        Undo.RegisterCreatedObjectUndo(go, "New room");
        go.transform.SetParent(parent.transform, false);
        go.layer = 2;
        var pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        go.transform.position = new Vector3(Mathf.Round(pivot.x), Mathf.Round(pivot.y), 0);
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(34, 20);
        go.AddComponent<CameraRoom>();
        Selection.activeGameObject = go;
    }
}
