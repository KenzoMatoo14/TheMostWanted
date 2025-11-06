using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Stats Reference")]
    [SerializeField] private ScriptableStats playerStatsData;

    [Header("Health Bar Reference")]
    [SerializeField] private HealthBar healthBar;

    private int currentHealth;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnDeath;
    public UnityEngine.Events.UnityEvent<int> OnHealthChanged;

    private bool isDead = false;

    void Start()
    {
        // Validar que tenemos el ScriptableObject
        if (playerStatsData == null)
        {
            Debug.LogError("PlayerStats: No se asignó ScriptableStats! Asigna el ScriptableObject en el inspector.");
            return;
        }

        // Inicializar la vida desde el ScriptableObject
        currentHealth = playerStatsData.maxHealth;

        // Auto-encontrar la HealthBar si no está asignada
        if (healthBar == null)
        {
            healthBar = FindObjectOfType<HealthBar>();
            if (healthBar == null)
            {
                Debug.LogWarning("PlayerStats: No se encontró ninguna HealthBar en la escena.");
            }
        }

        // Actualizar la barra de vida al inicio
        UpdateHealthBar();
    }

    #region IDamageable Implementation

    public void TakeDamage(int amount)
    {
        if (isDead) return; // No recibir más daño si ya está muerto

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0); // No bajar de 0

        Debug.Log($"Player recibió {amount} de daño. Vida actual: {currentHealth}/{playerStatsData.maxHealth}");

        UpdateHealthBar();

        // Invocar evento de cambio de vida
        OnHealthChanged?.Invoke(currentHealth);

        // Verificar si murió
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    public bool IsDead()
    {
        return isDead;
    }
    public int GetCurrentHealth()
    {
        return currentHealth;
    }
    public int GetMaxHealth()
    {
        return playerStatsData != null ? playerStatsData.maxHealth : 0;
    }
    public float GetHealthPercentage()
    {
        if (playerStatsData == null || playerStatsData.maxHealth <= 0)
            return 0f;

        return (float)currentHealth / playerStatsData.maxHealth;
    }

    #endregion

    #region Additional Methods

    /// <summary>
    /// Cura al jugador
    /// </summary>
    public void Heal(int amount)
    {
        if (isDead || playerStatsData == null) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, playerStatsData.maxHealth); // No superar el máximo

        Debug.Log($"Player curado {amount}. Vida actual: {currentHealth}/{playerStatsData.maxHealth}");

        // Actualizar la barra de vida
        UpdateHealthBar();

        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Restaura la vida al máximo
    /// </summary>
    public void FullHeal()
    {
        if (isDead || playerStatsData == null) return;

        currentHealth = playerStatsData.maxHealth;

        Debug.Log("Player completamente curado!");

        // Actualizar la barra de vida
        UpdateHealthBar();

        OnHealthChanged?.Invoke(currentHealth);
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player ha muerto!");

        UpdateHealthBar();

        // Invocar evento de muerte
        OnDeath?.Invoke();

        // Aquí puedes agregar más lógica de muerte:
        // - Desactivar controles
        // - Reproducir animación de muerte
        // - Mostrar pantalla de Game Over
        // - etc.
    }

    /// <summary>
    /// Revive al jugador (útil para respawn)
    /// </summary>
    public void Revive()
    {
        if (playerStatsData == null) return;

        isDead = false;
        currentHealth = playerStatsData.maxHealth;

        Debug.Log("Player revivido!");

        // Actualizar la barra de vida
        UpdateHealthBar();

        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Actualiza la HealthBar con los valores actuales
    /// </summary>
    private void UpdateHealthBar()
    {
        if (healthBar != null && playerStatsData != null)
        {
            healthBar.UpdateHealthBar(currentHealth, playerStatsData.maxHealth);
        }
    }

    /// <summary>
    /// Permite cambiar la referencia de la HealthBar en runtime
    /// </summary>
    public void SetHealthBar(HealthBar newHealthBar)
    {
        healthBar = newHealthBar;
        UpdateHealthBar();
    }

    #endregion
}