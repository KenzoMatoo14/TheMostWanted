using UnityEngine;
using UnityEngine.UI;

public class EnemyEvilBat : EnemyBase
{
    [Header("Evil Bat Specific Settings")]
    [SerializeField] private bool logBehaviorDetails = true;

    [Header("Random Patrol Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolWaitTime = 2f; // Tiempo de espera en cada punto
    [SerializeField] private float patrolRadius = 5f; // Radio desde la posición inicial
    [SerializeField] private float waypointReachDistance = 0.2f; // Distancia para considerar que llegó al punto
    [SerializeField] private bool useStartPositionAsCenter = true; // Usar posición inicial como centro
    [SerializeField] private Transform customPatrolCenter; // Centro personalizado (opcional)
    [SerializeField] private bool validatePatrolPath = true;
    [SerializeField] private int maxPathAttempts = 5;
    [SerializeField] private float pathValidationRayCount = 8; // Número de rayos para validar el camino
    [SerializeField] private LayerMask pathObstacleLayer; // Layer de obstáculos para pathfinding

    [Header("Wing Flap Movement Settings")]
    [SerializeField] private float flapAmplitude = 0.3f; // Amplitud del aleteo (qué tan alto/bajo)
    [SerializeField] private float flapFrequency = 3f; // Frecuencia del aleteo (qué tan rápido)
    [SerializeField] private float horizontalWaveAmplitude = 0.2f; // Ondulación horizontal
    [SerializeField] private float horizontalWaveFrequency = 2f;
    [SerializeField] private bool usePerlinNoise = true; // Usar ruido Perlin para movimiento más orgánico
    [SerializeField] private float perlinNoiseSpeed = 1f;

    [Header("Idle/Waiting Hover Settings")]
    [SerializeField] private float idleHoverAmplitude = 0.15f; // Movimiento vertical cuando espera
    [SerializeField] private float idleHoverFrequency = 1.5f;
    [SerializeField] private float idleHorizontalDrift = 0.08f; // Deriva horizontal sutil
    [SerializeField] private float idleDriftFrequency = 0.8f;

    [Header("Chase Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float loseTargetDistance = 8f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Line of Sight Settings")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private float visionCheckInterval = 0.2f;
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private bool debugLineOfSight = true;
    [SerializeField] private float loseLineOfSightDelay = 0.5f;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackWindupTime = 0.3f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 1f;

    [Header("Visual Feedback")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool autoFlipSprite = true;
    [SerializeField] private Color chaseColor = Color.red;
    [SerializeField] private Color patrolColor = Color.white;

    [Header("Stun Bar UI")]
    [SerializeField] private Slider stunBar;
    [SerializeField] private bool autoFindStunBar = true;
    [SerializeField] private Image stunBarFillImage;
    [SerializeField] private Gradient stunBarGradient;
    [SerializeField] private bool hideWhenZero = true;

    [Header("Animation Controller")]
    [SerializeField] private BatAnimationController animationController;

    [Header("Death Settings")]
    [SerializeField] private float deathGravityScale = 15f; // Gravedad al morir
    [SerializeField] private float deathFallSpeed = 20f; // Velocidad de caída adicional
    [SerializeField] private float deathDestroyDelay = 3f; // Tiempo antes de destruir
    private float originalGravityScale = 0f; // Guardar gravedad original

    // Estado interno
    private enum BatState { Patrol, Chase, Attack, Waiting }
    private BatState currentState = BatState.Patrol;

    private Vector2 patrolCenter; // Centro de la patrulla
    private Vector2 currentPatrolTarget; // Punto actual al que se dirige
    private float waitTimer = 0f;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private float attackWindupTimer = 0f;
    private float visionCheckTimer = 0f;
    private bool hasLineOfSight = false;
    private float timeWithoutLineOfSight = 0f;

    private Transform player;
    private Vector2 lastMoveDirection = Vector2.right;

    private float flapTimer = 0f;
    private float horizontalWaveTimer = 0f;
    private float perlinNoiseOffset;
    private Vector3 waitingStartPosition;


    protected override void Start()
    {
        base.Start();

        // Auto-encontrar referencias
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (attackPoint == null)
        {
            attackPoint = transform;
        }

        if (visionOrigin == null)
        {
            visionOrigin = transform;
        }
        if (animationController == null)
        {
            animationController = GetComponent<BatAnimationController>();
        }
        if (rb != null)
        {
            originalGravityScale = rb.gravityScale;
        }

        FindPlayer();

        // Establecer centro de patrulla
        if (useStartPositionAsCenter || customPatrolCenter == null)
        {
            patrolCenter = transform.position;
        }
        else
        {
            patrolCenter = customPatrolCenter.position;
        }

        perlinNoiseOffset = Random.Range(0f, 1000f);

        // Generar primer punto de patrulla
        GenerateNewPatrolPoint();
        InitializeStunBar();

        if (logBehaviorDetails)
        {
            Debug.Log($"Evil Bat {gameObject.name} inicializado. Centro de patrulla: {patrolCenter}");
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDead || isCaptured)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        if (IsFullyStunned())
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        if (IsInKnockback())
        {
            return;
        }

        UpdateBehavior();
        UpdateAttackTimer();
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                if (logBehaviorDetails)
                {
                    Debug.Log($"{gameObject.name} encontró al jugador: {player.name}");
                }
            }
        }
    }

    #region Line of Sight

    private bool CheckLineOfSight()
    {
        if (!requireLineOfSight || player == null)
        {
            return true;
        }

        Vector2 origin = visionOrigin.position;
        Vector2 targetPosition = player.position;
        Vector2 direction = targetPosition - origin;
        float distance = direction.magnitude;

        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, distance, obstacleLayer | playerLayer);

        if (debugLineOfSight)
        {
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                Debug.DrawRay(origin, direction, Color.green);
            }
            else
            {
                Debug.DrawRay(origin, direction, Color.red);
            }
        }

        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Player"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    private void UpdateLineOfSight()
    {
        visionCheckTimer -= Time.deltaTime;

        if (visionCheckTimer <= 0f)
        {
            bool previousLineOfSight = hasLineOfSight;
            hasLineOfSight = CheckLineOfSight();
            visionCheckTimer = visionCheckInterval;

            if (previousLineOfSight && !hasLineOfSight)
            {
                timeWithoutLineOfSight = 0f;
                if (logBehaviorDetails && currentState == BatState.Chase)
                {
                    Debug.Log($"{gameObject.name} perdió línea de visión temporalmente");
                }
            }
            else if (!previousLineOfSight && hasLineOfSight)
            {
                timeWithoutLineOfSight = 0f;
                if (logBehaviorDetails && currentState == BatState.Chase)
                {
                    Debug.Log($"{gameObject.name} recuperó línea de visión");
                }
            }
        }

        if (!hasLineOfSight && currentState == BatState.Chase)
        {
            timeWithoutLineOfSight += Time.deltaTime;
        }
        else if (hasLineOfSight)
        {
            timeWithoutLineOfSight = 0f;
        }
    }

    #endregion

    #region Wing Flap Movement System
    private Vector2 CalculateFlapOffset(Vector2 baseDirection)
    {
        flapTimer += Time.deltaTime;
        horizontalWaveTimer += Time.deltaTime;

        float verticalOffset;
        float horizontalOffset;

        if (usePerlinNoise)
        {
            // Usar ruido Perlin para movimiento más orgánico y natural
            float perlinTime = Time.time * perlinNoiseSpeed + perlinNoiseOffset;
            verticalOffset = (Mathf.PerlinNoise(perlinTime, 0f) - 0.5f) * 2f * flapAmplitude;
            horizontalOffset = (Mathf.PerlinNoise(0f, perlinTime) - 0.5f) * 2f * horizontalWaveAmplitude;
        }
        else
        {
            // Usar senos para movimiento más predecible
            verticalOffset = Mathf.Sin(flapTimer * flapFrequency) * flapAmplitude;
            horizontalOffset = Mathf.Sin(horizontalWaveTimer * horizontalWaveFrequency) * horizontalWaveAmplitude;
        }

        // Calcular dirección perpendicular para el movimiento horizontal
        Vector2 perpendicular = new Vector2(-baseDirection.y, baseDirection.x);

        // Combinar offsets
        Vector2 flapOffset = Vector2.up * verticalOffset + perpendicular * horizontalOffset;

        return flapOffset;
    }
    private void ApplyFlapMovement(Vector2 baseDirection, float speed)
    {
        Vector2 flapOffset = CalculateFlapOffset(baseDirection);
        Vector2 finalVelocity = baseDirection * speed + flapOffset;

        if (rb != null)
        {
            rb.linearVelocity = finalVelocity;
        }
        else
        {
            transform.position += (Vector3)finalVelocity * Time.deltaTime;
        }
    }

    #endregion

    #region State Machine

    private void UpdateBehavior()
    {
        UpdateLineOfSight();

        switch (currentState)
        {
            case BatState.Patrol:
                PatrolBehavior();
                CheckForPlayer();
                break;

            case BatState.Chase:
                ChaseBehavior();
                CheckAttackRange();
                CheckLoseTarget();
                break;

            case BatState.Attack:
                AttackBehavior();
                break;

            case BatState.Waiting:
                WaitBehavior();
                CheckForPlayer();
                break;
        }
    }
    private void ChangeState(BatState newState)
    {
        if (currentState == newState) return;

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} cambió de {currentState} a {newState}");
        }

        currentState = newState;

        switch (newState)
        {
            case BatState.Waiting:
                waitTimer = patrolWaitTime;
                waitingStartPosition = transform.position; // Guardar posición actual
                // Resetear los timers para un hover más suave
                flapTimer = 0f;
                horizontalWaveTimer = 0f;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                break;

            case BatState.Chase:
                UpdateVisualFeedback(chaseColor);
                break;

            case BatState.Patrol:
                UpdateVisualFeedback(patrolColor);
                break;
        }
    }


    #endregion

    #region Path Validation
    private bool IsPathClear(Vector2 from, Vector2 to)
    {
        if (!validatePatrolPath)
        {
            return true;
        }

        Vector2 direction = to - from;
        float distance = direction.magnitude;

        // Si la distancia es muy corta, siempre es válido
        if (distance < 0.5f)
        {
            return true;
        }

        direction.Normalize();

        // Raycast directo
        RaycastHit2D directHit = Physics2D.Raycast(from, direction, distance, pathObstacleLayer);

        if (directHit.collider != null)
        {
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Camino directo bloqueado por {directHit.collider.name}");
            }
            return false;
        }

        // Validación adicional con múltiples rayos para mejor detección
        float angleStep = 360f / pathValidationRayCount;
        float checkRadius = 0.3f; // Radio de verificación alrededor del punto

        for (int i = 0; i < pathValidationRayCount; i++)
        {
            float angle = i * angleStep;
            Vector2 offset = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * checkRadius;

            Vector2 checkPoint = to + offset;
            Vector2 checkDirection = checkPoint - from;
            float checkDistance = checkDirection.magnitude;

            RaycastHit2D hit = Physics2D.Raycast(from, checkDirection.normalized, checkDistance, pathObstacleLayer);

            if (hit.collider != null)
            {
                if (logBehaviorDetails)
                {
                    Debug.Log($"{gameObject.name}: Camino bloqueado en ángulo {angle}° por {hit.collider.name}");
                }
                return false;
            }
        }

        return true;
    }
    private bool IsPointValid(Vector2 point)
    {
        Collider2D hit = Physics2D.OverlapCircle(point, 0.2f, pathObstacleLayer);

        if (hit != null)
        {
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Punto {point} inválido, overlapping con {hit.name}");
            }
            return false;
        }

        return true;
    }

    #endregion

    #region Random Patrol Behavior

    private void GenerateNewPatrolPoint()
    {
        int attempts = 0;
        bool validPointFound = false;

        while (attempts < maxPathAttempts && !validPointFound)
        {
            attempts++;

            // Generar un punto aleatorio dentro del radio
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            float randomDistance = Random.Range(patrolRadius * 0.3f, patrolRadius);
            Vector2 candidatePoint = patrolCenter + (randomDirection * randomDistance);

            // Validar si el punto es alcanzable
            if (validatePatrolPath)
            {
                // Primero verificar si el punto no está dentro de un obstáculo
                if (!IsPointValid(candidatePoint))
                {
                    if (logBehaviorDetails)
                    {
                        Debug.Log($"{gameObject.name}: Intento {attempts} - Punto dentro de obstáculo");
                    }
                    continue;
                }

                // Verificar si hay un camino despejado
                if (!IsPathClear(transform.position, candidatePoint))
                {
                    if (logBehaviorDetails)
                    {
                        Debug.Log($"{gameObject.name}: Intento {attempts} - Camino bloqueado");
                    }
                    continue;
                }
            }

            // Punto válido encontrado
            currentPatrolTarget = candidatePoint;
            validPointFound = true;

            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Punto válido encontrado en intento {attempts}: {currentPatrolTarget}");
            }
        }

        // Si no se encontró un punto válido después de todos los intentos
        if (!validPointFound)
        {
            // Fallback: usar la posición actual como objetivo (quedarse quieto)
            currentPatrolTarget = transform.position;

            if (logBehaviorDetails)
            {
                Debug.LogWarning($"{gameObject.name}: No se encontró punto válido después de {maxPathAttempts} intentos. Quedándose en posición actual.");
            }
        }
    }
    private void PatrolBehavior()
    {
        Vector2 direction = (currentPatrolTarget - (Vector2)transform.position).normalized;
        float speed = patrolSpeed * GetMovementSpeedMultiplier();

        // Aplicar movimiento con aleteo
        ApplyFlapMovement(direction, speed);

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);

        float distanceToPoint = Vector2.Distance(transform.position, currentPatrolTarget);
        if (distanceToPoint <= waypointReachDistance)
        {
            OnReachedPatrolPoint();
        }
    }
    private void OnReachedPatrolPoint()
    {
        // Generar nuevo punto aleatorio
        GenerateNewPatrolPoint();

        // Entrar en estado de espera
        ChangeState(BatState.Waiting);
    }
    private void WaitBehavior()
    {
        // Movimiento de aleteo estacionario (hover)
        flapTimer += Time.deltaTime;
        horizontalWaveTimer += Time.deltaTime;

        float verticalHover;
        float horizontalDrift;

        if (usePerlinNoise)
        {
            float perlinTime = Time.time * perlinNoiseSpeed * 0.5f + perlinNoiseOffset;
            verticalHover = (Mathf.PerlinNoise(perlinTime, 0f) - 0.5f) * 2f * idleHoverAmplitude;
            horizontalDrift = (Mathf.PerlinNoise(0f, perlinTime + 100f) - 0.5f) * 2f * idleHorizontalDrift;
        }
        else
        {
            verticalHover = Mathf.Sin(flapTimer * idleHoverFrequency) * idleHoverAmplitude;
            horizontalDrift = Mathf.Sin(horizontalWaveTimer * idleDriftFrequency) * idleHorizontalDrift;
        }

        Vector3 hoverOffset = new Vector3(horizontalDrift, verticalHover, 0f);
        Vector3 targetPosition = waitingStartPosition + hoverOffset;

        if (rb != null)
        {
            // Movimiento suave hacia la posición de hover
            Vector2 hoverVelocity = (targetPosition - transform.position) * 2f;
            rb.linearVelocity = hoverVelocity;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 5f);
        }

        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0)
        {
            ChangeState(BatState.Patrol);
        }
    }

    #endregion

    #region Chase Behavior

    private void CheckForPlayer()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange && hasLineOfSight)
        {
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name} detectó al jugador a {distanceToPlayer:F2} unidades");
            }
            ChangeState(BatState.Chase);
        }
    }
    private void ChaseBehavior()
    {
        if (player == null)
        {
            ChangeState(BatState.Patrol);
            return;
        }

        Vector2 direction = (player.position - transform.position).normalized;
        float speed = chaseSpeed * GetMovementSpeedMultiplier();

        // Aplicar movimiento con aleteo (más agresivo en persecución)
        ApplyFlapMovement(direction, speed);

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);
    }
    private void CheckAttackRange()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange && attackTimer <= 0)
        {
            ChangeState(BatState.Attack);
        }
    }
    private void CheckLoseTarget()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        bool tooFar = distanceToPlayer > loseTargetDistance;
        bool lostVisionTooLong = timeWithoutLineOfSight >= loseLineOfSightDelay;

        if (tooFar || lostVisionTooLong)
        {
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name} perdió de vista al jugador");
            }
            timeWithoutLineOfSight = 0f;
            ChangeState(BatState.Patrol);
        }
    }

    #endregion

    #region Attack Behavior

    private void AttackBehavior()
    {
        if (!isAttacking)
        {
            StartAttack();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (attackWindupTimer > 0)
        {
            attackWindupTimer -= Time.deltaTime;
            if (attackWindupTimer <= 0)
            {
                ExecuteAttack();
            }
        }
    }
    private void StartAttack()
    {
        isAttacking = true;
        attackWindupTimer = attackWindupTime;

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} iniciando ataque");
        }

        OnAttackStartedCustom();
    }
    private void ExecuteAttack()
    {
        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} ejecutando ataque");
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, playerLayer);

        foreach (Collider2D hit in hits)
        {
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(attackDamage, transform.position);
                Debug.Log($"{gameObject.name} golpeó a {hit.gameObject.name}");
            }
        }

        OnAttackExecutedCustom();

        attackTimer = attackCooldown;
        isAttacking = false;

        ChangeState(BatState.Chase);
    }
    private void UpdateAttackTimer()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region Visual Feedback

    private void UpdateSpriteFlip(float directionX)
    {
        if (!autoFlipSprite || spriteRenderer == null) return;

        if (directionX > 0.01f)
        {
            spriteRenderer.flipX = true;
        }
        else if (directionX < -0.01f)
        {
            spriteRenderer.flipX = false;
        }
    }

    private void UpdateVisualFeedback(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    #endregion

    #region Stun Bar

    private void InitializeStunBar()
    {
        if (stunBar == null && autoFindStunBar)
        {
            FindStunBar();
        }

        if (stunBar != null)
        {
            stunBar.minValue = 0f;
            stunBar.maxValue = 100f;
            stunBar.value = 0f;

            if (stunBarFillImage == null)
            {
                stunBarFillImage = stunBar.fillRect?.GetComponent<Image>();
            }

            if (hideWhenZero)
            {
                SetStunBarVisibility(false);
            }
        }
    }

    private void FindStunBar()
    {
        Transform stunBarTransform = transform.Find("StunBar");

        if (stunBarTransform == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                if (slider.gameObject.name.Contains("Stun"))
                {
                    stunBar = slider;
                    break;
                }
            }
        }
        else
        {
            stunBar = stunBarTransform.GetComponent<Slider>();
        }
    }

    protected override void OnStunnedChangedCustom(float stunnedValue)
    {
        base.OnStunnedChangedCustom(stunnedValue);
        UpdateStunBar(stunnedValue);
    }

    private void UpdateStunBar(float stunnedValue)
    {
        if (stunBar == null) return;

        stunBar.value = stunnedValue;

        if (hideWhenZero)
        {
            SetStunBarVisibility(stunnedValue > 0);
        }

        if (stunBarFillImage != null && stunBarGradient != null)
        {
            float normalizedValue = stunnedValue / 100f;
            stunBarFillImage.color = stunBarGradient.Evaluate(normalizedValue);
        }
    }

    private void SetStunBarVisibility(bool visible)
    {
        if (stunBar != null)
        {
            stunBar.gameObject.SetActive(visible);
        }
    }

    #endregion

    #region Custom Override Methods

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();
        if (logBehaviorDetails)
        {
            Debug.Log($"Evil Bat {gameObject.name} inicializado con {GetMaxHealth()} HP");
        }
    }
    protected override void OnDamageTakenCustom(int damageAmount)
    {
        base.OnDamageTakenCustom(damageAmount);

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} recibió {damageAmount} de daño");
        }

        if (currentState == BatState.Patrol || currentState == BatState.Waiting)
        {
            if (player != null)
            {
                ChangeState(BatState.Chase);
            }
        }
    }
    protected override void OnDeathCustom()
    {
        base.OnDeathCustom();

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} ha muerto");
        }

        // ACTIVAR LA ANIMACIÓN DE MUERTE
        if (animationController != null)
        {
            animationController.TriggerDeath();
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Animación de muerte activada");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No se encontró BatAnimationController!");
        }

        // HACER QUE CAIGA SUPER RÁPIDO
        if (rb != null)
        {
            // Activar gravedad alta
            rb.gravityScale = deathGravityScale;

            // Aplicar velocidad de caída inmediata hacia abajo
            rb.linearVelocity = new Vector2(0f, -deathFallSpeed);

            // Asegurarse de que el Rigidbody siga activo para que la física funcione
            rb.simulated = true;

            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Caída rápida activada (Gravedad: {deathGravityScale}, Velocidad: {deathFallSpeed})");
            }
        }

        Destroy(gameObject, deathDestroyDelay);
    }
    protected virtual void OnAttackStartedCustom()
    {
        // Override en clases hijas para animaciones/efectos
    }
    protected virtual void OnAttackExecutedCustom()
    {
        // Override en clases hijas para efectos visuales/sonoros
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Vector2 center = Application.isPlaying ? patrolCenter :
            (useStartPositionAsCenter || customPatrolCenter == null) ?
            (Vector2)transform.position : (Vector2)customPatrolCenter.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentPatrolTarget, 0.3f);
            Gizmos.DrawLine(transform.position, currentPatrolTarget);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Vector3 attackPos = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(attackPos, attackRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

        if (Application.isPlaying && player != null && debugLineOfSight)
        {
            Vector3 visionPos = visionOrigin != null ? visionOrigin.position : transform.position;
            Gizmos.color = hasLineOfSight ? Color.green : Color.red;
            Gizmos.DrawLine(visionPos, player.position);
        }
    }

    #endregion
}