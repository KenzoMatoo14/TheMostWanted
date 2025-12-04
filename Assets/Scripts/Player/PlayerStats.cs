using UnityEngine;

public class PlayerStats : MonoBehaviour, IDamageable
{
    [Header("Stats Reference")]
    [SerializeField] private ScriptableStats playerStatsData;

    [Header("Health Bar Reference")]
    [SerializeField] private HealthBar healthBar;

    [Header("Knockback Settings")]
    [SerializeField] private bool canBeKnockback = true;
    [SerializeField] private float maxKnockbackDistance = 1.5f;
    [SerializeField] private float knockbackDuration = 0.2f;
    [SerializeField] private AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [Tooltip("Reducción del knockback para el jugador (0.5 = 50% del knockback normal)")]
    [SerializeField] private float playerKnockbackReduction = 0.5f;

    private int currentHealth;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent OnDeath;
    public UnityEngine.Events.UnityEvent<int> OnHealthChanged;

    private bool isDead = false;

    [Header("Scripts to Disable on Death")]
    [SerializeField] private MonoBehaviour[] scriptsToDisable;
    [SerializeField] private bool disableAllScriptsExceptThis = false;

    private Rigidbody2D rb;
    private bool isKnockbackActive = false;
    private Vector2 knockbackDirection;
    private float knockbackTimer = 0f;
    private float knockbackStartDistance = 0f;

    void Start()
    {
        // Validar que tenemos el ScriptableObject
        if (playerStatsData == null)
        {
            Debug.LogError("PlayerStats: No se asignó ScriptableStats! Asigna el ScriptableObject en el inspector.");
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        if (rb == null && canBeKnockback)
        {
            Debug.LogWarning("PlayerStats: No se encontró Rigidbody2D. El knockback no funcionará.");
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
    void Update()
    {
        UpdateKnockback();
    }

    #region Knockback

    private void UpdateKnockback()
    {
        if (!isKnockbackActive || rb == null) return;

        knockbackTimer += Time.deltaTime;
        float progress = knockbackTimer / knockbackDuration;

        if (progress >= 1f)
        {
            // Knockback completado
            isKnockbackActive = false;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y); // Mantener velocidad Y
            return;
        }

        // Aplicar knockback usando la curva de animación
        float curveValue = knockbackCurve.Evaluate(progress);
        float currentSpeed = (knockbackStartDistance / knockbackDuration) * curveValue;

        // Solo afectar el eje X para el jugador, mantener la velocidad Y (gravedad/salto)
        Vector2 knockbackVelocity = new Vector2(knockbackDirection.x * currentSpeed, rb.linearVelocity.y);
        rb.linearVelocity = knockbackVelocity;
    }

    private void ApplyKnockback(int damageAmount, Vector2 damageSource)
    {
        if (!canBeKnockback || rb == null || isDead) return;

        // Calcular el porcentaje de daño respecto a la vida máxima
        float damagePercentage = Mathf.Clamp01((float)damageAmount / playerStatsData.maxHealth);

        // Calcular la distancia de knockback basada en el porcentaje de daño
        // Aplicar reducción para el jugador
        knockbackStartDistance = damagePercentage * maxKnockbackDistance * playerKnockbackReduction;

        // Calcular la dirección del knockback (desde la fuente del daño hacia el jugador)
        Vector2 playerPosition = transform.position;
        knockbackDirection = (playerPosition - damageSource).normalized;

        // Iniciar el knockback
        isKnockbackActive = true;
        knockbackTimer = 0f;

        Debug.Log($"Player - Knockback aplicado: {knockbackStartDistance:F2} unidades. Daño: {damagePercentage * 100:F1}%");
    }

    public void CancelKnockback()
    {
        if (isKnockbackActive && rb != null)
        {
            isKnockbackActive = false;
            // No resetear la velocidad completamente para no interferir con el movimiento del jugador
        }
    }

    public bool IsKnockbackActive()
    {
        return isKnockbackActive;
    }

    #endregion

    #region IDamageable Implementation

    public void TakeDamage(int amount, Vector2 damageSourcePosition = default)
    {
        if (isDead) return; // No recibir más daño si ya está muerto

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0); // No bajar de 0

        Debug.Log($"Player recibió {amount} de daño. Vida actual: {currentHealth}/{playerStatsData.maxHealth}");

        if (damageSourcePosition != default)
        {
            ApplyKnockback(amount, damageSourcePosition);
        }

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

        CancelKnockback();

        UpdateHealthBar();

        DisablePlayerScripts();

        // Invocar evento de muerte
        OnDeath?.Invoke();

        // - Reproducir animación de muerte
        // - Mostrar pantalla de Game Over
    }
    /// <summary>
    /// Desactiva los scripts del jugador al morir
    /// </summary>
    private void DisablePlayerScripts()
    {
        if (disableAllScriptsExceptThis)
        {
            // Desactiva TODOS los scripts excepto PlayerStats
            MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in allScripts)
            {
                if (script != this && script.enabled)
                {
                    script.enabled = false;
                    Debug.Log($"Script desactivado: {script.GetType().Name}");
                }
            }
        }
        else if (scriptsToDisable != null && scriptsToDisable.Length > 0)
        {
            // Desactiva solo los scripts especificados en el inspector
            foreach (MonoBehaviour script in scriptsToDisable)
            {
                if (script != null && script.enabled)
                {
                    script.enabled = false;
                    Debug.Log($"Script desactivado: {script.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// Reactiva los scripts al revivir
    /// </summary>
    private void EnablePlayerScripts()
    {
        if (disableAllScriptsExceptThis)
        {
            MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in allScripts)
            {
                if (script != this && !script.enabled)
                {
                    script.enabled = true;
                    Debug.Log($"Script reactivado: {script.GetType().Name}");
                }
            }
        }
        else if (scriptsToDisable != null && scriptsToDisable.Length > 0)
        {
            foreach (MonoBehaviour script in scriptsToDisable)
            {
                if (script != null && !script.enabled)
                {
                    script.enabled = true;
                    Debug.Log($"Script reactivado: {script.GetType().Name}");
                }
            }
        }
    }

    /// <summary>
    /// Revive al jugador (útil para respawn)
    /// </summary>
    public void Revive()
    {
        if (playerStatsData == null) return;

        isDead = false;
        currentHealth = playerStatsData.maxHealth;

        CancelKnockback();

        // NUEVO: Reactivar los scripts del jugador
        EnablePlayerScripts();

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetBool("isDeath", false);
            animator.Play("Idle", 0, 0f); // Forzar la animación Idle inmediatamente
        }

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