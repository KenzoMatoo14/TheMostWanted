using UnityEngine;

public class ImpactTrigger : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Manager del efecto de impacto (se busca automáticamente si no se asigna)")]
    public ImpactFrameManager impactManager;

    [Header("Configuración")]
    [Tooltip("Tags que pueden activar el efecto (dejar vacío para cualquier objeto)")]
    public string[] validTags = new string[] { };

    [Tooltip("Activar solo en la primera colisión")]
    public bool triggerOnce = false;

    [Tooltip("Cooldown entre activaciones (segundos)")]
    public float cooldown = 0.5f;

    [Header("Condiciones")]
    [Tooltip("Velocidad mínima de impacto para activar el efecto")]
    public float minImpactVelocity = 2f;

    [Tooltip("Usar OnCollisionEnter (física 3D)")]
    public bool useCollision3D = true;

    [Tooltip("Usar OnCollisionEnter2D (física 2D)")]
    public bool useCollision2D = false;

    [Tooltip("Usar OnTriggerEnter (triggers 3D)")]
    public bool useTrigger3D = false;

    [Tooltip("Usar OnTriggerEnter2D (triggers 2D)")]
    public bool useTrigger2D = false;

    private bool hasTriggered = false;
    private float lastTriggerTime = -999f;

    private void Start()
    {
        // Buscar manager si no está asignado
        if (impactManager == null)
        {
            impactManager = FindObjectOfType<ImpactFrameManager>();

            if (impactManager == null)
            {
                Debug.LogWarning($"⚠️ {gameObject.name}: No se encontró ImpactFrameManager en la escena");
            }
        }
    }

    #region Collision 3D
    private void OnCollisionEnter(Collision collision)
    {
        if (!useCollision3D) return;

        // Verificar velocidad de impacto
        if (collision.relativeVelocity.magnitude < minImpactVelocity) return;

        TryTriggerImpact(collision.gameObject);
    }
    #endregion

    #region Collision 2D
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!useCollision2D) return;

        // Verificar velocidad de impacto
        if (collision.relativeVelocity.magnitude < minImpactVelocity) return;

        TryTriggerImpact(collision.gameObject);
    }
    #endregion

    #region Trigger 3D
    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger3D) return;
        TryTriggerImpact(other.gameObject);
    }
    #endregion

    #region Trigger 2D
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTrigger2D) return;
        TryTriggerImpact(other.gameObject);
    }
    #endregion

    /// <summary>
    /// Intenta disparar el efecto de impacto si se cumplen las condiciones
    /// </summary>
    private void TryTriggerImpact(GameObject otherObject)
    {
        // Verificar si ya se disparó (si triggerOnce está activo)
        if (triggerOnce && hasTriggered) return;

        // Verificar cooldown
        if (Time.time - lastTriggerTime < cooldown) return;

        // Verificar manager
        if (impactManager == null)
        {
            Debug.LogWarning($"⚠️ {gameObject.name}: No hay ImpactFrameManager asignado");
            return;
        }

        // Verificar tags válidos
        if (validTags.Length > 0)
        {
            bool tagValid = false;
            foreach (string tag in validTags)
            {
                if (otherObject.CompareTag(tag))
                {
                    tagValid = true;
                    break;
                }
            }

            if (!tagValid) return;
        }

        // ¡DISPARAR EFECTO!
        impactManager.TriggerImpact(gameObject, otherObject);

        // Actualizar estado
        hasTriggered = true;
        lastTriggerTime = Time.time;
    }

    /// <summary>
    /// Resetea el estado de trigger once (útil para reutilizar el objeto)
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    /// <summary>
    /// Dispara manualmente el efecto de impacto
    /// </summary>
    public void ManualTrigger(GameObject otherObject = null)
    {
        if (impactManager != null)
        {
            impactManager.TriggerImpact(gameObject, otherObject);
            hasTriggered = true;
            lastTriggerTime = Time.time;
        }
    }
}