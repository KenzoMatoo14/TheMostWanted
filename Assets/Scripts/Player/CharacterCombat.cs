using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.UI;

public class CharacterCombat : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerControls controls;

    [Header("Stats Reference")]
    [SerializeField] private ScriptableStats stats;

    [Header("Combat References")]
    public Transform attackPoint;
    public Transform whipTip;
    private bool canAttack = true;

    [Header("Whip Attack Area")]
    [SerializeField] private float whipCapsuleRadius = 0.5f; // Radio de la cápsula
    [SerializeField] private bool showAttackArea = true; // Para debug visual

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip whipSound;
    [SerializeField][Range(0f, 1f)] private float whipSoundVolume = 1f;

    [Header("Capture References")]
    private bool isCapturing = false;
    private float currentCaptureProgress = 0f;
    private ICaptureable targetCapturable;  // Interface universal para capturar
    private MonoBehaviour targetMB;          // MonoBehaviour del objeto capturado
    private EnemyBase targetEnemy;
    private Vector2 captureStartPosition;
    [SerializeField] private LineRenderer ropeRenderer;
    private bool hasEnemyCaptured = false;
    [SerializeField] private GrapplingHook hookScript;

    private CapturedEnemyController currentCapturedController;

    private ICaptureable lastHoveredCapturable;
    private ICaptureable currentHoveredCapturable;
    private EnemyBase lastAttackedEnemy;
    private Animator animator;

    [Header("Capture UI")]
    public GameObject captureUI; // Panel que contiene la barra
    public Slider captureProgressBar; // Barra de progreso

    private Camera mainCamera;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controls = new PlayerControls();
        mainCamera = Camera.main;
        animator = GetComponentInChildren<Animator>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        controls.Combat.HitWhip.performed += ctx => HandleHitWhipInput();

        controls.Combat.Capture.started += ctx => ToggleCapture();
        controls.Combat.Capture.canceled += ctx => OnCaptureButtonReleased();

        // Validar que las stats existan
        if (stats == null)
        {
            Debug.LogError("ScriptableStats no asignado en CharacterCombat!");
        }
    }
    void OnEnable()
    {
        controls.Combat.Enable();
        controls.Movement.Enable();
    }
    void OnDisable()
    {
        controls.Combat.Disable();
        controls.Movement.Disable();
    }
    void Update()
    {
        UpdateHoverDetection();

        if (isCapturing)
        {
            UpdateCapture();
        }

        // Actualizar la cuerda de captura si hay un enemigo capturado
        if (hasEnemyCaptured)
        {
            // PRIMERO: Verificar si el objeto fue destruido (GameObject null)
            if (targetMB == null)
            {
                Debug.Log("Objeto capturado fue destruido - liberando automáticamente");
                CleanupCaptureState();
            }
            // SEGUNDO: Si el objeto existe, verificar si murió (solo para IDamageable)
            else
            {
                IDamageable damageable = targetMB.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsDead())
                {
                    Debug.Log($"Objeto capturado {targetMB.name} ha muerto - liberando automáticamente");
                    ReleaseEnemyOnDeath();
                }
                else
                {
                    DrawCaptureRope(targetMB.transform.position);
                }
            }
        }

        if (hookScript != null)
        {
            hookScript.canUseHook = !hasEnemyCaptured;
        }
    }

    ///////////////////////// AUDIO
    void PlayWhipSound()
    {
        if (audioSource != null && whipSound != null)
        {
            audioSource.PlayOneShot(whipSound, whipSoundVolume);
        }
    }

    ///////////////////////// HOVER DETECTION
    void UpdateHoverDetection()
    {
        // Actualizar el último hovered si hay uno actual
        if (currentHoveredCapturable != null)
        {
            lastHoveredCapturable = currentHoveredCapturable;
        }

        // Resetear el current
        currentHoveredCapturable = null;

        // Verificar que hay mouse conectado
        if (UnityEngine.InputSystem.Mouse.current == null)
        {
            return;
        }

        // Obtener la posición del mouse en el mundo usando el nuevo Input System
        Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        mouseWorldPos.z = 0f;

        // Hacer un CircleCast desde el mouse hacia los lados para detectar enemigos
        Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorldPos, stats.HoverDetectionRadius, stats.EnemyLayers);

        // Buscar el ICaptureable más cercano al cursor
        float closestDistance = Mathf.Infinity;
        foreach (Collider2D hit in hits)
        {
            ICaptureable capturable = hit.GetComponent<ICaptureable>();
            if (capturable != null)
            {
                MonoBehaviour capturableMB = capturable as MonoBehaviour;
                if (capturableMB != null)
                {
                    float distance = Vector2.Distance(mouseWorldPos, capturableMB.transform.position);
                    if (distance < closestDistance)
                    {
                        currentHoveredCapturable = capturable;
                        closestDistance = distance;
                    }
                }
            }
        }

        // Debug visual para saber qué está detectando
        if (currentHoveredCapturable != null)
        {
            MonoBehaviour hoveredMB = currentHoveredCapturable as MonoBehaviour;
            if (hoveredMB != null)
            {
                //Debug.Log($"Hovering: {hoveredMB.name}");
            }
        }
    }
    Vector2 GetAimDirection()
    {
        // Si tienes un sistema de apuntado con mouse/joystick, úsalo aquí
        // Por ahora, usar la dirección hacia donde mira el sprite (escala X)
        float facingDirection = transform.localScale.x > 0 ? 1f : -1f;
        return new Vector2(facingDirection, 0f);
    }
    ICaptureable GetTargetCapturable()
    {
        // Prioridad 1: El que está siendo hovereado actualmente
        if (currentHoveredCapturable != null)
        {
            //Debug.Log($"Objetivo seleccionado: hovereado actualmente - {((MonoBehaviour)currentHoveredCapturable).name}");
            return currentHoveredCapturable;
        }

        // Prioridad 2: El último que fue hovereado
        if (lastHoveredCapturable != null)
        {
            // Verificar que sigue siendo válido y en rango
            MonoBehaviour lastHoveredMB = lastHoveredCapturable as MonoBehaviour;
            if (lastHoveredMB != null)
            {
                float distance = Vector2.Distance(transform.position, lastHoveredMB.transform.position);
                if (distance <= stats.CaptureRange)
                {
                    //Debug.Log($"Objetivo seleccionado: último hovereado - {lastHoveredMB.name}");
                    return lastHoveredCapturable;
                }
            }
        }

        // Prioridad 3: Buscar el más cercano
        //Debug.Log("Objetivo seleccionado: enemigo más cercano (fallback)");
        return null; // Retornar null para que se use FindNearestEnemy
    }

    ///////////////////////// CAPTURE

    void ToggleCapture()
    {
        if (hasEnemyCaptured)
        {
            // Si ya hay un enemigo capturado, liberarlo
            ReleaseEnemy();
        }
        else
        {
            // Si no hay enemigo capturado, iniciar captura
            StartCapture();
        }
    }
    void OnCaptureButtonReleased()
    {
        // Solo detener la captura si estamos en proceso de capturar (no si ya está capturado)
        if (isCapturing && !hasEnemyCaptured)
        {
            StopCapture();
        }
    }
    void StartCapture()
    {
        targetCapturable = null;
        targetMB = null;
        targetEnemy = null;

        // PRIORIDAD 1: CurrentHoveredCapturable
        if (currentHoveredCapturable != null)
        {
            targetCapturable = currentHoveredCapturable;
            targetMB = currentHoveredCapturable as MonoBehaviour;
            targetEnemy = currentHoveredCapturable as EnemyBase;
            if (targetEnemy != null)
            {
                string objectType = targetEnemy != null ? "Enemigo" : "Objeto";
                Debug.Log($"[CAPTURA] Objetivo: CurrentHovered - {targetMB.name} ({objectType})");
            }
        }

        // PRIORIDAD 2: Último enemigo atacado (si está en rango)
        if (targetCapturable == null && lastAttackedEnemy != null)
        {
            float distance = Vector2.Distance(transform.position, lastAttackedEnemy.transform.position);
            if (distance <= stats.CaptureRange && !lastAttackedEnemy.IsDead())
            {
                targetCapturable = lastAttackedEnemy;
                targetMB = lastAttackedEnemy;
                targetEnemy = lastAttackedEnemy;
                Debug.Log($"[CAPTURA] Objetivo: Último Atacado en rango - {targetEnemy.name} (distancia: {distance:F2}m)");
            }
            else
            {
                if (lastAttackedEnemy.IsDead())
                {
                    Debug.Log($"[CAPTURA] Último Atacado está muerto: {lastAttackedEnemy.name}");
                }
                else
                {
                    Debug.Log($"[CAPTURA] Último Atacado fuera de rango: {lastAttackedEnemy.name} (distancia: {distance:F2}m / max: {stats.CaptureRange}m)");
                }
            }
        }

        // PRIORIDAD 3: LastHoveredCapturable (si está en rango)
        if (targetCapturable == null && lastHoveredCapturable != null)
        {
            MonoBehaviour lastHoveredMB = lastHoveredCapturable as MonoBehaviour;
            if (lastHoveredMB != null)
            {
                float distance = Vector2.Distance(transform.position, lastHoveredMB.transform.position);
                if (distance <= stats.CaptureRange)
                {
                    targetCapturable = lastHoveredCapturable;
                    targetMB = lastHoveredMB;
                    targetEnemy = lastHoveredCapturable as EnemyBase;

                    string objectType = targetEnemy != null ? "Enemigo" : "Objeto";
                    Debug.Log($"[CAPTURA] Objetivo: LastHovered en rango - {targetMB.name} ({objectType}) (distancia: {distance:F2}m)");
                }
                else
                {
                    Debug.Log($"[CAPTURA] LastHovered fuera de rango: {lastHoveredMB.name} (distancia: {distance:F2}m / max: {stats.CaptureRange}m)");
                }
            }
        }

        // PRIORIDAD 4: FindNearestEnemy (fallback)
        if (targetCapturable == null)
        {
            Debug.Log("[CAPTURA] Usando FindNearestCapturable (fallback)");
            targetCapturable = FindNearestCapturable();
            if (targetCapturable != null)
            {
                targetMB = targetCapturable as MonoBehaviour;
                targetEnemy = targetCapturable as EnemyBase;
            }
        }

        if (targetCapturable != null && targetMB != null)
        {
            // Intentar iniciar captura en el enemigo
            if (!targetCapturable.StartCapture())
            {
                Debug.Log($"No se puede capturar a {targetMB.name} en este momento");
                return;
            }

            isCapturing = true;

            // IMPORTANTE: Inicializar con el progreso basado en el stun del enemigo
            float initialProgress = targetCapturable.GetCaptureStartProgress();
            currentCaptureProgress = initialProgress * stats.CaptureTime;

            captureStartPosition = transform.position;

            UpdateCaptureUI();

            string objectType = targetEnemy != null ? "Enemigo" : "Objeto";
            string stunInfo = targetEnemy != null ? $" | Stun: {targetEnemy.GetCurrentStunned():F1}%" : "";
            Debug.Log($"Iniciando captura de: {targetMB.name} ({objectType}) | Progreso inicial: {initialProgress * 100f:F1}%{stunInfo}");
        }
        else
        {
            Debug.Log("No hay enemigos en rango para capturar");
        }
    }
    void StopCapture()
    {
        isCapturing = false;
        currentCaptureProgress = 0f;

        if (!hasEnemyCaptured)
        {
            targetCapturable = null;
            targetMB = null;
            targetEnemy = null;
            HideCaptureRope();
        }

        Debug.Log("Captura cancelada");
    }
    void UpdateCapture()
    {
        if (targetCapturable == null || targetMB == null)
        {
            StopCapture();
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, targetMB.transform.position);
        if (distanceToTarget > stats.CaptureRange)
        {
            Debug.Log("Objeto fuera de rango - captura cancelada");
            StopCapture();
            return;
        }

        currentCaptureProgress += Time.deltaTime;

        UpdateCaptureUI();

        if (currentCaptureProgress >= stats.CaptureTime)
        {
            CompleteCapture();
        }
    }
    void UpdateCaptureUI()
    {
        if (captureProgressBar != null)
        {
            captureProgressBar.value = currentCaptureProgress / stats.CaptureTime;
        }
    }
    void CompleteCapture()
    {
        if (targetCapturable != null && targetMB != null)
        {
            Debug.Log($"¡Captura exitosa de: {targetMB.name}!");

            // Llamar al método TryCapture del enemigo
            bool captureSuccess = targetCapturable.CompleteCapture();

            if (captureSuccess)
            {
                Debug.Log("Captura confirmada por el enemigo");

                //Marcar que hay enemigo capturado
                hasEnemyCaptured = true;
                isCapturing = false;
                currentCaptureProgress = 0f;

                UpdateCaptureUI();

                currentCapturedController = targetMB.gameObject.AddComponent<CapturedEnemyController>();
                currentCapturedController.Initialize(transform, mainCamera, this);

                // Dibujar la cuerda hacia el enemigo capturado
                DrawCaptureRope(targetMB.transform.position);
            }
            else
            {
                Debug.Log("El enemigo resistió la captura");
                StopCapture();
            }
        }
        else
        {
            StopCapture();
        }
    }
    void DrawCaptureRope(Vector3 enemyPosition)
    {
        if (ropeRenderer == null) return;

        // Activar el renderer si no está activo
        if (!ropeRenderer.enabled)
        {
            ropeRenderer.enabled = true;
        }

        // Dibujar la cuerda desde el jugador hasta el enemigo
        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, transform.position); // Posición del jugador
        ropeRenderer.SetPosition(1, enemyPosition);      // Posición del enemigo capturado
    }
    void HideCaptureRope()
    {
        if (ropeRenderer != null)
        {
            ropeRenderer.enabled = false;
            ropeRenderer.positionCount = 0;
        }
    }
    EnemyBase FindNearestEnemy()
    {
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, stats.CaptureRange, stats.EnemyLayers);

        EnemyBase nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Collider2D enemyCollider in enemiesInRange)
        {
            EnemyBase enemy = enemyCollider.GetComponent<EnemyBase>();
            if (enemy != null)
            {
                float distance = Vector2.Distance(transform.position, enemy.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEnemy = enemy;
                }
            }
        }

        return nearestEnemy;
    }
    ICaptureable FindNearestCapturable()
    {
        Collider2D[] objectsInRange = Physics2D.OverlapCircleAll(transform.position, stats.CaptureRange, stats.EnemyLayers);

        ICaptureable nearestCapturable = null;
        float nearestDistance = Mathf.Infinity;

        foreach (Collider2D objCollider in objectsInRange)
        {
            ICaptureable capturable = objCollider.GetComponent<ICaptureable>();
            if (capturable != null)
            {
                MonoBehaviour capturableMB = capturable as MonoBehaviour;
                if (capturableMB != null)
                {
                    float distance = Vector2.Distance(transform.position, capturableMB.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestCapturable = capturable;
                    }
                }
            }
        }

        return nearestCapturable;
    }
    private void CleanupCaptureState()
    {
        // Destruir el controller si existe
        if (currentCapturedController != null)
        {
            Destroy(currentCapturedController);
            currentCapturedController = null;
        }

        // Limpiar referencias
        targetCapturable = null;
        targetMB = null;
        targetEnemy = null;
        hasEnemyCaptured = false;
        isCapturing = false;
        currentCaptureProgress = 0f;

        HideCaptureRope();

        Debug.Log("Estado de captura limpiado completamente");
    }

    ///////////////////////// RELEASE
    public void ReleaseEnemy()
    {
        if (targetCapturable != null && targetMB != null && hasEnemyCaptured)
        {
            Debug.Log($"Liberando a: {targetMB.name}");

            // Obtener la velocidad de liberación ANTES de destruir el controller
            Vector2 releaseVelocity = Vector2.zero;
            if (currentCapturedController != null)
            {
                releaseVelocity = currentCapturedController.GetReleaseVelocity();
                Debug.Log($"Velocidad de liberación obtenida: {releaseVelocity.magnitude:F2} m/s en dirección {releaseVelocity.normalized}");

                bool isBox = currentCapturedController.IsBox();

                if (isBox)
                {
                    // Para cajas: añadir ThrowableBoxCollisionDamage
                    ThrowableBoxCollisionDamage boxDamage = targetMB.gameObject.AddComponent<ThrowableBoxCollisionDamage>();
                    boxDamage.Initialize(
                        currentCapturedController.minVelocityForDamage,
                        currentCapturedController.damageMultiplier * 1.5f, // 50% más de daño que enemigos
                        currentCapturedController.damageableLayers,
                        true, // Destruir al primer impacto
                        5f    // Duración de 5 segundos
                    );
                    Debug.Log("ThrowableBoxCollisionDamage añadido a la caja");
                }
                else
                {
                    // Para enemigos: añadir ReleasedEnemyCollisionDamage
                    ReleasedEnemyCollisionDamage collisionDamage = targetMB.gameObject.AddComponent<ReleasedEnemyCollisionDamage>();
                    collisionDamage.Initialize(
                        currentCapturedController.minVelocityForDamage,
                        currentCapturedController.damageMultiplier,
                        currentCapturedController.damageableLayers,
                        currentCapturedController.damageCooldown,
                        3f // Duración de 3 segundos
                    );
                    Debug.Log("ReleasedEnemyCollisionDamage añadido al enemigo");
                }

                // Destruir el controller
                Destroy(currentCapturedController);
                currentCapturedController = null;
            }

            // Liberar el enemigo con la velocidad calculada
            targetCapturable.Release(releaseVelocity);
            targetCapturable = null;
            targetMB = null;
            targetEnemy = null;
            hasEnemyCaptured = false;
            HideCaptureRope();
        }
    }
    private void ReleaseEnemyOnDeath()
    {
        if (targetCapturable != null && targetMB != null && hasEnemyCaptured)
        {
            Debug.Log($"Objeto {targetMB.name} murió mientras estaba capturado - limpiando estado");

            // Desuscribirse del evento de muerte (si aún no se ha hecho)
            if (targetEnemy != null)
            {
                targetEnemy.OnDeath.RemoveListener(OnCapturedEnemyDied);
            }
        }

        // Usar el método centralizado para limpiar
        CleanupCaptureState();
    }
    private void OnCapturedEnemyDied()
    {
        Debug.Log("Evento OnDeath recibido del enemigo capturado");
        ReleaseEnemyOnDeath();
    }

    ///////////////////////// MELEE
    void HandleHitWhipInput()
    {
        if (hasEnemyCaptured)
        {
            // Si hay un enemigo capturado, liberarlo
            ReleaseEnemy();
        }
        else
        {
            // Si no hay enemigo capturado, realizar ataque normal
            TryMeleeAttack();
        }
    }
    void TryMeleeAttack()
    {
        if (!canAttack || hasEnemyCaptured) return;

        MeleeAttack();

        // Activar cooldown
        canAttack = false;
        Invoke(nameof(ResetAttackCooldown), stats.AttackCooldown);
    }
    void ResetAttackCooldown()
    {
        canAttack = true;
    }
    void MeleeAttack()
    {
        PlayWhipSound();

        // Activar animación de ataque
        if (animator != null)
        {
            animator.SetTrigger("isHiting");
        }
    }
    public void ExecuteWhipDamage()
    {
        Debug.Log("=== EJECUTANDO DAÑO DEL LÁTIGO (CÁPSULA) ===");

        // Calcular el punto medio entre attackPoint y whipTip
        Vector2 capsuleStart = attackPoint.position;
        Vector2 capsuleEnd = whipTip.position;

        // Calcular dirección y distancia
        Vector2 direction = (capsuleEnd - capsuleStart).normalized;
        float distance = Vector2.Distance(capsuleStart, capsuleEnd);

        // Usar CapsuleCast para detectar enemigos en el área de cápsula
        RaycastHit2D[] hitEnemies = Physics2D.CapsuleCastAll(
            capsuleStart,                    // Origen (punto de inicio)
            new Vector2(whipCapsuleRadius * 2f, distance), // Tamaño de la cápsula (ancho, alto)
            CapsuleDirection2D.Vertical,     // Dirección de la cápsula
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f, // Ángulo de rotación
            direction,                       // Dirección del cast (Vector2, no float)
            distance,                        // Distancia del cast
            stats.EnemyLayers                // Layers a detectar
        );

        bool hitSomething = false;

        foreach (RaycastHit2D hit in hitEnemies)
        {
            Debug.Log("Whip hit: " + hit.collider.gameObject.name);

            SpawnHitParticles(hit.point, hit.normal);

            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(stats.MeleeDamage, (Vector2)attackPoint.position);
                hitSomething = true;

                EnemyBase enemy = hit.collider.GetComponent<EnemyBase>();
                if (enemy != null)
                {
                    lastAttackedEnemy = enemy;

                    if (stats.ApplyStunOnHit)
                    {
                        ApplyStunToEnemy(enemy, stats.MeleeDamage);
                    }
                }
            }
        }

        if (hitSomething && stats.HitStopDuration > 0f)
        {
            HitStopManager.Instance.DoHitStop(stats.HitStopDuration);
        }
    }
    public void ExecuteWhipDamageFromChild()
    {
        // Buscar el CharacterCombat en el padre
        CharacterCombat combat = GetComponent<CharacterCombat>();
        if (combat != null)
        {
            combat.ExecuteWhipDamage();
        }
        else
        {
            Debug.LogError("No se encontró CharacterCombat en el padre!");
        }
    }

    ///////////////////////// STUNNED
    void ApplyStunToEnemy(EnemyBase enemy, int damageDealt)
    {
        // Calcular el porcentaje de daño que representa del HP máximo
        float maxHealth = enemy.GetMaxHealth();
        float damagePercentage = (damageDealt / maxHealth) * 100f;

        // Aplicar el multiplicador de stun
        float finalStunAmount = damagePercentage * stats.StunMultiplier;

        // Aplicar bonus por vida baja si está activado
        if (stats.ApplyLowHealthBonus)
        {
            float healthPercentage = enemy.GetHealthPercentage();
            if (healthPercentage < 0.5f)
            {
                finalStunAmount += stats.LowHealthStunBonus;
                Debug.Log($"Bonus de stun aplicado por vida baja: +{stats.LowHealthStunBonus}");
            }
        }

        enemy.AddStunned(finalStunAmount);

        Debug.Log($"Stun aplicado a {enemy.name}: {finalStunAmount:F1}% (Daño: {damageDealt}/{maxHealth} = {damagePercentage:F1}% x {stats.StunMultiplier})");
    }
    public void ApplyStunToTarget(EnemyBase enemy, float customStunAmount) // Método opcional para aplicar stun directamente a un enemigo específico
    {
        if (enemy != null && !enemy.IsDead())
        {
            enemy.AddStunned(customStunAmount);
            Debug.Log($"Stun personalizado aplicado a {enemy.name}: {customStunAmount} puntos");
        }
    }
    public void ApplyStunOnly() // Método para aplicar stun sin daño (para habilidades especiales)
    {
        RaycastHit2D[] hitEnemies = Physics2D.LinecastAll(attackPoint.position, whipTip.position, stats.EnemyLayers);

        foreach (RaycastHit2D Enemy in hitEnemies)
        {
            EnemyBase enemy = Enemy.collider.GetComponent<EnemyBase>();
            if (enemy != null && !enemy.IsDead())
            {
                ApplyStunToEnemy(enemy, stats.MeleeDamage);
                Debug.Log($"Solo stun aplicado a: {enemy.name}");
            }
        }
    }

    ///////////////////////// GIZMOS

    void SpawnHitParticles(Vector2 hitPosition, Vector2 hitNormal)
    {
        if (stats.HitParticleEffect != null)
        {
            // Instanciar el efecto de partículas
            GameObject particleInstance = Instantiate(stats.HitParticleEffect, hitPosition, Quaternion.identity);

            // Opcional: Rotar las partículas para que apunten en dirección opuesta al impacto
            if (hitNormal != Vector2.zero)
            {
                float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
                particleInstance.transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // Destruir el objeto después de que terminen las partículas
            ParticleSystem ps = particleInstance.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                Destroy(particleInstance, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(particleInstance, 2f); // Destruir después de 2 segundos si no hay ParticleSystem
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null || whipTip == null) return;

        // Línea central del látigo
        Gizmos.color = Color.red;
        Gizmos.DrawLine(attackPoint.position, whipTip.position);

        // Dibujar la cápsula de ataque si está activado showAttackArea
        if (showAttackArea)
        {
            Vector2 start = attackPoint.position;
            Vector2 end = whipTip.position;
            Vector2 direction = (end - start).normalized;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * whipCapsuleRadius;

            // Dibujar los bordes de la cápsula
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Rojo semi-transparente

            // Líneas laterales
            Gizmos.DrawLine(start + perpendicular, end + perpendicular);
            Gizmos.DrawLine(start - perpendicular, end - perpendicular);

            // Círculos en los extremos
            DrawGizmoCircle(start, whipCapsuleRadius, Color.red);
            DrawGizmoCircle(end, whipCapsuleRadius, Color.red);
        }

        // Puntos de referencia
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(attackPoint.position, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(whipTip.position, 0.1f);

        // Rango de captura
        Gizmos.color = Color.purple;
        Gizmos.DrawWireSphere(transform.position, stats.CaptureRange);

        // Área de detección del mouse
        if (mainCamera != null && Application.isPlaying && UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 mouseScreenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            mouseWorldPos.z = 0f;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mouseWorldPos, stats.HoverDetectionRadius);
        }

        // Enemigo hovereado actualmente
        if (currentHoveredCapturable != null)
        {
            MonoBehaviour hoveredMB = currentHoveredCapturable as MonoBehaviour;
            if (hoveredMB != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(hoveredMB.transform.position, 0.5f);
                Gizmos.DrawLine(transform.position, hoveredMB.transform.position);
            }
        }

        // Último enemigo hovereado
        if (lastHoveredCapturable != null && lastHoveredCapturable != currentHoveredCapturable)
        {
            MonoBehaviour lastHoveredMB = lastHoveredCapturable as MonoBehaviour;
            if (lastHoveredMB != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(lastHoveredMB.transform.position, 0.4f);
            }
        }
    }
    private void DrawGizmoCircle(Vector2 center, float radius, Color color)
    {
        Gizmos.color = color;
        int segments = 20;
        float angleStep = 360f / segments;
        Vector2 prevPoint = center + new Vector2(radius, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            Vector2 newPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}
