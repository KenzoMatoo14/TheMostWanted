using UnityEngine;

public class EnemyEvilBat : MeleeChaserEnemy
{
    [Header("Random Patrol Settings")]
    [SerializeField] private float patrolRadius = 5f; // Radio desde la posición inicial
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

    [Header("Animation Controller")]
    [SerializeField] private BatAnimationController animationController;

    private Vector2 patrolCenter; // Centro de la patrulla
    private Vector2 currentPatrolTarget; // Punto actual al que se dirige

    private float flapTimer = 0f;
    private float horizontalWaveTimer = 0f;
    private float perlinNoiseOffset;
    private Vector3 waitingStartPosition;

    protected override void Start()
    {
        base.Start();

        if (animationController == null)
        {
            animationController = GetComponent<BatAnimationController>();
        }

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

        if (logBehaviorDetails)
        {
            Debug.Log($"Evil Bat {gameObject.name} inicializado. Centro de patrulla: {patrolCenter}");
        }
    }

    protected override void FreezeMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    protected override void OnEnterWaitingState()
    {
        waitingStartPosition = transform.position; // Guardar posición actual
        // Resetear los timers para un hover más suave
        flapTimer = 0f;
        horizontalWaveTimer = 0f;
    }

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
    protected override void PatrolBehavior()
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
        ChangeState(ChaserState.Waiting);
    }
    protected override void WaitBehavior()
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

        TickWaitTimer();
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

        Vector2 direction = (player.position - transform.position).normalized;
        float speed = chaseSpeed * GetMovementSpeedMultiplier();

        // Aplicar movimiento con aleteo (más agresivo en persecución)
        ApplyFlapMovement(direction, speed);

        lastMoveDirection = direction;
        UpdateSpriteFlip(direction.x);
    }

    #endregion

    #region Visual Feedback

    protected override void UpdateSpriteFlip(float directionX)
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

    protected override void OnDeathVisuals()
    {
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
