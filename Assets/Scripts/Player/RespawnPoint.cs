using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
    [Header("Visualización")]
    [SerializeField] private Color gizmoColor = Color.green;
    [SerializeField] private float gizmoSize = 0.5f;
    [SerializeField] private bool showLabel = true;

    [Header("Activación automática")]
    [SerializeField] private bool setAsRespawnOnTrigger = true;
    [Tooltip("Si está marcado, este punto de respawn se activa cuando el jugador lo toca")]

    private void Start()
    {
        // Asegurarse de que tenga el tag "Respawn"
        if (!gameObject.CompareTag("Respawn"))
        {
            gameObject.tag = "Respawn";
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (setAsRespawnOnTrigger && other.CompareTag("Player"))
        {
            // Buscar el DeathManager y actualizar el punto de respawn
            DeathManager deathManager = FindObjectOfType<DeathManager>();
            if (deathManager != null)
            {
                deathManager.SetRespawnPoint(transform);
                Debug.Log($"Checkpoint activado: {gameObject.name}");
            }
        }
    }

    // Visualizar el punto de respawn en el editor
    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;

        // Dibujar una esfera
        Gizmos.DrawWireSphere(transform.position, gizmoSize);

        // Dibujar una cruz
        Vector3 pos = transform.position;
        Gizmos.DrawLine(pos + Vector3.up * gizmoSize, pos + Vector3.down * gizmoSize);
        Gizmos.DrawLine(pos + Vector3.left * gizmoSize, pos + Vector3.right * gizmoSize);

#if UNITY_EDITOR
        if (showLabel)
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * (gizmoSize + 0.2f),
                                      "Respawn Point",
                                      new GUIStyle() { normal = new GUIStyleState() { textColor = gizmoColor } });
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        // Cuando está seleccionado, dibujarlo más grande y sólido
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.3f);
        Gizmos.DrawSphere(transform.position, gizmoSize);
    }
}