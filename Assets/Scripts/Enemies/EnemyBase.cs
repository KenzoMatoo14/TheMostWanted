using UnityEngine.Events;
using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IDamageable, IStunnable, ICaptureable
{
    [Header("Enemy Stats")] 
    [SerializeField] protected Enemy enemyStats;

    [Header("UI Reference")]
    [SerializeField] protected HealthBar healthBar;

    [Header("Knockback Settings")]
    [SerializeField] protected bool canBeKnockback = true;
    [SerializeField] protected float maxKnockbackDistance = 2.33f;
    [SerializeField] protected float knockbackDuration = 0.2f; // Duración del knockback en segundos
    [SerializeField] protected AnimationCurve knockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Capture Settings")]
    [SerializeField] protected float captureDifficulty = 0.5f; // 0.0 = imposible, 1.0 = muy f�cil
    [SerializeField] protected Color capturedColor = Color.green;
    [SerializeField] protected float stunToCaptureProgressMultiplier = 0.8f; // 80% del stun se convierte en progreso inicial
    [SerializeField] protected bool requireMinimumStunToCapture = false;
    [SerializeField] protected float minimumStunForCapture = 30f; // Stun m�nimo requerido para capturar

    [Header("Stunned Settings")]
    [SerializeField] protected float stunnedDecayBaseRate = 10f; // Velocidad base de reducci�n por segundo
    [SerializeField] protected float stunnedDecaySlowdownFactor = 0.5f; // Factor que ralentiza la reducci�n seg�n el nivel
    [SerializeField] protected float stunnedMovementImpactMax = 0.9f; // Reducci�n m�xima de velocidad (90%)
    [SerializeField] protected AnimationCurve stunnedMovementCurve = AnimationCurve.Linear(0, 0, 1, 1); // Curva de impacto
    [SerializeField] protected float stunnedThresholdForFullStop = 95f; // A partir de este % el enemigo se detiene completamente

    [Header("Events")]
    public UnityEvent OnDamageTaken;
    public UnityEvent OnDeath;
    public UnityEvent OnCaptured;
    public UnityEvent OnCaptureStarted;
    public UnityEvent OnCaptureCanceled;
    public UnityEvent<float> OnStunnedChanged;
    public UnityEvent OnReleased;

    protected int currentHealth;
    protected bool isDead = false;
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
    protected MonoBehaviour[] aiComponents;
    protected virtual void Start()
    {
        // Configurar referencias autom�ticamente si no est�n asignadas
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<HealthBar>();
        }

        if (enemyStats == null)
        {
            Debug.LogError($"No se asign� el ScriptableObject Enemy en {gameObject.name}");
            return;
        }

        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();

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
        float progress = knockbackTimer / knockbackDuration;

        if (progress >= 1f)
        {
            // Knockback completado
            isKnockbackActive = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Aplicar knockback usando la curva de animación
        float curveValue = knockbackCurve.Evaluate(progress);
        float currentSpeed = (knockbackStartDistance / knockbackDuration) * curveValue;
        rb.linearVelocity = knockbackDirection * currentSpeed;
    }
    public virtual bool IsInKnockback()
    {
        return isKnockbackActive;
    }
    protected virtual void ApplyKnockback(int damageAmount, Vector2 damageSource)
    {
        if (!canBeKnockback || rb == null || isDead) return;

        // Calcular el porcentaje de daño respecto a la vida máxima
        float damagePercentage = Mathf.Clamp01((float)damageAmount / enemyStats.MaxHealth);

        // Calcular la distancia de knockback basada en el porcentaje de daño
        knockbackStartDistance = damagePercentage * maxKnockbackDistance;

        // Calcular la dirección del knockback (desde la fuente del daño hacia el enemigo)
        Vector2 enemyPosition = transform.position;
        knockbackDirection = (enemyPosition - damageSource).normalized;

        // Iniciar el knockback
        isKnockbackActive = true;
        knockbackTimer = 0f;

        Debug.Log($"{gameObject.name} - Knockback aplicado: {knockbackStartDistance:F2} unidades. Daño: {damagePercentage * 100:F1}%");
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
            Debug.Log($"{gameObject.name} no puede ser capturado en este momento");
            return false;
        }

        CancelKnockback();

        isBeingCaptured = true;
        OnCaptureStarted?.Invoke();
        OnCaptureStartedCustom();

        Debug.Log($"{gameObject.name} - Captura iniciada. Progreso inicial: {GetCaptureStartProgress() * 100f:F1}%");
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

        Debug.Log($"{gameObject.name} - Enemigo liberado");

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
                Debug.Log($"{gameObject.name} - Velocidad aplicada: {releaseVelocity.magnitude:F2} m/s");
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

        Debug.Log($"�{gameObject.name} capturado exitosamente!");

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

        Debug.Log($"{gameObject.name} - Enemigo congelado: comportamientos desactivados");
    }
    protected virtual void ReenableAIComponents()
    {
        /*
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            // Reactivar componentes que fueron desactivados durante la captura
            if (component != this && !component.enabled)
            {
                // No reactivar componentes de renderizado/físicas básicas
                if (!(component is SpriteRenderer) &&
                    !(component is Animator) &&
                    !(component is Rigidbody2D) &&
                    !(component is Collider2D))
                {
                    component.enabled = true;
                    Debug.Log($"{gameObject.name} - Componente reactivado: {component.GetType().Name}");
                }
            }
        }
        */
    }
    protected virtual void DisableAIComponents()
    {
        /*
        // Desactivar todos los MonoBehaviour excepto este script base
        MonoBehaviour[] components = GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour component in components)
        {
            // No desactivar este script base ni componentes esenciales
            if (component != this && component.enabled)
            {
                // Puedes agregar excepciones espec�ficas si es necesario
                // Por ejemplo, no desactivar el SpriteRenderer, Animator, etc.
                if (!(component is SpriteRenderer) &&
                    !(component is Animator) &&
                    !(component is Rigidbody2D) &&
                    !(component is Collider2D))
                {
                    component.enabled = false;
                    Debug.Log($"{gameObject.name} - Componente desactivado: {component.GetType().Name}");
                }
            }
        }
        */
    }
    public virtual void CancelCapture() // Cancela el proceso de captura
    {
        if (isBeingCaptured)
        {
            isBeingCaptured = false;
            OnCaptureCanceled?.Invoke();
            OnCaptureCanceledCustom();
            Debug.Log($"{gameObject.name} - Captura cancelada");
        }
    }
    public virtual float GetCaptureStartProgress() // Calcula el progreso inicial de captura basado en el stun actual,  M�s stun = barra empieza m�s llena
    {
        if (currentStunned <= 0)
            return 0f;

        // Convertir el stun actual en progreso de captura
        float stunPercentage = currentStunned / maxStunned;
        float initialProgress = stunPercentage * stunToCaptureProgressMultiplier;

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
        if (requireMinimumStunToCapture && currentStunned < minimumStunForCapture)
        {
            Debug.Log($"{gameObject.name} - Stun insuficiente para captura ({currentStunned:F1}/{minimumStunForCapture})");
            return false;
        }

        return true;
    }
    public virtual float GetCaptureDifficulty()
    {
        return captureDifficulty;
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
            float decaySlowdown = 1f - (stunnedNormalized * stunnedDecaySlowdownFactor);
            float actualDecayRate = stunnedDecayBaseRate * decaySlowdown;

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

        Debug.Log($"{gameObject.name} - Stunned a�adido: {amount}. Nivel actual: {currentStunned:F1}%");

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedAddedCustom(amount, previousStunned, currentStunned);

        // Si alcanza el umbral m�ximo, detener completamente
        if (currentStunned >= stunnedThresholdForFullStop && previousStunned < stunnedThresholdForFullStop)
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

        Debug.Log($"{gameObject.name} - Stunned reducido: {amount}. Nivel actual: {currentStunned:F1}%");

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedReducedCustom(amount, previousStunned, currentStunned);
    }
    public virtual void SetStunned(float value) // Establece el stunned a un valor espec�fico
    {
        float previousStunned = currentStunned;
        currentStunned = Mathf.Clamp(value, 0f, maxStunned);

        Debug.Log($"{gameObject.name} - Stunned establecido: {currentStunned:F1}%");

        OnStunnedChanged?.Invoke(currentStunned);
        OnStunnedChangedCustom(currentStunned);
    }
    public virtual void ClearStunned() // Limpia completamente el efecto de stunned
    {
        if (currentStunned > 0)
        {
            currentStunned = 0f;
            Debug.Log($"{gameObject.name} - Stunned limpiado");
            OnStunnedChanged?.Invoke(currentStunned);
            OnStunnedClearedCustom();
        }
    }
    public virtual bool IsFullyStunned() // Verifica si el enemigo est� completamente aturdido (stunned >= threshold)
    {
        return currentStunned >= stunnedThresholdForFullStop;
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
        if (currentStunned >= stunnedThresholdForFullStop) return 0f;

        // Usar la curva de animaci�n para calcular el impacto
        float stunnedNormalized = currentStunned / maxStunned;
        float curveValue = stunnedMovementCurve.Evaluate(stunnedNormalized);
        float reduction = curveValue * stunnedMovementImpactMax;

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
        Debug.Log($"{gameObject.name} - �Completamente aturdido!");
    }

    ///////////////////////////////////////////////////

    protected virtual void InitializeEnemy()
    {
        // Las clases hijas pueden sobrescribir este m�todo
    }
    protected virtual void InitializeHealth()
    {
        currentHealth = enemyStats.MaxHealth;

        // Actualizar la barra de vida
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, enemyStats.MaxHealth);
        }
    }
    public virtual void TakeDamage(int amount, Vector2 damageSourcePosition = default)
    {
        if (isDead) return;

        int actualDamage = Mathf.Min(amount, currentHealth);
        currentHealth -= actualDamage;
        currentHealth = Mathf.Clamp(currentHealth, 0, enemyStats.MaxHealth);

        Debug.Log($"{gameObject.name} - Da�o recibido: {actualDamage}. Vida actual: {currentHealth}/{enemyStats.MaxHealth}");

        if (damageSourcePosition != default)
        {
            ApplyKnockback(actualDamage, damageSourcePosition);
        }

        // Actualizar UI
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, enemyStats.MaxHealth);
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

        Debug.Log($"{gameObject.name} - Curaci�n recibida: {actualHeal}. Vida actual: {currentHealth}/{enemyStats.MaxHealth}");

        // Actualizar UI
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, enemyStats.MaxHealth);
        }

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

        Debug.Log($"{gameObject.name} muerto");

        OnDeath?.Invoke();
        OnDeathCustom(); // M�todo virtual para comportamiento espec�fico de muerte
    }
    protected virtual void OnDeathCustom()
    {
        // Las clases hijas pueden sobrescribir este m�todo
        // Por ejemplo: animaciones de muerte, drop de items, etc.
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