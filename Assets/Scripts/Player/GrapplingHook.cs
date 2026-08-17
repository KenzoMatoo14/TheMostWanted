using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControls controls;
    [SerializeField] private PlayerController character;
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
    private float lastHookTime = -999f;
    public bool canUseHook = true;

    void Awake()
    {
        controls = new PlayerControls();
        controls.Movement.FireWhip.performed += ctx => TryHook();
        controls.Movement.FireWhip.canceled += ctx => ReleaseHook();
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
        if (isHooked)
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
        // CircleCast en vez de un Raycast de una sola línea: le da un pequeño margen de
        // perdón a la puntería (no hace falta apuntar exacto al borde del objeto).
        RaycastHit2D hit = Physics2D.CircleCast(firePoint.position, stats.GrappleAimRadius, shootDirection, stats.MaxGrappleDistance, stats.GrappleLayer);

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

            // Frenar la velocidad radial (la que se aleja del radio), pero sin invertirla:
            // antes el factor llegaba hasta 1.7 (170%), lo que literalmente rebotaba al jugador
            // hacia adentro y comía todo el momentum del swing. Ahora se limita a como mucho 70%.
            Vector2 radialDirection = ropeVector.normalized;
            float radialVelocity = Vector2.Dot(rb.linearVelocity, radialDirection);

            if (radialVelocity > 0) // Si se está alejando
            {
                float reductionFactor = Mathf.Clamp01(overExtension * 0.5f); // Más reducción cuanto más lejos
                float radialDampFactor = Mathf.Clamp01(0.3f + reductionFactor * 0.4f); // rango 0.3 - 0.7
                rb.linearVelocity -= radialDirection * (radialVelocity * radialDampFactor);
            }
        }

        // 2. Aplicar fuerza tangencial para el columpiado
        Vector2 tangent = new Vector2(-ropeVector.y, ropeVector.x).normalized;

        // Input del jugador para controlar la dirección del swing (con intensidad ajustable)
        float horizontalInput = character != null ? character.moveInput.x : 0f;

        // 2b. Límite de ángulo: mide el ángulo actual respecto a "recto hacia abajo" del gancho.
        // Moverse en +tangent siempre rota ropeVector en sentido antihorario (así está construido
        // tangent), así que comparar el signo del ángulo con el signo del input/velocidad tangencial
        // nos dice si el swing está reforzando el lado en el que ya está inclinado (alejándose más)
        // o volviendo hacia el centro. Sin esto, el jugador puede dar vueltas completas alrededor
        // del gancho, lo que vuelve impredecible cualquier diseño de nivel de parkour.
        float currentAngle = Vector2.SignedAngle(Vector2.down, ropeVector);
        bool pastAngleLimit = Mathf.Abs(currentAngle) >= stats.MaxSwingAngle;
        bool inputReinforcesSameSide = horizontalInput != 0f && Mathf.Sign(currentAngle) == Mathf.Sign(horizontalInput);

        // Aplicar fuerza de swing del jugador, salvo que ya esté en el límite y siga empujando
        // para pasarse todavía más
        if (!(pastAngleLimit && inputReinforcesSameSide))
        {
            rb.AddForce(tangent * horizontalInput * stats.SwingForce, ForceMode2D.Force);
        }

        if (pastAngleLimit)
        {
            // Pared suave: empuja de vuelta hacia el límite y frena el momentum tangencial que ya
            // traía al jugador más allá (si no, la inercia lo llevaría igual a dar la vuelta aunque
            // ya no reciba más fuerza de input)
            float angleOverExtension = Mathf.Abs(currentAngle) - stats.MaxSwingAngle;
            float pushBackSign = -Mathf.Sign(currentAngle);
            rb.AddForce(tangent * pushBackSign * angleOverExtension * stats.ConstraintSpeed, ForceMode2D.Force);

            float tangentialVelocity = Vector2.Dot(rb.linearVelocity, tangent);
            bool movingFurtherOut = tangentialVelocity != 0f && Mathf.Sign(tangentialVelocity) == Mathf.Sign(currentAngle);
            if (movingFurtherOut)
            {
                rb.linearVelocity -= tangent * (tangentialVelocity * 0.6f);
            }
        }

        // 3. Amortiguación, expresada como fracción de velocidad retenida POR SEGUNDO (no por
        // tick de física). Antes se multiplicaba por stats.Dampening en cada FixedUpdate, y con
        // Fixed Timestep = 0.02 (50 ticks/seg) eso perdía ~92% de la velocidad cada segundo aunque
        // Dampening = 0.95 sonara a "poco freno". Con Pow(dampening, fixedDeltaTime), Dampening
        // ahora sí representa cuánta velocidad queda tras 1 segundo real.
        rb.linearVelocity *= Mathf.Pow(stats.Dampening, Time.fixedDeltaTime);

        // 4. Tope de velocidad: sin esto, bombear el input tangencial suma energía sin límite
        // (como columpiarse sin fricción), lo que permite acelerar indefinidamente hasta girar
        // sin control alrededor del gancho. Solo interviene en los extremos, no afecta el swing normal.
        if (rb.linearVelocity.magnitude > stats.MaxSwingSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * stats.MaxSwingSpeed;
        }
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