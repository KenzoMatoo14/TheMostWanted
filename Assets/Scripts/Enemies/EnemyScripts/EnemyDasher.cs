using UnityEngine;

/// <summary>
/// Enemigo que, en vez de perseguir hasta golpear cuerpo a cuerpo, se queda vigilando
/// y al detectar al jugador telegrafía brevemente y luego se lanza en línea recta a
/// máxima velocidad. Solo se detiene al golpear al jugador, al pasarlo de largo, o al
/// chocar contra un obstáculo del nivel.
/// </summary>
public class EnemyDasher : MeleeChaserEnemy
{
    private enum DashPhase { Telegraph, Dashing, Recovering }
    private DashPhase dashPhase = DashPhase.Telegraph;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 12f;
    [Tooltip("Distancia extra que recorre más allá de la posición del jugador al momento de arrancar la carga, para que se sienta como que lo 'pasa de largo' en vez de frenar justo encima.")]
    [SerializeField] private float dashOvershootBuffer = 1.5f;

    [Header("Obstacle Check")]
    [SerializeField] private Transform obstacleCheck;
    [SerializeField] private float obstacleCheckRadius = 0.3f;
    [SerializeField] private float obstacleCheckDistance = 0.6f;

    [Header("Dash Hitbox")]
    [Tooltip("Collider2D (trigger) cuadrado que detecta el golpe al instante durante la carga. Se habilita solo mientras dura el dash.")]
    [SerializeField] private Collider2D dashHitboxCollider;
    [Tooltip("Tamaño local de la caja si hay que autogenerarla (se multiplica por la escala del Dasher).")]
    [SerializeField] private Vector2 dashHitboxSize = new Vector2(1f, 1f);

    [Header("Dash Visual Feedback")]
    [SerializeField] private Color telegraphColor = new Color(1f, 0.55f, 0.55f, 1f); // Rojo claro
    [SerializeField] private Color dashColor = new Color(0.75f, 0f, 0f, 1f); // Rojo intenso

    [Header("Impact Feedback")]
    [Tooltip("Velocidad del retroceso al impactar al jugador.")]
    [SerializeField] private float recoilSpeed = 6f;
    [Tooltip("Cuánto dura el retroceso antes de que el Dasher se quede quieto en recuperación.")]
    [SerializeField] private float recoilDuration = 0.15f;

    private Vector2 dashDirection;
    private Vector2 dashStartPosition;
    private float dashTargetDistance;
    private float recoilTimer;

    protected override void Start()
    {
        base.Start();

        if (obstacleCheck == null)
        {
            GameObject obstacleCheckObj = new GameObject("ObstacleCheck");
            obstacleCheckObj.transform.parent = transform;
            obstacleCheckObj.transform.localPosition = new Vector3(0, 0.3f, 0);
            obstacleCheck = obstacleCheckObj.transform;
        }

        if (dashHitboxCollider == null)
        {
            // Segundo Collider2D en el mismo GameObject: el de siempre sigue siendo sólido
            // (física normal), este es un trigger que solo se activa durante el dash.
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = dashHitboxSize;
            dashHitboxCollider = box;
        }

        dashHitboxCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (dashPhase != DashPhase.Dashing) return;
        if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(attackDamage, transform.position);
        }

        OnAttackExecutedCustom();
        EndDash();

        // Retroceso al impactar, como feedback del golpe
        recoilTimer = recoilDuration;
    }

    #region Patrol / Wait (el Dasher no patrulla, solo vigila quieto)

    protected override void PatrolBehavior()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }
    }

    protected override void WaitBehavior()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        TickWaitTimer();
    }

    #endregion

    #region Chase Behavior (eliminada: el Dasher va directo de detectar a atacar)

    /// <summary>
    /// El Dasher no tiene fase de acercamiento caminando: en cuanto detecta al jugador
    /// pasa directo a Attack (ver GetEngagementState). Este override queda solo como
    /// red de seguridad por si algo más lo transiciona a Chase (ej. recibir daño
    /// mientras está parado, que en la base pasa por Chase) — lo redirige de inmediato.
    /// </summary>
    protected override void ChaseBehavior()
    {
        ChangeState(ChaserState.Attack);
    }

    protected override ChaserState GetEngagementState() => ChaserState.Attack;

    #endregion

    #region Dash Attack

    protected override void AttackBehavior()
    {
        if (!isAttacking)
        {
            BeginTelegraph();
        }

        switch (dashPhase)
        {
            case DashPhase.Telegraph:
                UpdateTelegraph();
                break;

            case DashPhase.Dashing:
                UpdateDash();
                break;

            case DashPhase.Recovering:
                UpdateRecovery();
                break;
        }
    }

    private void BeginTelegraph()
    {
        isAttacking = true;
        dashPhase = DashPhase.Telegraph;
        attackWindupTimer = attackWindupTime;

        UpdateVisualFeedback(telegraphColor);

        // Encarar al jugador desde ya para que el "tell" visual sea claro (el hitbox
        // se termina de orientar en StartDash, por si se mueve durante el telegraph).
        if (player != null)
        {
            UpdateSpriteFlip(player.position.x - transform.position.x);
        }

        OnAttackStartedCustom();
    }

    private void UpdateTelegraph()
    {
        FreezeMovement();

        attackWindupTimer -= Time.deltaTime;
        if (attackWindupTimer <= 0)
        {
            StartDash();
        }
    }

    private void StartDash()
    {
        dashPhase = DashPhase.Dashing;

        UpdateVisualFeedback(dashColor);

        if (dashHitboxCollider != null)
        {
            dashHitboxCollider.enabled = true;
        }

        Vector2 direction = player != null ? (Vector2)(player.position - transform.position) : lastMoveDirection;
        direction.y = 0;
        dashDirection = direction.normalized;

        // Orientar el sprite (y con él, el offset del hitbox) hacia la dirección real
        // de la carga. Sin esto, el hitbox puede quedar apuntando al lado equivocado
        // si el jugador se acerca por el lado que el Dasher no estaba mirando.
        UpdateSpriteFlip(dashDirection.x);
        lastMoveDirection = dashDirection;

        dashStartPosition = rb != null ? rb.position : (Vector2)transform.position;

        float distanceToPlayer = player != null ? Vector2.Distance(transform.position, player.position) : 0f;
        dashTargetDistance = distanceToPlayer + dashOvershootBuffer;
    }

    private void UpdateDash()
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector2(dashDirection.x * dashSpeed, rb.linearVelocity.y);
        }
        else
        {
            transform.position += (Vector3)dashDirection * dashSpeed * Time.deltaTime;
        }

        // El golpe ya no se chequea acá: lo dispara OnTriggerEnter2D() en el instante
        // exacto del contacto. Acá solo quedan las otras dos condiciones de corte:
        // pasar de largo u obstáculo.
        if (CheckObstacleAhead())
        {
            EndDash();
            return;
        }

        Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;
        float distanceTravelled = Vector2.Distance(dashStartPosition, currentPosition);
        if (distanceTravelled >= dashTargetDistance)
        {
            EndDash();
        }
    }

    private bool CheckObstacleAhead()
    {
        if (obstacleCheck == null) return false;

        Vector2 checkPosition = (Vector2)obstacleCheck.position + dashDirection * obstacleCheckDistance;
        return Physics2D.OverlapCircle(checkPosition, obstacleCheckRadius, obstacleLayer);
    }

    private void EndDash()
    {
        dashPhase = DashPhase.Recovering;

        if (dashHitboxCollider != null)
        {
            dashHitboxCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        recoilTimer = 0f;
        attackTimer = attackCooldown;
    }

    private void UpdateRecovery()
    {
        if (recoilTimer > 0)
        {
            recoilTimer -= Time.deltaTime;
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(-dashDirection.x * recoilSpeed, rb.linearVelocity.y);
            }
        }
        else
        {
            FreezeMovement();
        }

        if (attackTimer <= 0)
        {
            isAttacking = false;
            // Sin fase de Chase: vuelve a Patrol y CheckForPlayer() decide de nuevo —
            // si el jugador sigue en rango, dispara otra carga inmediatamente.
            ChangeState(ChaserState.Patrol);
        }
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
            Debug.Log($"Dasher {gameObject.name} inicializado con {GetMaxHealth()} HP");
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Distancia a la que decide empezar la carga (CheckAttackRange se mide desde transform.position)
        Gizmos.color = new Color(1f, 0.5f, 0f, 1f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Caja de golpe real (el segundo Collider2D trigger, activo solo durante el dash)
        if (dashHitboxCollider is BoxCollider2D box)
        {
            Gizmos.color = Color.red;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

        if (obstacleCheck != null)
        {
            Gizmos.color = Color.magenta;
            Vector2 checkPos = Application.isPlaying
                ? (Vector2)obstacleCheck.position + dashDirection * obstacleCheckDistance
                : (Vector2)obstacleCheck.position;
            Gizmos.DrawWireSphere(checkPos, obstacleCheckRadius);
        }

        if (Application.isPlaying && dashPhase == DashPhase.Dashing)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(dashStartPosition, dashStartPosition + dashDirection * dashTargetDistance);
        }

        if (Application.isPlaying && player != null && debugLineOfSight)
        {
            Vector3 visionPos = visionOrigin != null ? visionOrigin.position : transform.position;
            Gizmos.color = hasLineOfSight ? Color.green : Color.red;
            Gizmos.DrawLine(visionPos, player.position);
        }
    }

    #endregion
}
