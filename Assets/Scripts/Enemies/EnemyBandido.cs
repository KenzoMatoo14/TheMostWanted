using UnityEngine;
using UnityEngine.UI;

public class EnemyBandido : EnemyBase
{
    [Header("Bandido Specific Settings")]
    [SerializeField] private bool logBehaviorDetails = true;

    [Header("Patrol Settings")]
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float patrolWaitTime = 2f; // Tiempo de espera en cada punto
    [SerializeField] private Transform[] patrolPoints; // Puntos de patrulla
    [SerializeField] private bool loopPatrol = true; // true = loop, false = ping-pong
    [SerializeField] private float waypointReachDistance = 0.2f; // Distancia para considerar que llegó al punto
    [SerializeField] private bool constrainToGroundMovement = true;

    [Header("Chase Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float loseTargetDistance = 8f; // Distancia para perder al objetivo
    [SerializeField] private LayerMask playerLayer;

    [Header("Line of Sight Settings")]
    [SerializeField] private LayerMask obstacleLayer; // Capa de paredes/obstáculos
    [SerializeField] private bool requireLineOfSight = true; // Activar/desactivar LOS
    [SerializeField] private float visionCheckInterval = 0.2f; // Frecuencia de chequeo (optimización)
    [SerializeField] private Transform visionOrigin; // Punto desde donde mira (ojos del enemigo)
    [SerializeField] private bool debugLineOfSight = true; // Mostrar rayos de visión
    [SerializeField] private float loseLineOfSightDelay = 0.5f; // Tiempo antes de perder al jugador sin visión

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackWindupTime = 0.3f; // Tiempo antes de hacer daño
    [SerializeField] private Transform attackPoint; // Punto desde donde se verifica el ataque
    [SerializeField] private float attackRadius = 1f; // Radio del área de ataque

    [Header("Jump Settings")]
    [SerializeField] private bool canJump = true;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpCooldown = 1f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private Transform obstacleCheck;
    [SerializeField] private float obstacleCheckRadius = 0.2f;
    [SerializeField] private float obstacleCheckDistance = 0.5f;
    [SerializeField] private float playerAboveDetectionHeight = 1f;
    [SerializeField] private bool debugJump = true;

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

    // Estado interno
    private enum BanditState { Patrol, Chase, Attack, Waiting }
    private BanditState currentState = BanditState.Patrol;

    private int currentPatrolIndex = 0;
    private bool patrolForward = true; // Para el modo ping-pong
    private float waitTimer = 0f;
    private float attackTimer = 0f;
    private bool isAttacking = false;
    private float attackWindupTimer = 0f;
    private float visionCheckTimer = 0f;
    private bool hasLineOfSight = false;
    private float timeWithoutLineOfSight = 0f;

    private float jumpTimer = 0f;
    private bool isGrounded = false;
    private bool wasBlocked = false;

    private Transform player;
    private Vector2 lastMoveDirection = Vector2.right;

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

        // Si no se asigna un punto de visión, usar la posición del enemigo
        if (visionOrigin == null)
        {
            visionOrigin = transform;
        }

        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.parent = transform;
            groundCheckObj.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = groundCheckObj.transform;
        }
        if (obstacleCheck == null)
        {
            GameObject obstacleCheckObj = new GameObject("ObstacleCheck");
            obstacleCheckObj.transform.parent = transform;
            obstacleCheckObj.transform.localPosition = new Vector3(0, 0.3f, 0); // Un poco arriba del centro
            obstacleCheck = obstacleCheckObj.transform;
        }

        FindPlayer();

        // Validar puntos de patrulla
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} no tiene puntos de patrulla asignados. Creando patrulla simple.");
            CreateDefaultPatrolPoints();
        }

        InitializeStunBar();

        if (logBehaviorDetails)
        {
            Debug.Log($"Bandido {gameObject.name} inicializado");
        }
    }

    protected override void Update()
    {
        base.Update();

        // No hacer nada si está muerto, capturado o siendo capturado
        if (isDead || isCaptured )
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); // Mantener gravedad
            }
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        // No moverse si está completamente aturdido
        if (IsFullyStunned())
        {
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
            return;
        }

        if (IsInKnockback())
        {
            return;
        }

        UpdateBehavior();
        UpdateAttackTimer();
        UpdateJumpTimer();
        CheckGrounded();

        // Verificar si debe saltar
        if (canJump && isGrounded && jumpTimer <= 0 && !IsFullyStunned())
        {
            if (currentState == BanditState.Chase || currentState == BanditState.Patrol)
            {
                if (ShouldJump())
                {
                    PerformJump();
                }
            }
        }
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

    /// <summary>
    /// Verifica si el enemigo tiene línea de visión directa al jugador
    /// </summary>
    private bool CheckLineOfSight()
    {
        if (!requireLineOfSight || player == null)
        {
            return true; // Si no se requiere LOS, siempre retorna true
        }

        Vector2 origin = visionOrigin.position;
        Vector2 targetPosition = player.position;
        Vector2 direction = targetPosition - origin;
        float distance = direction.magnitude;

        // Hacer un raycast hacia el jugador
        RaycastHit2D hit = Physics2D.Raycast(origin, direction.normalized, distance, obstacleLayer | playerLayer);

        // Debug visual
        if (debugLineOfSight)
        {
            if (hit.collider != null && hit.collider.CompareTag("Player"))
            {
                Debug.DrawRay(origin, direction, Color.green); // Verde = puede ver
            }
            else
            {
                Debug.DrawRay(origin, direction, Color.red); // Rojo = bloqueado
            }
        }

        // Si el raycast golpea algo
        if (hit.collider != null)
        {
            // Verificar si lo que golpeó es el jugador
            if (hit.collider.CompareTag("Player"))
            {
                return true; // Visión clara al jugador
            }
            else
            {
                // Golpeó un obstáculo antes de llegar al jugador
                return false;
            }
        }

        // No golpeó nada (no debería pasar si el jugador tiene collider)
        return false;
    }

    /// <summary>
    /// Actualiza el estado de línea de visión con intervalo de tiempo (optimización)
    /// </summary>
    private void UpdateLineOfSight()
    {
        visionCheckTimer -= Time.deltaTime;

        if (visionCheckTimer <= 0f)
        {
            bool previousLineOfSight = hasLineOfSight;
            hasLineOfSight = CheckLineOfSight();
            visionCheckTimer = visionCheckInterval;

            // Si perdió línea de visión, empezar a contar
            if (previousLineOfSight && !hasLineOfSight)
            {
                timeWithoutLineOfSight = 0f;
                if (logBehaviorDetails && currentState == BanditState.Chase)
                {
                    Debug.Log($"{gameObject.name} perdió línea de visión temporalmente");
                }
            }
            // Si recuperó línea de visión, resetear contador
            else if (!previousLineOfSight && hasLineOfSight)
            {
                timeWithoutLineOfSight = 0f;
                if (logBehaviorDetails && currentState == BanditState.Chase)
                {
                    Debug.Log($"{gameObject.name} recuperó línea de visión");
                }
            }
        }

        // Si no tiene línea de visión, incrementar el contador
        if (!hasLineOfSight && currentState == BanditState.Chase)
        {
            timeWithoutLineOfSight += Time.deltaTime;
        }
        else if (hasLineOfSight)
        {
            timeWithoutLineOfSight = 0f;
        }
    }

    #endregion

    #region State Machine

    private void UpdateBehavior()
    {
        // Actualizar línea de visión
        UpdateLineOfSight();

        switch (currentState)
        {
            case BanditState.Patrol:
                PatrolBehavior();
                CheckForPlayer();
                break;

            case BanditState.Chase:
                ChaseBehavior();
                CheckAttackRange();
                CheckLoseTarget();
                break;

            case BanditState.Attack:
                AttackBehavior();
                break;

            case BanditState.Waiting:
                WaitBehavior();
                CheckForPlayer();
                break;
        }
    }
    private void ChangeState(BanditState newState)
    {
        if (currentState == newState) return;

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} cambió de {currentState} a {newState}");
        }

        currentState = newState;

        // Acciones al entrar al estado
        switch (newState)
        {
            case BanditState.Waiting:
                waitTimer = patrolWaitTime;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                break;

            case BanditState.Chase:
                UpdateVisualFeedback(chaseColor);
                break;

            case BanditState.Patrol:
                UpdateVisualFeedback(patrolColor);
                break;
        }
    }

    #endregion

    #region Patrol Behavior

    private void PatrolBehavior()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        Transform targetPoint = patrolPoints[currentPatrolIndex];
        if (targetPoint == null) return;

        // Moverse hacia el punto de patrulla
        Vector2 direction = (targetPoint.position - transform.position);
        direction.y = 0; // Ignorar diferencia vertical
        direction = direction.normalized;

        float speed = patrolSpeed * GetMovementSpeedMultiplier();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;
        }

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);

        // Verificar si llegó al punto
        float distanceToPoint = Mathf.Abs(transform.position.x - targetPoint.position.x);
        if (distanceToPoint <= waypointReachDistance)
        {
            OnReachedPatrolPoint();
        }
    }
    private void OnReachedPatrolPoint()
    {
        ChangeState(BanditState.Waiting);

        // Avanzar al siguiente punto
        if (loopPatrol)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Modo ping-pong
            if (patrolForward)
            {
                currentPatrolIndex++;
                if (currentPatrolIndex >= patrolPoints.Length)
                {
                    currentPatrolIndex = patrolPoints.Length - 2;
                    patrolForward = false;
                }
            }
            else
            {
                currentPatrolIndex--;
                if (currentPatrolIndex < 0)
                {
                    currentPatrolIndex = 1;
                    patrolForward = true;
                }
            }
        }

        // Entrar en estado de espera
        ChangeState(BanditState.Waiting);
    }
    private void WaitBehavior()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0)
        {
            ChangeState(BanditState.Patrol);
        }
    }
    private void CreateDefaultPatrolPoints()
    {
        // Crear dos puntos de patrulla simples
        GameObject patrolContainer = new GameObject($"{gameObject.name}_PatrolPoints");

        GameObject point1 = new GameObject("PatrolPoint1");
        point1.transform.parent = patrolContainer.transform;
        point1.transform.position = transform.position + Vector3.left * 3f;

        GameObject point2 = new GameObject("PatrolPoint2");
        point2.transform.parent = patrolContainer.transform;
        point2.transform.position = transform.position + Vector3.right * 3f;

        patrolPoints = new Transform[] { point1.transform, point2.transform };
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
            ChangeState(BanditState.Chase);
        }
    }
    private void ChaseBehavior()
    {
        if (player == null)
        {
            ChangeState(BanditState.Patrol);
            return;
        }

        Vector2 direction = (player.position - transform.position);
        direction.y = 0; // Ignorar diferencia vertical
        direction = direction.normalized;

        float speed = chaseSpeed * GetMovementSpeedMultiplier();

        if (rb != null)
        {
            // Solo modificar velocidad horizontal, mantener la velocidad vertical (gravedad)
            rb.linearVelocity = new Vector2(direction.x * speed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += new Vector3(direction.x * speed * Time.deltaTime, 0, 0);
        }

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);
    }
    private void CheckAttackRange()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange && attackTimer <= 0)
        {
            ChangeState(BanditState.Attack);
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
            ChangeState(BanditState.Patrol);
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

        // Detener movimiento durante el ataque
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        // Contar el windup
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

        // Aquí puedes activar animación de ataque
        OnAttackStartedCustom();
    }
    private void ExecuteAttack()
    {
        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} ejecutando ataque");
        }

        // Detectar jugador en rango de ataque
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

        // Reiniciar timers
        attackTimer = attackCooldown;
        isAttacking = false;

        // Volver a perseguir
        ChangeState(BanditState.Chase);
    }
    private void UpdateAttackTimer()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    #endregion

    #region Jump Behavior
    private void CheckGrounded()
    {
        if (groundCheck == null) return;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }
    private void UpdateJumpTimer()
    {
        if (jumpTimer > 0)
        {
            jumpTimer -= Time.deltaTime;
        }
    }
    private bool ShouldJump()
    {
        // Verificar si hay un obstáculo delante usando OverlapCircle (igual que groundCheck)
        bool hasObstacle = CheckObstacleAhead();

        // Verificar si el jugador está encima
        bool playerAbove = IsPlayerAbove();

        if (hasObstacle || playerAbove)
        {
            if (debugJump)
            {
                string reason = hasObstacle ? "obstáculo detectado" : "jugador encima";
                Debug.Log($"{gameObject.name} va a saltar: {reason}");
            }
            return true;
        }

        return false;
    }
    private bool CheckObstacleAhead()
    {
        if (obstacleCheck == null) return false;

        // Determinar dirección de movimiento
        float direction = lastMoveDirection.x != 0 ? Mathf.Sign(lastMoveDirection.x) :
                         (transform.localScale.x > 0 ? -1f : 1f);

        // Calcular posición del check adelante del enemigo
        Vector2 checkPosition = (Vector2)obstacleCheck.position + new Vector2(direction * obstacleCheckDistance, 0);

        // Usar OverlapCircle para detectar obstáculos (igual que groundCheck)
        bool hasObstacle = Physics2D.OverlapCircle(checkPosition, obstacleCheckRadius, groundLayer);

        if (debugJump && hasObstacle)
        {
            Debug.Log($"{gameObject.name} detectó obstáculo adelante");
        }

        return hasObstacle;
    }
    private bool IsPlayerAbove()
    {
        if (player == null) return false;

        Vector2 boxCenter = (Vector2)transform.position + Vector2.up * (playerAboveDetectionHeight * 0.5f);
        Vector2 boxSize = new Vector2(1f, playerAboveDetectionHeight);

        Collider2D hit = Physics2D.OverlapBox(boxCenter, boxSize, 0f, playerLayer);

        if (debugJump && hit != null)
        {
            Debug.DrawLine(transform.position, boxCenter + Vector2.up * (playerAboveDetectionHeight * 0.5f), Color.magenta);
        }

        return hit != null;
    }
    private void PerformJump()
    {
        if (rb == null) return;

        // Aplicar fuerza vertical
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);

        // Reiniciar cooldown
        jumpTimer = jumpCooldown;

        if (debugJump)
        {
            Debug.Log($"{gameObject.name} realizó un salto con fuerza {jumpForce}");
        }

        OnJumpPerformed();
    }
    protected virtual void OnJumpPerformed()
    {
        // Override para efectos de sonido/visuales
    }

    #endregion

    #region Visual Feedback

    private void UpdateSpriteFlip(float directionX)
    {
        if (!autoFlipSprite || spriteRenderer == null) return;

        Vector3 localScale = transform.localScale;

        if (directionX > 0.01f)
        {
            localScale.x = -Mathf.Abs(localScale.x); // Asegura que sea positivo (mirando a la derecha)
        }
        else if (directionX < -0.01f)
        {
            localScale.x = Mathf.Abs(localScale.x); // Negativo (mirando a la izquierda)
        }

        transform.localScale = localScale;
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
            Debug.Log($"Bandido {gameObject.name} inicializado con {GetMaxHealth()} HP");
        }
    }

    protected override void OnDamageTakenCustom(int damageAmount)
    {
        base.OnDamageTakenCustom(damageAmount);

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} recibió {damageAmount} de daño");
        }

        // Entrar en modo persecución si está patrullando
        if (currentState == BanditState.Patrol || currentState == BanditState.Waiting)
        {
            if (player != null)
            {
                ChangeState(BanditState.Chase);
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

        // Detener completamente
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        // Desactivar colisiones
        Collider2D[] cols = GetComponents<Collider2D>();
        foreach (Collider2D col in cols)
        {
            col.enabled = false;
        }

        // Destruir después de un tiempo
        Destroy(gameObject, 3f);
    }

    protected virtual void OnAttackStartedCustom()
    {
        // Override en clases hijas para animaciones/efectos de inicio de ataque
    }

    protected virtual void OnAttackExecutedCustom()
    {
        // Override en clases hijas para efectos visuales/sonoros del golpe
    }

    #endregion

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        // Rango de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Rango de ataque
        Gizmos.color = Color.red;
        Vector3 attackPos = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(attackPos, attackRadius);

        // Rango para perder objetivo
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

        // Puntos de patrulla
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolPoints[i].position, 0.3f);

                    // Líneas entre puntos
                    if (i < patrolPoints.Length - 1 && patrolPoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
                    }
                    else if (loopPatrol && i == patrolPoints.Length - 1 && patrolPoints[0] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[0].position);
                    }
                }
            }
        }
        // Línea de visión (en Play Mode)
        if (Application.isPlaying && player != null && debugLineOfSight)
        {
            Vector3 visionPos = visionOrigin != null ? visionOrigin.position : transform.position;
            Gizmos.color = hasLineOfSight ? Color.green : Color.red;
            Gizmos.DrawLine(visionPos, player.position);
        }

        // Ground check
        if (groundCheck != null)
        {
            Gizmos.color = Application.isPlaying && isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // Detección de obstáculos para salto
        if (canJump)
        {
            Vector3 boxCenter = transform.position + Vector3.up * (playerAboveDetectionHeight * 0.5f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(boxCenter, new Vector3(1f, playerAboveDetectionHeight, 0.1f));
        }
    }

    #endregion
}