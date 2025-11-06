using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Script temporal que se añade a un enemigo liberado para que cause daño por colisión
/// basado en su velocidad durante un tiempo limitado después de ser liberado.
/// </summary>
public class ReleasedEnemyCollisionDamage : MonoBehaviour
{
    private Rigidbody2D rb;
    private EnemyBase enemyBase;

    [Header("Damage Settings")]
    [SerializeField] private float minVelocityForDamage = 2f;
    [SerializeField] private float damageMultiplier = 2f;
    [SerializeField] private LayerMask damageableLayers = 1 << 6;
    [SerializeField] private float damageCooldown = 0.3f;

    [Header("Duration Settings")]
    [SerializeField] private float activeDuration = 5f; // Tiempo que el script permanece activo
    [SerializeField] private float velocityThreshold = 1f; // Si la velocidad baja de esto, desactivar

    private float startTime;
    private float lastDamageTime;
    private HashSet<Collider2D> recentlyDamagedColliders = new HashSet<Collider2D>();

    public void Initialize(float minVel, float damageMult, LayerMask layers, float cooldown, float duration = 3f)
    {
        rb = GetComponent<Rigidbody2D>();
        enemyBase = GetComponent<EnemyBase>();

        if (rb == null)
        {
            Debug.LogError($"ReleasedEnemyCollisionDamage: No Rigidbody2D encontrado en {gameObject.name}");
            Destroy(this);
            return;
        }

        minVelocityForDamage = minVel;
        damageMultiplier = damageMult;
        damageableLayers = layers;
        damageCooldown = cooldown;
        activeDuration = duration;

        startTime = Time.time;

        Debug.Log($"ReleasedEnemyCollisionDamage inicializado en {gameObject.name} por {activeDuration}s");
    }

    void Update()
    {
        // Verificar si debe auto-destruirse
        float elapsedTime = Time.time - startTime;

        if (elapsedTime >= activeDuration)
        {
            Debug.Log($"{gameObject.name} - Tiempo de colisión expirado ({activeDuration}s)");
            Destroy(this);
            return;
        }

        // Si la velocidad es muy baja, también desactivar
        if (rb != null && rb.linearVelocity.magnitude < velocityThreshold)
        {
            Debug.Log($"{gameObject.name} - Velocidad muy baja ({rb.linearVelocity.magnitude:F2}), desactivando colisiones");
            Destroy(this);
            return;
        }

        CleanupDamagedColliders();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Verificar si el objeto está en la capa de daño
        if (((1 << collision.gameObject.layer) & damageableLayers) == 0)
            return;

        // Verificar velocidad
        float currentVelocity = rb.linearVelocity.magnitude;
        if (currentVelocity < minVelocityForDamage)
            return;

        // Verificar cooldown
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        // Verificar si ya dañamos este collider recientemente
        if (recentlyDamagedColliders.Contains(collision.collider))
            return;

        // Aplicar daño
        ApplyCollisionDamage(collision, currentVelocity);
    }
    void ApplyCollisionDamage(Collision2D collision, float velocityMagnitude)
    {
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable != null)
        {
            // Calcular daño basado en la velocidad
            float velocityRatio = velocityMagnitude / minVelocityForDamage;
            int damage = Mathf.RoundToInt(velocityRatio * damageMultiplier);
            damage = Mathf.Max(damage, 1); // Mínimo 1 de daño

            // Aplicar stun al enemigo golpeado si es posible
            EnemyBase enemyHit = damageable as EnemyBase;
            if (enemyHit != null)
            {
                enemyHit.AddStunned(damage);
            }

            // Aplicar daño al objetivo
            damageable.TakeDamage(damage);

            // El enemigo liberado también recibe daño por el impacto
            if (enemyBase != null)
            {
                enemyBase.TakeDamage(damage);
                enemyBase.AddStunned(damage);
            }

            Debug.Log($"{gameObject.name} (liberado) hizo {damage} de daño a {collision.gameObject.name} con velocidad {velocityMagnitude:F2}");

            // Registrar el daño
            lastDamageTime = Time.time;
            recentlyDamagedColliders.Add(collision.collider);

            // Efecto de rebote
            ApplyBounceEffect(collision, velocityMagnitude);
        }
    }
    void ApplyBounceEffect(Collision2D collision, float velocityMagnitude)
    {
        if (rb == null) return;

        // Aplicar un rebote al enemigo liberado
        Vector2 bounceDirection = (rb.position - collision.GetContact(0).point).normalized;
        float bounceForce = velocityMagnitude * 0.3f; // 30% de la velocidad actual
        rb.AddForce(bounceDirection * bounceForce, ForceMode2D.Impulse);
    }
    void CleanupDamagedColliders()
    {
        // Limpiar la lista de colliders dañados recientemente después del cooldown
        if (Time.time - lastDamageTime > damageCooldown)
        {
            recentlyDamagedColliders.Clear();
        }
    }
    void OnDestroy()
    {
        Debug.Log($"ReleasedEnemyCollisionDamage destruido en {gameObject.name}");
    }
}