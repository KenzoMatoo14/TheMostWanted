using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ataque de látigo dirigido al mouse (estilo ScourgeBringer).
/// Vive en el root del jugador. El látigo es el hijo "WhipPivot" (SpriteRenderer + Animator +
/// PlayerAnimationEventRelay). Los tiempos (hitbox, ventana de cancelación, fin) los marcan
/// los Animation Events del clip WhipSlash, generados por Tools/Setup Whip Attack.
/// El daño, stun, partículas y lastAttackedEnemy se reutilizan desde CharacterCombat.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class WhipAttack : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform whipPivot;
    [SerializeField] private SpriteRenderer whipRenderer;
    [SerializeField] private Animator whipAnimator;
    [SerializeField] private Animator bodyAnimator;

    [Header("Estados del Animator")]
    [SerializeField] private string bodyAttackState = "WhipWindup";
    [SerializeField] private string whipAttackState = "WhipSlash";

    [Header("Dirección")]
    [Tooltip("Ángulo (grados) al que apunta la punta del látigo en el frame del chasquido, con el sprite sin rotar. " +
             "Se compensa para que el chasquido quede exactamente en dirección del mouse.")]
    [SerializeField] private float snapTipAngle = 14.7f;
    [Tooltip("Redondea la rotación del sprite a múltiplos de snapStep (para que los pixeles rotados se vean mejor).")]
    [SerializeField] private bool snapAngle = false;
    [SerializeField] private float snapStep = 22.5f;

    [Header("Hitbox (en pixeles del sprite, se convierten con el PPU)")]
    [SerializeField] private float pixelsPerUnit = 48f;
    [SerializeField] private float hitboxStartPx = 30f;
    [SerializeField] private float hitboxEndPx = 140f;
    [SerializeField] private float hitboxRadiusPx = 12f;

    [Header("Combate")]
    [SerializeField] private int damage = 10;
    [Tooltip("Tiempo después de que termina un ataque completo antes de poder atacar de nuevo. Cancelar en la recuperación lo ignora.")]
    [SerializeField] private float cooldown = 0.05f;
    [Tooltip("Si se hace click durante el ataque antes de la ventana de cancelación, se guarda este tiempo y se dispara al abrirse la ventana.")]
    [SerializeField] private float inputBufferTime = 0.15f;

    [Header("Rebote al golpear hacia abajo (pogo)")]
    [SerializeField] private bool enablePogo = true;
    [Tooltip("Velocidad vertical del rebote. Como referencia, JumpForce en PlayerStats es 17.5.")]
    [SerializeField] private float pogoForce = 15f;
    [Tooltip("Qué tan lejos de 'directo hacia abajo' puede apuntar el golpe y seguir contando como pogo (grados).")]
    [SerializeField][Range(0f, 90f)] private float pogoAngle = 45f;
    [SerializeField] private bool pogoRefreshesDash = true;
    [SerializeField] private bool pogoRefreshesAirJumps = true;

    [Header("Debug")]
    [SerializeField] private bool showHitbox = true;

    private PlayerController playerController;
    private CharacterCombat combat;
    private GrapplingHook grapplingHook;
    private Camera mainCamera;

    private int bodyStateHash;
    private int whipStateHash;

    private bool isAttacking;
    private bool hitboxActive;
    private bool canCancel;
    private bool pendingEnd;
    private bool hitStopDoneThisAttack;
    private bool pogoDoneThisAttack;
    private float cooldownEndTime;
    private float bufferedInputTime = -1f;

    private Vector2 aimDirection = Vector2.right;   // dirección real del chasquido (hacia el mouse)
    private float spriteAngle;                      // rotación aplicada al sprite del látigo (mundo)
    private bool aimingLeft;

    private readonly HashSet<IDamageable> hitThisAttack = new HashSet<IDamageable>();
    private readonly List<Collider2D> overlapResults = new List<Collider2D>();
    private ContactFilter2D enemyFilter;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        combat = GetComponent<CharacterCombat>();
        grapplingHook = GetComponent<GrapplingHook>();
        mainCamera = Camera.main;

        if (whipPivot == null) whipPivot = transform.Find("WhipPivot");
        if (whipPivot != null)
        {
            if (whipRenderer == null) whipRenderer = whipPivot.GetComponent<SpriteRenderer>();
            if (whipAnimator == null) whipAnimator = whipPivot.GetComponent<Animator>();
        }
        if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();

        if (whipPivot == null || whipRenderer == null || whipAnimator == null)
        {
            Debug.LogError("WhipAttack: falta WhipPivot (con SpriteRenderer y Animator) como hijo del jugador.");
        }

        bodyStateHash = Animator.StringToHash(bodyAttackState);
        whipStateHash = Animator.StringToHash(whipAttackState);

        enemyFilter = new ContactFilter2D();
        enemyFilter.useTriggers = true;
        if (combat != null && combat.Stats != null)
        {
            enemyFilter.SetLayerMask(combat.Stats.EnemyLayers);
        }

        if (whipPivot != null) whipPivot.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!isAttacking) return;

        // El dash (o morir / quedar sin control) interrumpe el ataque
        if (playerController.IsDashing || !CanAct())
        {
            CancelAttack();
            return;
        }

        // Click guardado antes de la ventana de cancelación
        if (canCancel && bufferedInputTime >= 0f && Time.time <= bufferedInputTime + inputBufferTime)
        {
            bufferedInputTime = -1f;
            StartAttack();
            return;
        }

        if (hitboxActive)
        {
            CheckHits();
        }

        // Respaldo por si el evento WhipEnd no llegara a dispararse
        if (!pendingEnd && whipAnimator != null)
        {
            AnimatorStateInfo info = whipAnimator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == whipStateHash && info.normalizedTime >= 1f)
            {
                pendingEnd = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (!isAttacking) return;

        if (pendingEnd)
        {
            FinishAttack();
            return;
        }

        // Se re-aplica cada frame por si algo cambió la escala del padre
        ApplyWhipTransform();
    }

    ///////////////////////// INPUT

    /// <summary>Llamado por CharacterCombat cuando se presiona el ataque.</summary>
    public void TryAttack()
    {
        if (!CanAct() || playerController.IsDashing) return;

        if (isAttacking)
        {
            if (canCancel)
            {
                StartAttack();
            }
            else
            {
                bufferedInputTime = Time.time;
            }
            return;
        }

        if (Time.time < cooldownEndTime) return;

        StartAttack();
    }

    private bool CanAct()
    {
        return playerController.canMove || (grapplingHook != null && grapplingHook.IsHooked());
    }

    ///////////////////////// ATAQUE

    private void StartAttack()
    {
        if (whipPivot == null || whipAnimator == null) return;

        // 1) Dirección hacia el mouse, bloqueada para todo el ataque
        aimDirection = GetMouseDirection();
        aimingLeft = aimDirection.x < 0f;

        // 2) Ángulo del sprite: se resta el ángulo de la punta en el chasquido para que el
        //    chasquido (no el sprite base) apunte al mouse. Con flipY la punta queda espejada.
        float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        float tipOffset = aimingLeft ? -snapTipAngle : snapTipAngle;
        spriteAngle = aimAngle - tipOffset;

        if (snapAngle && snapStep > 0f)
        {
            spriteAngle = Mathf.Round(spriteAngle / snapStep) * snapStep;
            float snappedAim = (spriteAngle + tipOffset) * Mathf.Deg2Rad;
            aimDirection = new Vector2(Mathf.Cos(snappedAim), Mathf.Sin(snappedAim));
        }

        // 3) Orientar el cuerpo con el Flip existente y bloquear el giro por input
        playerController.FaceDirection(!aimingLeft);
        playerController.facingLocked = true;

        isAttacking = true;
        hitboxActive = false;
        canCancel = false;
        pendingEnd = false;
        hitStopDoneThisAttack = false;
        pogoDoneThisAttack = false;
        bufferedInputTime = -1f;
        hitThisAttack.Clear();

        whipPivot.gameObject.SetActive(true);
        whipRenderer.flipY = aimingLeft;
        ApplyWhipTransform();

        // 4) Las dos animaciones desde el inicio
        whipAnimator.Play(whipStateHash, 0, 0f);
        whipAnimator.Update(0f);
        if (bodyAnimator != null)
        {
            bodyAnimator.Play(bodyStateHash, 0, 0f);
        }
    }

    /// <summary>
    /// WhipPivot es hijo de un root que se espeja con localScale.x = -1. Para que el látigo no
    /// herede el espejo, se contrarresta la escala y se invierte el ángulo local
    /// (S(-1)·R(a)·S(-1) = R(-a)), así el resultado en mundo siempre es R(spriteAngle).
    /// </summary>
    private void ApplyWhipTransform()
    {
        float parentSign = Mathf.Sign(transform.lossyScale.x);
        whipPivot.localScale = new Vector3(parentSign, 1f, 1f);
        whipPivot.localRotation = Quaternion.Euler(0f, 0f, spriteAngle * parentSign);
    }

    private Vector2 GetMouseDirection()
    {
        Vector2 fallback = new Vector2(playerController.facingRight ? 1f : -1f, 0f);
        if (mainCamera == null) mainCamera = Camera.main;
        if (Mouse.current == null || mainCamera == null) return fallback;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0f));
        Vector2 dir = (Vector2)mouseWorld - (Vector2)whipPivot.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : fallback;
    }

    private void FinishAttack()
    {
        EndAttackState();
        cooldownEndTime = Time.time + cooldown;
    }

    private void CancelAttack()
    {
        EndAttackState();
    }

    private void EndAttackState()
    {
        isAttacking = false;
        hitboxActive = false;
        canCancel = false;
        pendingEnd = false;
        bufferedInputTime = -1f;
        hitThisAttack.Clear();
        playerController.facingLocked = false;

        if (whipPivot != null) whipPivot.gameObject.SetActive(false);
        // El cuerpo vuelve solo a Idle por la transición con Exit Time de WhipWindup
    }

    ///////////////////////// HITBOX

    private void GetHitbox(out Vector2 center, out Vector2 size, out float angle)
    {
        float start = hitboxStartPx / pixelsPerUnit;
        float end = hitboxEndPx / pixelsPerUnit;
        float radius = hitboxRadiusPx / pixelsPerUnit;

        Vector2 origin = whipPivot.position;
        center = origin + aimDirection * ((start + end) * 0.5f);
        size = new Vector2(Mathf.Max(end - start, radius * 2f), radius * 2f);
        angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
    }

    private void CheckHits()
    {
        if (combat == null) return;

        GetHitbox(out Vector2 center, out Vector2 size, out float angle);
        Physics2D.OverlapCapsule(center, size, CapsuleDirection2D.Horizontal, angle, enemyFilter, overlapResults);

        bool hitSomething = false;
        foreach (Collider2D hit in overlapResults)
        {
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable == null || damageable.IsDead() || hitThisAttack.Contains(damageable)) continue;

            hitThisAttack.Add(damageable);
            combat.ApplyWhipHit(hit, damageable, damage, whipPivot.position, aimDirection, center);
            hitSomething = true;
        }

        if (hitSomething && !hitStopDoneThisAttack && combat.Stats != null && combat.Stats.HitStopDuration > 0f)
        {
            hitStopDoneThisAttack = true;
            HitStopManager.Instance.DoHitStop(combat.Stats.HitStopDuration);
        }

        if (hitSomething)
        {
            TryPogo();
        }
    }

    /// <summary>
    /// Rebote estilo Hollow Knight: solo al golpear algo, solo en el aire, solo si el golpe
    /// apunta hacia abajo y una vez por ataque. Durante el hitstop la física está en pausa,
    /// así que el impulso arranca justo cuando termina la pausa (golpe → pausa → salto).
    /// Estando enganchado no se aplica, para no pelear con el balanceo del gancho.
    /// </summary>
    private void TryPogo()
    {
        if (!enablePogo || pogoDoneThisAttack) return;
        if (playerController.IsGrounded()) return;
        if (grapplingHook != null && grapplingHook.IsHooked()) return;
        if (Vector2.Angle(aimDirection, Vector2.down) > pogoAngle) return;

        pogoDoneThisAttack = true;
        playerController.Pogo(pogoForce, pogoRefreshesDash, pogoRefreshesAirJumps);
    }

    ///////////////////////// ANIMATION EVENTS (vía PlayerAnimationEventRelay en WhipPivot)

    public void OnWhipHitboxOn()
    {
        if (!isAttacking) return;
        hitboxActive = true;
        if (combat != null) combat.PlayWhipSound();
        CheckHits(); // el frame del chasquido golpea en el mismo instante
    }

    public void OnWhipHitboxOff()
    {
        hitboxActive = false;
    }

    public void OnWhipOpenCancel()
    {
        if (isAttacking) canCancel = true;
        // El reinicio por input guardado se hace en Update, nunca dentro del evento del Animator
    }

    public void OnWhipEnd()
    {
        if (isAttacking) pendingEnd = true;
    }

    ///////////////////////// GIZMOS

    private void OnDrawGizmosSelected()
    {
        if (!showHitbox || whipPivot == null) return;

        if (!Application.isPlaying)
        {
            aimDirection = Vector2.right;
        }

        GetHitbox(out Vector2 center, out Vector2 size, out float angle);
        Gizmos.color = hitboxActive ? new Color(1f, 0f, 0f, 0.9f) : new Color(1f, 0.6f, 0f, 0.5f);

        Vector2 axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float radius = size.y * 0.5f;
        float halfSegment = Mathf.Max(0f, size.x * 0.5f - radius);
        Vector2 a = center - axis * halfSegment;
        Vector2 b = center + axis * halfSegment;
        Vector2 normal = new Vector2(-axis.y, axis.x) * radius;

        Gizmos.DrawWireSphere(a, radius);
        Gizmos.DrawWireSphere(b, radius);
        Gizmos.DrawLine(a + normal, b + normal);
        Gizmos.DrawLine(a - normal, b - normal);
        Gizmos.DrawLine(whipPivot.position, center);

        // Cono de direcciones que cuentan como pogo
        if (enablePogo)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
            float reach = hitboxEndPx / pixelsPerUnit;
            Vector3 origin = whipPivot.position;
            Vector3 left = Quaternion.Euler(0f, 0f, -pogoAngle) * Vector3.down * reach;
            Vector3 right = Quaternion.Euler(0f, 0f, pogoAngle) * Vector3.down * reach;
            Gizmos.DrawLine(origin, origin + left);
            Gizmos.DrawLine(origin, origin + right);
        }
    }
}
