using UnityEngine;

public class EnemyBandido : MeleeChaserEnemy
{
    [Header("Patrol Points")]
    [SerializeField] private Transform[] patrolPoints; // Puntos de patrulla
    [SerializeField] private bool loopPatrol = true; // true = loop, false = ping-pong

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

    private int currentPatrolIndex = 0;
    private bool patrolForward = true; // Para el modo ping-pong

    private float jumpTimer = 0f;
    public bool isGrounded = false;

    protected override void Start()
    {
        base.Start();

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

        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} no tiene puntos de patrulla asignados. Creando patrulla simple.");
            CreateDefaultPatrolPoints();
        }

        if (logBehaviorDetails)
        {
            Debug.Log($"Bandido {gameObject.name} inicializado");
        }
    }

    protected override void OnActiveUpdate()
    {
        UpdateJumpTimer();
        CheckGrounded();

        if (canJump && isGrounded && jumpTimer <= 0 && !IsFullyStunned())
        {
            if (currentState == ChaserState.Chase || currentState == ChaserState.Patrol)
            {
                if (ShouldJump())
                {
                    PerformJump();
                }
            }
        }
    }

    #region Patrol Behavior

    protected override void PatrolBehavior()
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
        // Avanzar al siguiente punto
        if (loopPatrol)
        {
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            // Modo ping-pong
            if (patrolPoints.Length <= 1)
            {
                // Un solo punto de patrulla: no hay rebote posible, quedarse en el índice 0
                currentPatrolIndex = 0;
            }
            else if (patrolForward)
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
        ChangeState(ChaserState.Waiting);
    }
    protected override void WaitBehavior()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        TickWaitTimer();
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

    protected override void ChaseBehavior()
    {
        if (player == null)
        {
            ChangeState(ChaserState.Patrol);
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

    protected override void UpdateSpriteFlip(float directionX)
    {
        if (!autoFlipSprite || spriteRenderer == null) return;

        Vector3 localScale = transform.localScale;

        if (directionX > 0.01f)
        {
            localScale.x = -Mathf.Abs(localScale.x);
        }
        else if (directionX < -0.01f)
        {
            localScale.x = Mathf.Abs(localScale.x);
        }

        transform.localScale = localScale;
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
