using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable, IStunnable, ICaptureable
{
    [Header("Enemy Stats")]
    [SerializeField] protected Enemy enemyStats;

    [Header("Events")]
    public UnityEvent OnDamageTaken;
    public UnityEvent OnDeath;
    public UnityEvent OnCaptured;
    public UnityEvent OnCaptureStarted;
    public UnityEvent OnCaptureCanceled;
    public UnityEvent<float> OnStunnedChanged;
    public UnityEvent OnReleased;

    protected int currentHealth;
    public bool isDead = false;
    protected bool isCaptured = false;
    protected bool isBeingCaptured = false;

    private bool isKnockbackActive = false;
    private Vector2 knockbackDirection;
    private float knockbackTimer = 0f;
    private float knockbackStartDistance = 0f;

    protected float currentStunned = 0f; // 0-100
    protected float maxStunned = 100f;

    protected Rigidbody2D rb;
    protected Collider2D[] colliders;

    // Componentes que DisableAIComponents() apag� al capturar, para poder
    // reactivar exactamente esos (y no algo que ya estaba apagado por otra razón)
    private readonly List<MonoBehaviour> componentsDisabledByCapture = new List<MonoBehaviour>();

    protected DamageFlashEffect damageFlashEffect;
    protected virtual void Start()
    {
        if (enemyStats == null)
        {
            Debug.LogError($"No se asign� el ScriptableObject Enemy en {gameObject.name}");
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();

        damageFlashEffect = GetComponent<DamageFlashEffect>();
        if (damageFlashEffect == null)
        {
            damageFlashEffect = gameObject.AddComponent<DamageFlashEffect>();
        }

        InitializeHealth();
        InitializeEnemy(); // M�todo virtual para inicializaci�n espec�fica de cada enemigo
    }
    protected virtual void Update()
    {
        UpdateStunnedEffect();
        UpdateKnockback();
    }

    //////////////////////////////////// KNOCKBACK
    protected virtual void UpdateKnockback()
    {
        if (!isKnockbackActive || rb == null) return;

        knockbackTimer += Time.deltaTime;
        float progress = knockbackTimer / enemyStats.Knockback.KnockbackDuration;

        if (progress >= 1f)
        {
            // Knockback completado
            isKnockbackActive = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Aplicar knockback usando la curva de animación
        float curveValue = enemyStats.Knockback.KnockbackCurve.Evaluate(progress);
        float currentSpeed = (knockbackStartDistance / enemyStats.Knockback.KnockbackDuration) * curveValue;
        rb.linearVelocity = knockbackDirection * currentSpeed;
    }
    public virtual bool IsInKnockback()
    {
        return isKnockbackActive;
    }
    protected virtual void ApplyKnockback(int damageAmount, Vector2 damageSource)
    {
        if (!enemyStats.Knockback.CanBeKnockback || rb == null || isDead || isCaptured || isBeingCaptured) return;

        // Calcular el porcentaje de daño respecto a la vida máxima
        float damagePercentage = Mathf.Clamp01((float)damageAmount / enemyStats.MaxHealth);

        // Calcular la distancia de knockback basada en el porcentaje de daño
        knockbackStartDistance = damagePercentage * enemyStats.Knockback.MaxKnockbackDistance;

        // Calcular la dirección del knockback (desde la fuente del daño hacia el enemigo)
        Vector2 enemyPosition = transform.position;
        knockbackDirection = (enemyPosition - damageSource).normalized;

        // Iniciar el knockback
        isKnockbackActive = true;
        knockbackTimer = 0f;
    }
    public virtual void CancelKnockback()
    {
        if (isKnockbackActive && rb != null)
        {
            isKnockbackActive = false;
            rb.linearVelocity = Vector2.zero;
        }
    }

    //////////////////////////////////// CAPTURE

    public virtual bool StartCapture() // Inicia el proceso de captura
    {
        if (!CanBeCaptured())
        {
            return false;
        }

        CancelKnockback();

        isBeingCaptured = true;
        OnCaptureStarted?.Invoke();
        OnCaptureStartedCustom();

        return true;
    }
    public virtual bool Release(Vector2 releaseVelocity = default)
    {
        if (!isCaptured)
        {
            Debug.LogWarning($"{gameObject.name} - No se puede liberar un enemigo que no está capturado");
            return false;
        }

        isCaptured = false;
        isBeingCaptured = false;

        // Reactivar componentes de IA
        ReenableAIComponents();

        // Restaurar el Rigidbody2D a su estado normal si existe
        if (rb != null)
        {
            // Restaurar propiedades físicas normales
            rb.gravityScale = 1f;

            // Aplicar la velocidad de liberación si se proporcionó
            if (releaseVelocity != Vector2.zero)
            {
                rb.linearVelocity = releaseVelocity;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        // Limpiar el stunned al liberar
        ClearStunned();

        // Invocar eventos
        OnReleased?.Invoke();
        OnReleasedCustom();

        return true;
    }
    public virtual bool CompleteCapture() // Completa la captura del enemigo
    {
        if (!isBeingCaptured)
        {
            Debug.LogWarning($"{gameObject.name} - Intento de completar captura sin haberla iniciado");
            return false;
        }

        isCaptured = true;
        isBeingCaptured = false;

        FreezeEnemy();

        OnCaptured?.Invoke();
        OnCapturedCustom();

        return true;
    }
    protected virtual void FreezeEnemy()
    {
        CancelKnockback();

        // Detener el Rigidbody2D pero mantenerlo din�mico
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            // El Rigidbody2D se mantiene din�mico para f�sicas naturales
        }

        // Desactivar todos los scripts de IA y comportamiento
        DisableAIComponents();

        // Limpiar el stunned
        ClearStunned();
    }
    protected virtual void ReenableAIComponents()
    {
        foreach (MonoBehaviour component in componentsDisabledByCapture)
        {
            // El componente podr�a haber sido destruido mientras estaba capturado
            if (component == null) continue;

            component.enabled = true;
        }

        componentsDisabledByCapture.Clear();
    }
    protected virtual void DisableAIComponents()
    {
        componentsDisabledByCapture.Clear();

        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            // No desactivarse a s� mismo, ni algo que ya estaba desactivado por otra raz�n
            if (component == this || !component.enabled) continue;

            if (!IsProtectedFromCaptureDisable(component))
            {
                component.enabled = false;
                componentsDisabledByCapture.Add(component);
            }
        }
    }
    /// <summary>
    /// Punto de extensi�n para excluir componentes espec�ficos de ser apagados al capturar.
    /// Por defecto no hay excepciones (aparte de este mismo script): se agregan seg�n se necesiten.
    /// </summary>
    protected virtual bool IsProtectedFromCaptureDisable(MonoBehaviour component)
    {
        return false;
    }
    public virtual void CancelCapture() // Cancela el proceso de captura
    {
        if (isBeingCaptured)
        {
            isBeingCaptured = false;
            OnCaptureCanceled?.Invoke();
            OnCaptureCanceledCustom();
        }
    }
    public virtual float GetCaptureStartProgress() // Calcula el progreso inicial de captura basado en el stun actual,  M�s stun = barra empieza m�s llena
    {
        if (currentStunned <= 0)
            return 0f;

        // Convertir el stun actual en progreso de captura
        float stunPercentage = currentStunned / maxStunned;
        float initialProgress = stunPercentage * enemyStats.Capture.StunToCaptureProgressMultiplier;

        return Mathf.Clamp01(initialProgress);
    }
    public virtual bool IsBeingCaptured() // Verifica si est� siendo capturado actualmente
    {
        return isBeingCaptured;
    }
    public virtual bool IsCaptured() // Verifica si ya fue capturado
    {
        return isCaptured;
    }
    public virtual bool CanBeCaptured() // Verifica si el enemigo puede ser capturado
    {
        // No se puede capturar si est� muerto o ya capturado
        if (isDead || isCaptured)
            return false;

        // Si requiere stun m�nimo, verificar
        if (enemyStats.Capture.RequireMinimumStunToCapture && currentStunned < enemyStats.Capture.MinimumStunForCapture)
        {
            return false;
        }

        return true;
    }
    public virtual float GetCaptureDifficulty()
    {
        return enemyStats.Capture.CaptureDifficulty;
    }
    public virtual float GetCaptureSpeedMultiplier()
    {
        float baseMultiplier = 1f;

        // Bonus por stun
        float stunPercentage = currentStunned / maxStunned;
        float stunBonus = stunPercentage * 0.5f; // Hasta +50% de velocidad

        // Bonus por vida baja
        float healthPercentage = GetHealthPercentage();
        float healthBonus = 0f;
        if (healthPercentage < 0.45f)
        {
            healthBonus = 0.5f; // +5s0% si est� por debajo del 45% de vida
        }
        else if (healthPercentage < 0.7f)
        {
            healthBonus = 0.25f; // +15% si est� por debajo del 70% de vida
        }

        return baseMultiplier + stunBonus + healthBonus;
    }
    protected virtual void OnCaptureStartedCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos visuales, sonidos, etc.
    }
    protected virtual void OnCapturedCustom()
    {
        // Las clases hijas pueden sobrescribir para comportamiento al ser capturado
        // Por ejemplo: cambiar color, desactivar IA, etc.
    }
    protected virtual void OnCaptureCanceledCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos cuando se cancela
    }
    protected virtual void OnReleasedCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos visuales, sonidos, etc.
        // Por ejemplo: restaurar color original, animación de liberación, etc.
    }

    //////////////////////////////////// STUNNED

    protected virtual void UpdateStunnedEffect() // Actualiza el efecto de stunned cada frame
    {
        if (currentStunned > 0)
        {
            // Calcular la velocidad de reducci�n basada en el nivel actual
            // Mientras m�s alto sea el stunned, m�s lento se reduce
            float stunnedNormalized = currentStunned / maxStunned;
            float decaySlowdown = 1f - (stunnedNormalized * enemyStats.Stun.StunnedDecaySlowdownFactor);
            float actualDecayRate = enemyStats.Stun.StunnedDecayBaseRate * decaySlowdown;

            // Reducir el stunned
            currentStunned -= actualDecayRate * Time.deltaTime;
            currentStunned = Mathf.Clamp(currentStunned, 0f, maxStunned);

            OnStunnedChanged?.Invoke(currentStunned);
            OnStunnedChangedCustom(currentStunned);
        }
    }
    public virtual void AddStunned(float amount) // A�ade stunned al enemigo
    {
        if (isDead) return;

        float previousStunned = currentStunned;
        currentStunned += amount;
        currentStunned = Mathf.Clamp(currentStunned, 0f, maxStunned);

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedAddedCustom(amount, previousStunned, currentStunned);

        // Si alcanza el umbral m�ximo, detener completamente
        if (currentStunned >= enemyStats.Stun.StunnedThresholdForFullStop && previousStunned < enemyStats.Stun.StunnedThresholdForFullStop)
        {
            OnFullyStunned();
        }
    }
    public virtual void ReduceStunned(float amount) // Reduce el stunned del enemigo
    {
        if (currentStunned <= 0) return;

        float previousStunned = currentStunned;
        currentStunned -= amount;
        currentStunned = Mathf.Clamp(currentStunned, 0f, maxStunned);

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedReducedCustom(amount, previousStunned, currentStunned);
    }
    public virtual void SetStunned(float value) // Establece el stunned a un valor espec�fico
    {
        float previousStunned = currentStunned;
        currentStunned = Mathf.Clamp(value, 0f, maxStunned);

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedChangedCustom(currentStunned);
    }
    public virtual void ClearStunned() // Limpia completamente el efecto de stunned
    {
        if (currentStunned > 0)
        {
            currentStunned = 0f;
            OnStunnedChanged?.Invoke(currentStunned);
            OnStunnedClearedCustom();
        }
    }
    public virtual bool IsFullyStunned() // Verifica si el enemigo est� completamente aturdido (stunned >= threshold)
    {
        return currentStunned >= enemyStats.Stun.StunnedThresholdForFullStop;
    }
    public virtual float GetStunnedPercentage() // Obtiene el porcentaje actual de stunned(0-1)
    {
        return currentStunned / maxStunned;
    }
    public virtual bool IsStunned() // Verifica si el enemigo tiene alg�n nivel de stunned
    {
        return currentStunned > 0;
    }
    public virtual float GetCurrentStunned() // Obtiene el valor actual de stunned (0-100)
    {
        return currentStunned;
    }
    public virtual float GetMovementSpeedMultiplier()
    {
        if (currentStunned <= 0) return 1f;
        if (currentStunned >= enemyStats.Stun.StunnedThresholdForFullStop) return 0f;

        // Usar la curva de animaci�n para calcular el impacto
        float stunnedNormalized = currentStunned / maxStunned;
        float curveValue = enemyStats.Stun.StunnedMovementCurve.Evaluate(stunnedNormalized);
        float reduction = curveValue * enemyStats.Stun.StunnedMovementImpactMax;

        return 1f - reduction;
    } // Calcula el multiplicador de velocidad basado en el stunned actual
    protected virtual void OnStunnedChangedCustom(float stunnedValue)
    {
        // Las clases hijas pueden sobrescribir para efectos visuales, sonidos, etc.
    }
    protected virtual void OnStunnedAddedCustom(float amount, float previousValue, float newValue)
    {
        // Las clases hijas pueden sobrescribir para efectos cuando se a�ade stunned
    }
    protected virtual void OnStunnedReducedCustom(float amount, float previousValue, float newValue)
    {
        // Las clases hijas pueden sobrescribir para efectos cuando se reduce stunned
    }
    protected virtual void OnStunnedClearedCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos cuando se limpia stunned
    }
    protected virtual void OnFullyStunned()
    {
        // Las clases hijas pueden sobrescribir para efectos cuando alcanza el umbral m�ximo
    }

    ///////////////////////////////////////////////////

    protected virtual void InitializeEnemy()
    {
        // Las clases hijas pueden sobrescribir este m�todo
    }
    protected virtual void InitializeHealth()
    {
        currentHealth = enemyStats.MaxHealth;
    }
    public virtual void TakeDamage(int amount, Vector2 damageSourcePosition = default)
    {
        if (isDead) return;

        int actualDamage = Mathf.Min(amount, currentHealth);
        currentHealth -= actualDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, enemyStats.MaxHealth);

        if (damageFlashEffect != null && enemyStats.DamageFlashDuration > 0f)
        {
            damageFlashEffect.Flash(enemyStats.DamageFlashDuration);
        }

        if (damageSourcePosition != default)
        {
            ApplyKnockback(actualDamage, damageSourcePosition);
        }

        OnDamageTaken?.Invoke();
        OnDamageTakenCustom(actualDamage); // M�todo virtual para comportamiento espec�fico

        if (currentHealth <= 0)
        {
            Die();
        }
    }
    protected virtual void OnDamageTakenCustom(int damageAmount)
    {
        // Las clases hijas pueden sobrescribir este m�todo
    }
    public virtual void Heal(int amount)
    {
        if (isDead) return;

        int actualHeal = Mathf.Min(amount, enemyStats.MaxHealth - currentHealth);
        currentHealth += actualHeal;
        currentHealth = Mathf.Clamp(currentHealth, 0, enemyStats.MaxHealth);

        OnHealedCustom(actualHeal); // M�todo virtual para comportamiento espec�fico
    }
    protected virtual void OnHealedCustom(int healAmount)
    {
        // Las clases hijas pueden sobrescribir este m�todo
    }
    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;

        CancelKnockback();

        OnDeath?.Invoke();
        OnDeathCustom(); // M�todo virtual para comportamiento espec�fico de muerte
    }
    protected virtual void OnDeathCustom()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnEnemyKilled();
        }
    }


    // M�todos p�blicos para obtener informaci�n
    public bool IsDead() => isDead;
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => enemyStats != null ? enemyStats.MaxHealth : 0;
    public float GetHealthPercentage() => enemyStats != null ? (float)currentHealth / enemyStats.MaxHealth : 0f;
    public bool IsFullHealth() => currentHealth >= (enemyStats != null ? enemyStats.MaxHealth : 0);

    // M�todo para obtener las stats del enemigo (�til para las clases hijas)
    protected Enemy GetEnemyStats() => enemyStats;
}