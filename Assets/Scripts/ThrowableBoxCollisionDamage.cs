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
    [SerializeField] private LayerMask damageableLayers = 1 << 6;
    [SerializeField] private bool destroyOnFirstHit = true;

    [Header("Duration Settings")]
    [SerializeField] private float activeDuration = 5f;
    [SerializeField] private float velocityThreshold = 1f;

    private float startTime;
    private bool hasDealtDamage = false;

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

        Debug.Log($"ThrowableBoxCollisionDamage inicializado en {gameObject.name} por {activeDuration}s");
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

            // Aplicar stun si es un enemigo
            EnemyBase enemyHit = damageable as EnemyBase;
            if (enemyHit != null)
            {
                enemyHit.AddStunned(damage * 1.5f);
            }

            // Aplicar daño al objetivo
            damageable.TakeDamage(damage);

            Debug.Log($"Caja {gameObject.name} hizo {damage} de daño a {collision.gameObject.name} con velocidad {velocityMagnitude:F2}");

            // Marcar que causó daño
            hasDealtDamage = true;

            box.TakeDamage(999); // Destruir la caja

            // Aplicar rebote si no se destruyó
            if (!destroyOnFirstHit)
            {
                ApplyBounceEffect(collision, velocityMagnitude);
            }
        }
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