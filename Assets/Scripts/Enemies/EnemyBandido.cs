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

    [Header("Chase Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float loseTargetDistance = 8f; // Distancia para perder al objetivo
    [SerializeField] private LayerMask playerLayer;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackWindupTime = 0.3f; // Tiempo antes de hacer daño
    [SerializeField] private Transform attackPoint; // Punto desde donde se verifica el ataque
    [SerializeField] private float attackRadius = 1f; // Radio del área de ataque

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
                rb.linearVelocity = Vector2.zero;
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
                rb.linearVelocity = Vector2.zero;
            }
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

    #region State Machine

    private void UpdateBehavior()
    {
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
        Vector2 direction = (targetPoint.position - transform.position).normalized;
        float speed = patrolSpeed * GetMovementSpeedMultiplier();

        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
        else
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;
        }

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);

        // Verificar si llegó al punto
        float distanceToPoint = Vector2.Distance(transform.position, targetPoint.position);
        if (distanceToPoint <= waypointReachDistance)
        {
            OnReachedPatrolPoint();
        }
    }
    private void OnReachedPatrolPoint()
    {
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
            rb.linearVelocity = Vector2.zero;
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

        if (distanceToPlayer <= detectionRange)
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

        Vector2 direction = (player.position - transform.position).normalized;
        float speed = chaseSpeed * GetMovementSpeedMultiplier();

        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
        else
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;
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

        if (distanceToPlayer > loseTargetDistance)
        {
            if (logBehaviorDetails)
            {
                Debug.Log($"{gameObject.name} perdió de vista al jugador");
            }
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
            rb.linearVelocity = Vector2.zero;
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
                damageable.TakeDamage(attackDamage);
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

    #region Visual Feedback

    private void UpdateSpriteFlip(float directionX)
    {
        
        if (!autoFlipSprite || spriteRenderer == null) return;

        if (directionX > 0.01f)
        {
            spriteRenderer.flipX = false;
        }
        else if (directionX < -0.01f)
        {
            spriteRenderer.flipX = true;
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
    }

    #endregion
}