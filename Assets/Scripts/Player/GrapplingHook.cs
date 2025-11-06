using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControls controls;
    [SerializeField] private CharacterController character;
    [SerializeField] private Transform firePoint;
    [SerializeField] private LineRenderer ropeRenderer;
    [SerializeField] private ScriptableStats stats;
    [SerializeField] private Camera playerCamera;

    private Rigidbody2D rb;
    private bool isHooked = false;
    private Vector2 hookPoint;
    private float ropeLength;
    private float targetRopeLength;
    private bool isPulling = false;
    private float swingDirection = 1f;
    private Vector2 moveInput;
    private float lastHookTime = -999f;
    public bool canUseHook = true;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Movement.FireWhip.performed += ctx => TryHook();
        controls.Movement.FireWhip.canceled += ctx => ReleaseHook();
        controls.Movement.Move.performed += ctx => moveInput = new Vector2(ctx.ReadValue<float>(), 0f);
        controls.Movement.Move.canceled += ctx => moveInput = Vector2.zero;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        ropeRenderer.enabled = false;
    }

    void OnEnable() => controls?.Enable();
    void OnDisable() => controls?.Disable();

    void Update()
    {
        if (isHooked)
        {
            DrawRope();
        }
    }

    void FixedUpdate()
    {
        if (isHooked && !isPulling)
        {
            ApplySwingPhysics();
        }
    }

    private Vector2 GetMouseDirection()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("No se encontró cámara para calcular dirección del mouse. Usando dirección por defecto.");
            return character.facingRight ? Vector2.one.normalized : new Vector2(-1, 1).normalized;
        }

        // Obtener posición del mouse en píxeles de pantalla
        Vector3 mouseScreenPos = Mouse.current.position.ReadValue();

        // Convertir a coordenadas del mundo
        Vector3 mouseWorldPos = playerCamera.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, playerCamera.nearClipPlane));

        // Calcular dirección desde el firePoint hacia el mouse
        Vector2 direction = ((Vector2)mouseWorldPos - (Vector2)firePoint.position).normalized;

        return direction;
    }

    void TryHook()
    {
        if (isHooked || !canUseHook) return;

        // Verificar cooldown
        if (Time.time - lastHookTime < stats.CooldownTime) return;

        Vector2 shootDirection = GetMouseDirection();
        RaycastHit2D hit = Physics2D.Raycast(firePoint.position, shootDirection, stats.MaxGrappleDistance, stats.GrappleLayer);

        if (hit.collider != null)
        {
            hookPoint = hit.point;
            ropeLength = Vector2.Distance(transform.position, hookPoint);
            targetRopeLength = ropeLength * stats.RopeShorten;

            Vector2 relativePos = (Vector2)transform.position - hookPoint;
            swingDirection = relativePos.x > 0 ? 1f : -1f;

            isHooked = true;
            ropeRenderer.enabled = true;
            character.canMove = false;

            StartCoroutine(PullEffect());
        }
    }

    public void ReleaseHook()
    {
        if (!isHooked) return;

        Vector2 launchVelocity = rb.linearVelocity;
        if (launchVelocity.magnitude > 2f)
        {
            rb.linearVelocity = launchVelocity * 1.1f;
        }

        isHooked = false;
        isPulling = false;
        ropeRenderer.enabled = false;
        character.canMove = true;

        lastHookTime = Time.time;
    }

    IEnumerator PullEffect()
    {
        isPulling = true;
        float startTime = Time.time;
        float originalLength = ropeLength;
        Vector2 pullDirection = (hookPoint - (Vector2)transform.position).normalized;

        rb.AddForce(pullDirection * stats.PullStrength, ForceMode2D.Impulse);

        while (Time.time - startTime < stats.PullTime)
        {
            float progress = (Time.time - startTime) / stats.PullTime;
            progress = Mathf.SmoothStep(0f, 1f, progress);
            ropeLength = Mathf.Lerp(originalLength, targetRopeLength, progress);
            yield return null;
        }

        ropeLength = targetRopeLength;
        isPulling = false;
    }

    void ApplySwingPhysics()
    {
        Vector2 playerPos = transform.position;
        Vector2 ropeVector = playerPos - hookPoint;
        float currentDistance = ropeVector.magnitude;

        // 1. Constrainir la distancia a la longitud de la cuerda (SUAVE PERO EFECTIVO)
        if (currentDistance > ropeLength)
        {
            // En lugar de teletransportar, aplicar fuerza hacia el punto correcto
            Vector2 targetPos = hookPoint + ropeVector.normalized * ropeLength;
            Vector2 constraintDirection = (targetPos - playerPos).normalized;

            // Calcular qué tan lejos estamos del radio deseado
            float overExtension = currentDistance - ropeLength;

            // Aplicar fuerza proporcional MÁS FUERTE para mantener el radio
            float constraintForceAmount = overExtension * stats.ConstraintSpeed * 2f; // 🔧 Duplicar la fuerza
            rb.AddForce(constraintDirection * constraintForceAmount, ForceMode2D.Force);

            // Eliminar la velocidad radial más agresivamente cuando está fuera del radio
            Vector2 radialDirection = ropeVector.normalized;
            float radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection);

            if (radialVelocity > 0) // Si se está alejando
            {
                // Reducir más agresivamente la velocidad radial
                float reductionFactor = Mathf.Clamp01(overExtension * 0.5f); // Más reducción cuanto más lejos
                rb.linearVelocity -= radialDirection * (radialVelocity * (0.7f + reductionFactor));
            }
        }

        // 2. Aplicar fuerza tangencial para el columpiado
        Vector2 tangent = new Vector2(-ropeVector.y, ropeVector.x).normalized;

        // Input del jugador para controlar la dirección del swing (con intensidad ajustable)
        float horizontalInput = moveInput.x;

        // Aplicar fuerza de swing
        rb.AddForce(tangent * horizontalInput * stats.SwingForce, ForceMode2D.Force);

        // 3. Aplicar amortiguación para que se sienta más natural
        rb.linearVelocity *= stats.Dampening;

        // 4. Agregar un poco de gravedad extra para mantener el momentum
        rb.AddForce(Vector2.down * 2f, ForceMode2D.Force);
    }

    void DrawRope()
    {
        if (!ropeRenderer.enabled) return;

        ropeRenderer.positionCount = 2;
        ropeRenderer.SetPosition(0, hookPoint);
        ropeRenderer.SetPosition(1, transform.position);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stats.MaxGrappleDistance);

        if (isHooked)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(hookPoint, 0.5f);

            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(hookPoint, ropeLength);

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(hookPoint, transform.position);

            Vector2 ropeVector = hookPoint - (Vector2)transform.position;
            Vector2 tangent = new Vector2(-ropeVector.y, ropeVector.x).normalized;
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, tangent * swingDirection * 2f);
        }
    }

    public bool IsHooked()
    {
        return isHooked;
    }

    public Vector2 GetHookPoint()
    {
        return hookPoint;
    }
}