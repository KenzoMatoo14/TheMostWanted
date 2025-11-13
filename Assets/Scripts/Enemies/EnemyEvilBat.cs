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

    [Header("Chase Settings")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float loseTargetDistance = 8f;
    [SerializeField] private LayerMask playerLayer;

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

    // Estado interno
    private enum BatState { Patrol, Chase, Attack, Waiting }
    private BatState currentState = BatState.Patrol;

    private Vector2 patrolCenter; // Centro de la patrulla
    private Vector2 currentPatrolTarget; // Punto actual al que se dirige
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

        // Establecer centro de patrulla
        if (useStartPositionAsCenter || customPatrolCenter == null)
        {
            patrolCenter = transform.position;
        }
        else
        {
            patrolCenter = customPatrolCenter.position;
        }

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

    #region State Machine

    private void UpdateBehavior()
    {
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

    #region Random Patrol Behavior

    private void GenerateNewPatrolPoint()
    {
        // Generar un punto aleatorio dentro del radio
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        float randomDistance = Random.Range(patrolRadius * 0.3f, patrolRadius);

        currentPatrolTarget = patrolCenter + (randomDirection * randomDistance);

        if (logBehaviorDetails)
        {
            Debug.Log($"{gameObject.name} nuevo punto de patrulla: {currentPatrolTarget}");
        }
    }

    private void PatrolBehavior()
    {
        // Moverse hacia el punto aleatorio
        Vector2 direction = (currentPatrolTarget - (Vector2)transform.position).normalized;
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
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
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

        if (distanceToPlayer <= detectionRange)
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
            ChangeState(BatState.Attack);
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

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Collider2D[] cols = GetComponents<Collider2D>();
        foreach (Collider2D col in cols)
        {
            col.enabled = false;
        }

        Destroy(gameObject, 3f);
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
        // Centro de patrulla
        Vector2 center = Application.isPlaying ? patrolCenter :
            (useStartPositionAsCenter || customPatrolCenter == null) ?
            (Vector2)transform.position : (Vector2)customPatrolCenter.position;

        // Radio de patrulla
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, patrolRadius);

        // Punto objetivo actual (solo en runtime)
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentPatrolTarget, 0.3f);
            Gizmos.DrawLine(transform.position, currentPatrolTarget);
        }

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
    }

    #endregion
}