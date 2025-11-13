using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Caja que puede ser capturada instantáneamente y lanzada.
/// Se destruye al primer impacto con daño.
/// </summary>
public class ThrowableBox : MonoBehaviour, IDamageable, ICaptureable
{
    [Header("Box Settings")]
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private bool destroyOnAnyDamage = true;

    [Header("Capture Settings")]
    [SerializeField] private float captureDifficulty = 1f; // Muy fácil de capturar

    /*
    [Header("Visual Feedback")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private AudioClip breakSound;
    [SerializeField] private ParticleSystem breakParticles;
    */

    [Header("Events")]
    public UnityEvent OnDamageTaken;
    public UnityEvent OnDestroyed;
    public UnityEvent OnCaptured;
    public UnityEvent OnReleased;

    // Estado interno
    private int currentHealth;
    private bool isCaptured = false;
    private bool isBeingDestroyed = false;

    // Referencias
    private Rigidbody2D rb;
    private Collider2D boxCollider;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<Collider2D>();

        currentHealth = maxHealth;

        // Configurar Rigidbody2D si existe
        if (rb != null)
        {
            rb.gravityScale = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    #region IDamageable Implementation

    public void TakeDamage(int amount, Vector2 damageSourcePosition = default)
    {
        if (isBeingDestroyed) return;

        currentHealth -= amount;

        Debug.Log($"Caja {gameObject.name} recibió {amount} de daño. Vida restante: {currentHealth}");

        OnDamageTaken?.Invoke();

        // Si debe destruirse con cualquier daño o si la vida llega a 0
        if (destroyOnAnyDamage || currentHealth <= 0)
        {
            DestroyBox();
        }
    }
    public bool IsDead()
    {
        return isBeingDestroyed || currentHealth <= 0;
    }
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    public int GetMaxHealth()
    {
        return maxHealth;
    }
    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    #endregion

    #region ICaptureable Implementation

    public bool StartCapture()
    {
        if (isBeingDestroyed || isCaptured)
        {
            return false;
        }

        Debug.Log($"Iniciando captura de caja: {gameObject.name}");
        return true;
    }
    public bool CompleteCapture()
    {
        if (isBeingDestroyed)
        {
            return false;
        }

        isCaptured = true;

        Debug.Log($"¡Caja {gameObject.name} capturada!");

        // Congelar la caja
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
        }

        OnCaptured?.Invoke();

        return true;
    }
    public bool Release(Vector2 releaseVelocity = default)
    {
        if (!isCaptured)
        {
            return false;
        }

        isCaptured = false;

        Debug.Log($"Caja {gameObject.name} liberada con velocidad: {releaseVelocity.magnitude:F2}");

        // Restaurar físicas
        if (rb != null)
        {
            rb.gravityScale = 1f;

            if (releaseVelocity != Vector2.zero)
            {
                rb.linearVelocity = releaseVelocity;
            }
        }

        OnReleased?.Invoke();

        return true;
    }
    public void CancelCapture()
    {
        // Para cajas no necesitamos hacer nada especial
        Debug.Log($"Captura de caja {gameObject.name} cancelada");
    }
    public bool CanBeCaptured()
    {
        return !isBeingDestroyed && !isCaptured;
    }
    public bool IsCaptured()
    {
        return isCaptured;
    }
    public bool IsBeingCaptured()
    {
        // Las cajas se capturan instantáneamente, así que siempre es false
        return false;
    }
    public float GetCaptureDifficulty()
    {
        return captureDifficulty;
    }
    public float GetCaptureStartProgress()
    {
        // Las cajas se capturan instantáneamente (100% de progreso inicial)
        return 1f;
    }
    public float GetCaptureSpeedMultiplier()
    {
        // Captura instantánea
        return 999f;
    }

    #endregion

    #region Box Destruction

    private void DestroyBox()
    {
        
        if (isBeingDestroyed) return;

        isBeingDestroyed = true;

        Debug.Log($"Caja {gameObject.name} destruida");

        OnDestroyed?.Invoke();

        // Efectos visuales
        /*
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }

        if (breakParticles != null)
        {
            breakParticles.transform.SetParent(null);
            breakParticles.Play();
            Destroy(breakParticles.gameObject, 2f);
        }

        // Sonido
        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, transform.position);
        }
        */

        // Destruir el objeto
        Destroy(gameObject);
    }

    #endregion

    #region Debug

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Color según estado
        if (isCaptured)
        {
            Gizmos.color = Color.cyan;
        }
        else if (isBeingDestroyed)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }

        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }

    #endregion
}