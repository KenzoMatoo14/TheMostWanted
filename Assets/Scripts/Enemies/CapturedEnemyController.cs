using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CapturedEnemyController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Camera mainCamera;
    private Transform playerTransform;
    private Mouse mouse;
    private CharacterCombat characterCombat;

    private IDamageable damageable; // Para cualquier objeto con vida
    private EnemyBase enemyBase;    // Solo para enemigos (opcional)
    private bool isEnemy;           // Flag para saber si es un enemigo

    [Header("Drag Settings")]
    [SerializeField] private float dragSpeed = 75;
    [SerializeField] private float maxDistanceFromPlayer = 10f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Velocity Tracking")]
    private Vector2 previousPosition;
    private Vector2 currentVelocity;
    private float velocityMagnitude;
    private Queue<Vector2> velocityHistory = new Queue<Vector2>();
    [SerializeField] private int velocityHistorySize = 10;

    [Header("Release Settings")]
    [SerializeField] private float releaseVelocityMultiplier = 3f; // Multiplicador de velocidad al liberar
    [SerializeField] private float minReleaseVelocity = 2f; // Velocidad mínima al liberar
    [SerializeField] private float maxReleaseVelocity = 75; // Velocidad máxima al liberar

    [Header("Damage Settings")]
    [SerializeField] public float minVelocityForDamage = 2f;
    [SerializeField] public float damageMultiplier = 2f;
    [SerializeField] public LayerMask damageableLayers = 1 << 6;
    [SerializeField] public float damageCooldown = 0.3f;
    private float lastDamageTime;
    private HashSet<Collider2D> recentlyDamagedColliders = new HashSet<Collider2D>();

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highVelocityColor = Color.red;
    [SerializeField] private float highVelocityThreshold = 6f;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    [Header("Collision Ignore")]
    [SerializeField] private bool ignorePlayerCollision = true;

    [Header("Physics Settings")]
    [SerializeField] private float drag = 2f;
    [SerializeField] private float mass = 1f;

    private Vector2 targetPosition;
    private Vector2 smoothVelocity;
    private bool isInitialized = false;
    private int previousHealth;

    private Collider2D playerCollider;
    private Collider2D[] objectColliders;

    public void Initialize(Transform player, Camera camera, CharacterCombat combat)
    {
        playerTransform = player;
        mainCamera = camera;
        characterCombat = combat;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        enemyBase = GetComponent<EnemyBase>();
        damageable = GetComponent<IDamageable>();
        isEnemy = enemyBase != null;

        mouse = Mouse.current;
        if (mouse == null)
        {
            Debug.LogError($"CapturedEnemyController: No se encontró Mouse en el Input System");
            enabled = false;
            return;
        }
        if (rb == null)
        {
            Debug.LogError($"CapturedEnemyController: No se encontr� Rigidbody2D en {gameObject.name}");
            enabled = false;
            return;
        }

        // Configurar el Rigidbody2D para el arrastre
        rb.gravityScale = 0f;
        rb.linearDamping = drag;
        rb.mass = mass;
        rb.constraints = RigidbodyConstraints2D.None;

        // Guardar color original
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        previousPosition = rb.position;
        targetPosition = rb.position;

        if (mainCamera != null)
        {
            Vector3 enemyScreenPosition = mainCamera.WorldToScreenPoint(rb.position);
            Mouse.current.WarpCursorPosition(enemyScreenPosition);
            Debug.Log($"Cursor movido a la posición del enemigo: {enemyScreenPosition}");
        }

        if (damageable != null)
        {
            previousHealth = damageable.GetCurrentHealth();
        }

        if (ignorePlayerCollision)
        {
            SetupCollisionIgnore();
        }

        isInitialized = true;

        string objectType = isEnemy ? "Enemigo" : "Objeto";
        Debug.Log($"CapturedEnemyController inicializado en {gameObject.name} ({objectType})");
    }

    void Update()
    {
        if (!isInitialized || mainCamera == null || playerTransform == null)
            return;

        UpdateTargetPosition();
        UpdateVelocity();
        UpdateVisualFeedback();
        CleanupDamagedColliders();
        CheckHealthChange();
    }

    void FixedUpdate()
    {
        if (!isInitialized)
            return;

        MoveTowardsTarget();
        EnforceDistanceLimit();
    }
    void SetupCollisionIgnore()
    {
        // Obtener el collider del jugador
        if (playerTransform != null)
        {
            playerCollider = playerTransform.GetComponent<Collider2D>();
            if (playerCollider == null)
            {
                Debug.LogWarning($"No se encontró Collider2D en el jugador");
            }
        }

        // Obtener todos los colliders del enemigo
        objectColliders = GetComponents<Collider2D>();

        // Ignorar colisiones entre el enemigo y el jugador
        if (playerCollider != null && objectColliders.Length > 0)
        {
            foreach (Collider2D objCollider in objectColliders)
            {
                Physics2D.IgnoreCollision(objCollider, playerCollider, true);
                Debug.Log($"Colisión ignorada entre {gameObject.name} y {playerTransform.name}");
            }
        }
    }
    void CheckHealthChange()
    {
        if (damageable == null || characterCombat == null)
            return;

        int currentHealth = damageable.GetCurrentHealth();

        // Si la vida disminuyó, el enemigo recibió daño
        if (currentHealth < previousHealth)
        {
            int damageTaken = previousHealth - currentHealth;
            Debug.Log($"{gameObject.name} recibió {damageTaken} de daño mientras estaba capturado - Liberando...");

            // Notificar al CharacterCombat para que libere al enemigo
            characterCombat.ReleaseEnemy();
        }

        previousHealth = currentHealth;
    }
    void UpdateTargetPosition()
    {
        if (mouse == null)
            return;

        // Obtener posición del mouse usando el nuevo Input System
        Vector2 mouseScreenPosition = mouse.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;
        targetPosition = mouseWorldPosition;
    }
    void MoveTowardsTarget()
    {
        Vector2 newPosition = Vector2.SmoothDamp(
            rb.position,
            targetPosition,
            ref smoothVelocity,
            smoothTime,
            dragSpeed
        );

        rb.MovePosition(newPosition);
    }
    void EnforceDistanceLimit()
    {
        if (playerTransform == null)
            return;

        float distanceToPlayer = Vector2.Distance(rb.position, playerTransform.position);

        if (distanceToPlayer > maxDistanceFromPlayer)
        {
            // El enemigo salió del rango, liberarlo automáticamente
            Debug.Log($"{gameObject.name} salió del rango máximo ({distanceToPlayer:F2} > {maxDistanceFromPlayer}) - Liberando automáticamente...");

            // Notificar al CharacterCombat para que libere al enemigo
            if (characterCombat != null)
            {
                characterCombat.ReleaseEnemy();
            }
        }
    }
    void UpdateVelocity()
    {
        // Calcular velocidad actual
        Vector2 frameVelocity = (rb.position - previousPosition) / Time.deltaTime;
        previousPosition = rb.position;

        // Agregar a historial para promediar
        velocityHistory.Enqueue(frameVelocity);
        if (velocityHistory.Count > velocityHistorySize)
        {
            velocityHistory.Dequeue();
        }

        // Calcular velocidad promedio
        Vector2 avgVelocity = Vector2.zero;
        foreach (Vector2 vel in velocityHistory)
        {
            avgVelocity += vel;
        }
        avgVelocity /= velocityHistory.Count;

        currentVelocity = avgVelocity;
        velocityMagnitude = currentVelocity.magnitude;
    }
    void UpdateVisualFeedback()
    {
        if (spriteRenderer == null)
            return;

        // Cambiar color basado en la velocidad
        if (velocityMagnitude >= highVelocityThreshold)
        {
            float t = Mathf.InverseLerp(highVelocityThreshold, highVelocityThreshold * 1.5f, velocityMagnitude);
            spriteRenderer.color = Color.Lerp(originalColor, highVelocityColor, t);
        }
        else
        {
            spriteRenderer.color = Color.Lerp(spriteRenderer.color, originalColor, Time.deltaTime * 5f);
        }
    }
    void CleanupDamagedColliders()
    {
        // Limpiar la lista de colliders da�ados recientemente despu�s del cooldown
        if (Time.time - lastDamageTime > damageCooldown)
        {
            recentlyDamagedColliders.Clear();
        }
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isInitialized)
            return;

        // Verificar si el objeto est� en la capa de da�o
        if (((1 << collision.gameObject.layer) & damageableLayers) == 0)
            return;

        // Verificar si la velocidad es suficiente para hacer da�o
        if (velocityMagnitude < minVelocityForDamage)
            return;

        // Verificar cooldown
        if (Time.time - lastDamageTime < damageCooldown)
            return;

        // Verificar si ya da�amos este collider recientemente
        if (recentlyDamagedColliders.Contains(collision.collider))
            return;

        // Aplicar da�o
        ApplyCollisionDamage(collision);
    }
    void ApplyCollisionDamage(Collision2D collision)
    {
        IDamageable target = collision.gameObject.GetComponent<IDamageable>();
        if (target != null)
        {
            // Calcular da�o basado en la velocidad
            float velocityRatio = velocityMagnitude / minVelocityForDamage;
            int damage = Mathf.RoundToInt(velocityRatio * damageMultiplier);
            damage = Mathf.Max(damage, 1); // Mínimo 1 de daño

            EnemyBase targetEnemy = target as EnemyBase;
            if (targetEnemy != null)
            {
                targetEnemy.AddStunned(1.5f * damage);
            }

            target.TakeDamage(damage);

            if (isEnemy && enemyBase != null)
            {
                enemyBase.TakeDamage(damage);
                enemyBase.AddStunned(1.5f * damage);
            }
            // Las cajas y otros objetos NO reciben daño mientras están capturados

            Debug.Log($"{gameObject.name} hizo {damage} de da�o a {collision.gameObject.name} con velocidad {velocityMagnitude:F2}");

            // Registrar el daño
            lastDamageTime = Time.time;
            recentlyDamagedColliders.Add(collision.collider);

            // Efecto de rebote
            ApplyBounceEffect(collision);
        }
    }
    void ApplyBounceEffect(Collision2D collision)
    {
        // Aplicar un peque�o rebote al enemigo capturado
        Vector2 bounceDirection = (rb.position - collision.GetContact(0).point).normalized;
        float bounceForce = velocityMagnitude * 0.3f; // 30% de la velocidad actual
        rb.AddForce(bounceDirection * bounceForce, ForceMode2D.Impulse);
    }
    void OnDisable()
    {
        // Restaurar colisiones con el jugador
        if (playerCollider != null && objectColliders != null && objectColliders.Length > 0)
        {
            foreach (Collider2D objCollider in objectColliders)
            {
                if (objCollider != null)
                {
                    Physics2D.IgnoreCollision(objCollider, playerCollider, false);
                    Debug.Log($"Colisión restaurada entre {gameObject.name} y {playerTransform?.name}");
                }
            }
        }

        // Restaurar valores originales del Rigidbody2D
        if (rb != null)
        {
            rb.gravityScale = 1f;
            rb.linearDamping = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.constraints = RigidbodyConstraints2D.None; // O el valor que tenías originalmente
        }

        // Restaurar color original del sprite
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Limpiar referencias y estado
        isInitialized = false;
        velocityHistory.Clear();
        recentlyDamagedColliders.Clear();

        Debug.Log($"CapturedEnemyController desactivado en {gameObject.name}");
    }
    public Vector2 GetReleaseVelocity()
    {
        // Si la velocidad es muy baja, usar la velocidad mínima en la dirección del movimiento
        if (velocityMagnitude < minReleaseVelocity)
        {
            // Si no hay dirección clara, usar dirección hacia adelante del sprite
            Vector2 direction = currentVelocity.normalized;
            if (direction == Vector2.zero)
            {
                direction = transform.right; // O cualquier dirección por defecto
            }
            return direction * minReleaseVelocity;
        }

        // Aplicar el multiplicador y limitar la velocidad máxima
        Vector2 releaseVelocity = currentVelocity * releaseVelocityMultiplier;
        float clampedMagnitude = Mathf.Clamp(releaseVelocity.magnitude, minReleaseVelocity, maxReleaseVelocity);

        return releaseVelocity.normalized * clampedMagnitude;
    }

    // Métodos públicos para obtener información
    public float GetCurrentVelocity() => velocityMagnitude;
    public Vector2 GetVelocityVector() => currentVelocity;
    public bool IsMovingFastEnoughForDamage() => velocityMagnitude >= minVelocityForDamage;
    public bool IsEnemy() => isEnemy;
    public bool IsBox() => !isEnemy;

    // Método para ajustar settings en runtime si es necesario
    public void SetDragSpeed(float speed) => dragSpeed = speed;
    public void SetMaxDistance(float distance) => maxDistanceFromPlayer = distance;
    public void SetDamageMultiplier(float multiplier) => damageMultiplier = multiplier;

    // Debug
    void OnDrawGizmos()
    {
        if (!isInitialized || playerTransform == null)
            return;

        // Dibujar el l�mite de distancia m�xima
        Gizmos.color = Color.blueViolet;
        Gizmos.DrawWireSphere(playerTransform.position, maxDistanceFromPlayer);

        // Dibujar la velocidad actual
        Gizmos.color = velocityMagnitude >= minVelocityForDamage ? Color.red : Color.green;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)currentVelocity);

        // Dibujar target position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, 0.3f);
    }
}