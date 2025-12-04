using UnityEngine;

public class Spikes : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int damageAmount = 999;

    [Header("Optional Settings")]
    [SerializeField] private bool canDamageMultipleTimes = false;
    [SerializeField] private float damageCooldown = 1f;

    private float lastDamageTime;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Intentar obtener el componente IDamageable
        IDamageable damageable = collision.GetComponent<IDamageable>();

        if (damageable != null)
        {
            // Verificar si ya está muerto
            if (damageable.IsDead())
            {
                Debug.Log($"Spikes: {collision.name} ya está muerto, no se aplica daño.");
                return;
            }

            // Verificar cooldown si está habilitado el daño múltiple
            if (canDamageMultipleTimes)
            {
                if (Time.time - lastDamageTime < damageCooldown)
                {
                    return;
                }
                lastDamageTime = Time.time;
            }

            // Aplicar daño
            damageable.TakeDamage(damageAmount);
            Debug.Log($"Spikes causaron {damageAmount} de daño a {collision.name}");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Intentar obtener el componente IDamageable
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();

        if (damageable != null)
        {
            // Verificar si ya está muerto
            if (damageable.IsDead())
            {
                Debug.Log($"Spikes: {collision.gameObject.name} ya está muerto, no se aplica daño.");
                return;
            }

            // Verificar cooldown si está habilitado el daño múltiple
            if (canDamageMultipleTimes)
            {
                if (Time.time - lastDamageTime < damageCooldown)
                {
                    return;
                }
                lastDamageTime = Time.time;
            }

            // Aplicar daño
            damageable.TakeDamage(damageAmount);
            Debug.Log($"Spikes causaron {damageAmount} de daño a {collision.gameObject.name}");
        }
    }
}