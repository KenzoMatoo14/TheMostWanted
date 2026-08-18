using UnityEngine;

/// <summary>
/// Base compartida para enemigos cuerpo a cuerpo que persiguen al jugador:
/// línea de visión, detección, persecución, ataque y la máquina de estados
/// Patrol/Chase/Attack/Waiting. Cada enemigo concreto solo implementa su
/// propio estilo de movimiento (patrulla, flip de sprite) y, si aplica,
/// efectos visuales de muerte.
/// </summary>
public abstract class MeleeChaserEnemy : EnemyBase
{
    protected enum ChaserState { Patrol, Chase, Attack, Waiting }
    protected ChaserState currentState = ChaserState.Patrol;

    [Header("Chaser Behavior Settings")]
    [SerializeField] protected bool logBehaviorDetails = true;

    [Header("Patrol Settings")]
    [SerializeField] protected float patrolSpeed = 2f;
    [SerializeField] protected float patrolWaitTime = 2f; // Tiempo de espera en cada punto
    [SerializeField] protected float waypointReachDistance = 0.2f;

    [Header("Chase Settings")]
    [SerializeField] protected float detectionRange = 5f;
    [SerializeField] protected float chaseSpeed = 4f;
    [SerializeField] protected float loseTargetDistance = 8f; // Distancia para perder al objetivo
    [SerializeField] protected LayerMask playerLayer;

    [Header("Line of Sight Settings")]
    [SerializeField] protected LayerMask obstacleLayer; // Capa de paredes/obstáculos
    [SerializeField] protected bool requireLineOfSight = true;
    [SerializeField] protected float visionCheckInterval = 0.2f;
    [SerializeField] protected Transform visionOrigin; // Punto desde donde mira (ojos del enemigo)
    [SerializeField] protected bool debugLineOfSight = true;
    [SerializeField] protected float loseLineOfSightDelay = 0.5f;

    [Header("Attack Settings")]
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float attackCooldown = 1.5f;
    [SerializeField] protected int attackDamage = 10;
    [SerializeField] protected float attackWindupTime = 0.3f;
    [SerializeField] protected Transform attackPoint;
    [SerializeField] protected float attackRadius = 1f;

    [Header("Visual Feedback")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected bool autoFlipSprite = true;
    [SerializeField] protected Color chaseColor = Color.red;
    [SerializeField] protected Color patrolColor = Color.white;

    [Header("Death Settings")]
    [SerializeField] protected float deathGravityScale = 15f; // Gravedad al morir
    [SerializeField] protected float deathFallSpeed = 20f; // Velocidad de caída adicional
    [SerializeField] protected float deathDestroyDelay = 3f; // Tiempo antes de destruir

    protected float waitTimer = 0f;
    protected float attackTimer = 0f;
    public bool isAttacking = false;
    protected float attackWindupTimer = 0f;
    protected float visionCheckTimer = 0f;
    protected bool hasLineOfSight = false;
    protected float timeWithoutLineOfSight = 0f;

    protected Transform player;
    protected Vector2 lastMoveDirection = Vector2.right;

    protected override void Start()
    {
        base.Start();

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

        FindPlayer();
    }

    protected override void Update()
    {
        if (isDead || isCaptured)
        {
            FreezeMovement();
            return;
        }

        base.Update();

        if (player == null)
        {
            FindPlayer();
        }

        if (IsFullyStunned())
        {
            FreezeMovement();
            return;
        }

        if (IsInKnockback())
        {
            return;
        }

        UpdateBehavior();
        UpdateAttackTimer();
        OnActiveUpdate();
    }

    /// <summary>
    /// Punto de extensión para lógica que solo debe correr mientras el enemigo está
    /// activo (no muerto/capturado/aturdido/en knockback). Ej: salto del Bandido.
    /// </summary>
    protected virtual void OnActiveUpdate() { }

    /// <summary>
    /// Detiene el movimiento del enemigo. Por defecto conserva la velocidad vertical
    /// (para enemigos con gravedad); los enemigos voladores la sobrescriben a Vector2.zero.
    /// </summary>
    protected virtual void FreezeMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
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
            return hit.collider.CompareTag("Player");
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
                if (logBehaviorDetails && currentState == ChaserState.Chase)
                {
                    Debug.Log($"{gameObject.name} perdió línea de visión temporalmente");
                }
            }
            else if (!previousLineOfSight && hasLineOfSight)
            {
                timeWithoutLineOfSight = 0f;
                if (logBehaviorDetails && currentState == ChaserState.Chase)
                {
                    Debug.Log($"{gameObject.name} recuperó línea de visión");
                }
            }
        }

        if (!hasLineOfSight && currentState == ChaserState.Chase)
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
        UpdateLineOfSight();

        switch (currentState)
        {
            case ChaserState.Patrol:
                PatrolBehavior();
                CheckForPlayer();
                break;

            case ChaserState.Chase:
                ChaseBehavior();
                CheckAttackRange();
                CheckLoseTarget();
                break;

            case ChaserState.Attack:
                AttackBehavior();
                break;

            case ChaserState.Waiting:
                WaitBehavior();
                CheckForPlayer();
                break;
        }
    }

    protected void ChangeState(ChaserState newState)
    {
        if (currentState == newState) return;

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} cambió de {currentState} a {newState}");
        }

        currentState = newState;

        switch (newState)
        {
            case ChaserState.Waiting:
                waitTimer = patrolWaitTime;
                if (rb != null) rb.linearVelocity = Vector2.zero;
                OnEnterWaitingState();
                break;

            case ChaserState.Chase:
                UpdateVisualFeedback(chaseColor);
                break;

            case ChaserState.Patrol:
                UpdateVisualFeedback(patrolColor);
                break;
        }
    }

    /// <summary>
    /// Hook para que cada enemigo resetee estado propio (ej. temporizadores de aleteo) al entrar en espera.
    /// </summary>
    protected virtual void OnEnterWaitingState() { }

    /// <summary>
    /// Cuenta regresiva compartida del estado de espera. Cada enemigo la llama desde su propio
    /// WaitBehavior después de aplicar su movimiento de espera (hover, quieto, etc.).
    /// </summary>
    protected void TickWaitTimer()
    {
        waitTimer -= Time.deltaTime;

        if (waitTimer <= 0)
        {
            ChangeState(ChaserState.Patrol);
        }
    }

    #endregion

    protected abstract void PatrolBehavior();
    protected abstract void ChaseBehavior();
    protected abstract void WaitBehavior();
    protected abstract void UpdateSpriteFlip(float directionX);

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
            ChangeState(ChaserState.Chase);
        }
    }
    private void CheckAttackRange()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange && attackTimer <= 0)
        {
            ChangeState(ChaserState.Attack);
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
            ChangeState(ChaserState.Patrol);
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

        FreezeMovement();

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
            }
        }

        OnAttackExecutedCustom();

        attackTimer = attackCooldown;
        isAttacking = false;

        ChangeState(ChaserState.Chase);
    }
    private void UpdateAttackTimer()
    {
        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
        }
    }

    #endregion

    protected void UpdateVisualFeedback(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
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
        if (currentState == ChaserState.Patrol || currentState == ChaserState.Waiting)
        {
            if (player != null)
            {
                ChangeState(ChaserState.Chase);
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

        OnDeathVisuals();

        if (rb != null)
        {
            rb.gravityScale = deathGravityScale;
            rb.linearVelocity = new Vector2(0f, -deathFallSpeed);
            rb.simulated = true;

            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name}: Caída rápida activada (Gravedad: {deathGravityScale}, Velocidad: {deathFallSpeed})");
            }
        }

        Destroy(gameObject, deathDestroyDelay);
    }

    /// <summary>
    /// Hook para animaciones/efectos de muerte específicos (ej. TriggerDeath en el Bat).
    /// </summary>
    protected virtual void OnDeathVisuals() { }

    protected virtual void OnAttackStartedCustom() { }
    protected virtual void OnAttackExecutedCustom() { }
}
