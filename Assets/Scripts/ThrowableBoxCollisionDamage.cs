using UnityEngine;

/// <summary>
/// Script que se añade a una caja capturada para causar daño por colisión.
/// Similar a ReleasedEnemyCollisionDamage pero adaptado para cajas.
/// </summary>
public class ThrowableBoxCollisionDamage : MonoBehaviour
{
    private Rigidbody2D rb;
    private ThrowableBox box;

    [Header("Damage Settings")]
    [SerializeField] private float minVelocityForDamage = 2f;
    [SerializeField] private float damageMultiplier = 3f;
    [SerializeField] private LayerMask damageableLayers = (1 << 6) | (1 << 9);
    [SerializeField] private bool destroyOnFirstHit = true;

    [Header("Knockback Settings")]
    [SerializeField] private bool applyKnockbackOnHit = true;
    [Tooltip("Multiplicador del knockback basado en la velocidad de impacto")]
    [SerializeField] private float knockbackVelocityMultiplier = 1.2f;

    [Header("Duration Settings")]
    [SerializeField] private float activeDuration = 5f;
    [SerializeField] private float velocityThreshold = 1f;

    [Header("Impact Frame Settings")]
    [SerializeField] private bool enableImpactFrame = true;
    [SerializeField] private float minVelocityForImpactFrame = 5f;
    [Tooltip("Solo activar Impact Frame si el enemigo muere de un golpe")]
    [SerializeField] private bool onlyOnKill = true;

    private float startTime;
    private bool hasDealtDamage = false;
    private ImpactFrameManager impactManager;

    public void Initialize(float minVel, float damageMult, LayerMask layers, bool destroyOnHit = true, float duration = 5f)
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<ThrowableBox>();

        if (rb == null)
        {
            Debug.LogError($"ThrowableBoxCollisionDamage: No Rigidbody2D en {gameObject.name}");
            Destroy(this);
            return;
        }

        minVelocityForDamage = minVel;
        damageMultiplier = damageMult;
        damageableLayers = layers;
        destroyOnFirstHit = destroyOnHit;
        activeDuration = duration;

        startTime = Time.time;

        // Buscar Impact Frame Manager
        if (enableImpactFrame)
        {
            impactManager = FindObjectOfType<ImpactFrameManager>();
            if (impactManager == null)
            {
                Debug.LogWarning("ThrowableBoxCollisionDamage: No se encontró ImpactFrameManager en la escena");
            }
        }

        Debug.Log($"ThrowableBoxCollisionDamage inicializado en {gameObject.name} por {activeDuration}s");
    }
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        box = GetComponent<ThrowableBox>();

        // Buscar Impact Frame Manager
        if (enableImpactFrame)
        {
            impactManager = FindObjectOfType<ImpactFrameManager>();
        }
    }

    void Update()
    {
        // Verificar expiración por tiempo
        if (Time.time - startTime >= activeDuration)
        {
            Debug.Log($"{gameObject.name} - Tiempo de colisión expirado");
            Destroy(this);
            return;
        }

        // Verificar expiración por velocidad baja
        if (rb != null && rb.linearVelocity.magnitude < velocityThreshold)
        {
            Debug.Log($"{gameObject.name} - Velocidad muy baja, desactivando colisiones");
            Destroy(this);
            return;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Verificar si ya causó daño y debe destruirse
        if (hasDealtDamage && destroyOnFirstHit)
            return;

        // Verificar capa
        if (((1 << collision.gameObject.layer) & damageableLayers) == 0)
            return;

        // Verificar velocidad
        float currentVelocity = rb.linearVelocity.magnitude;
        if (currentVelocity < minVelocityForDamage)
            return;

        // Aplicar daño
        ApplyCollisionDamage(collision, currentVelocity);
    }

    void ApplyCollisionDamage(Collision2D collision, float velocityMagnitude)
    {
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Calcular daño basado en velocidad
            float velocityRatio = velocityMagnitude / minVelocityForDamage;
            int damage = Mathf.RoundToInt(velocityRatio * damageMultiplier);
            damage = Mathf.Max(damage, 1);

            // Calcular posición del impacto para el knockback
            Vector2 impactPoint = collision.contacts.Length > 0
                ? collision.contacts[0].point
                : (Vector2)transform.position;

            int healthBeforeDamage = damageable.GetCurrentHealth();
            bool wasAlive = !damageable.IsDead();

            // Aplicar stun si es un enemigo
            EnemyBase enemyHit = damageable as EnemyBase;
            if (enemyHit != null)
            {
                enemyHit.AddStunned(damage * 1.5f);
            }

            // Aplicar daño con knockback
            if (applyKnockbackOnHit)
            {
                // La dirección del knockback es la dirección de la velocidad de la caja
                // Para simular el impacto, usamos un punto "detrás" de la dirección de movimiento
                Vector2 knockbackSource = impactPoint - rb.linearVelocity.normalized * 0.5f;

                damageable.TakeDamage(damage, knockbackSource);

                Debug.Log($"Caja {gameObject.name} hizo {damage} de daño con knockback a {collision.gameObject.name} (velocidad: {velocityMagnitude:F2})");
            }
            else
            {
                damageable.TakeDamage(damage);

                Debug.Log($"Caja {gameObject.name} hizo {damage} de daño a {collision.gameObject.name} (velocidad: {velocityMagnitude:F2})");
            }

            // Verificar si el enemigo murió (solo si NO es otra caja)
            bool enemyDied = wasAlive && damageable.IsDead();
            bool isEnemyNotBox = collision.gameObject.GetComponent<ThrowableBox>() == null;

            if (enemyDied && isEnemyNotBox)
            {
                Debug.Log($"⚡ ¡ENEMIGO {collision.gameObject.name} ELIMINADO! Activando Impact Frame");
                TriggerImpactFrameOnKill(collision.gameObject, velocityMagnitude);
            }

            // Marcar que causó daño
            hasDealtDamage = true;

            // Esto asegura que la caja vuelva a estar en estado "no capturada"
            if (box != null && box.IsCaptured())
            {
                box.Release(rb.linearVelocity);
                Debug.Log($"Caja liberada después de causar daño");
            }
            if (destroyOnFirstHit)
            {
                box.TakeDamage(999);
            }
            else
            {
                box.TakeDamage(1);
                ApplyBounceEffect(collision, velocityMagnitude);
            }

            // Aplicar rebote si no se destruyó inmediatamente
            if (!destroyOnFirstHit)
            {
                ApplyBounceEffect(collision, velocityMagnitude);
            }
        }
    }

    /// <summary>
    /// Dispara el efecto de Impact Frame cuando la caja mata a un enemigo
    /// </summary>
    void TriggerImpactFrameOnKill(GameObject enemy, float velocityMagnitude)
    {
        // Verificar condiciones
        if (!enableImpactFrame) return;
        if (impactManager == null) return;
        if (velocityMagnitude < minVelocityForImpactFrame)
        {
            Debug.Log($"Velocidad insuficiente para Impact Frame ({velocityMagnitude:F2} < {minVelocityForImpactFrame})");
            return;
        }

        // 🎬 DISPARAR IMPACT FRAME
        // Usamos la caja y el enemigo como los dos objetos
        impactManager.TriggerImpact(gameObject, enemy);

        Debug.Log($"✨ Impact Frame disparado: Caja vs {enemy.name} (velocidad: {velocityMagnitude:F2})");
    }

    void ApplyBounceEffect(Collision2D collision, float velocityMagnitude)
    {
        if (rb == null) return;

        Vector2 bounceDirection = (rb.position - collision.GetContact(0).point).normalized;
        float bounceForce = velocityMagnitude * 0.4f;
        rb.AddForce(bounceDirection * bounceForce, ForceMode2D.Impulse);
    }

    void OnDestroy()
    {
        Debug.Log($"ThrowableBoxCollisionDamage destruido en {gameObject.name}");
    }
}