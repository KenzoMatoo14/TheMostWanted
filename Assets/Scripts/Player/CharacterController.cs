using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class CharacterController : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerControls controls;

    [Header("Stats")]
    public ScriptableStats stats;

    private Vector2 velocity = Vector2.zero;

    [HideInInspector] public bool canMove = true;

    [HideInInspector] public bool facingRight = true;
    [HideInInspector] public Vector2 moveInput;
    private bool jumpPressed;
    private bool isGrounded;
    private bool wasGrounded;

    [Header("Ground Check")]
    public Transform groundCheck;

    private int jumpCount = 0;

    private float timeLeftGrounded = -1f;
    private float lastJumpPressedTime = -1f;

    private bool consumedGroundJump = false;

    private bool isDashing;
    private float dashTime;
    private float dashCooldownTimer;
    private Vector2 dashDirection;
    private float dashSpeed;
    private float originalGravityScale;

    private GrapplingHook grapplingHook;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;
        controls = new PlayerControls();

        grapplingHook = GetComponent<GrapplingHook>();

        // Movement input
        controls.Movement.Move.performed += ctx => moveInput = new Vector2(ctx.ReadValue<float>(), 0f);
        controls.Movement.Move.canceled += ctx => moveInput = Vector2.zero;

        // Jump input
        controls.Movement.Jump.performed += ctx => lastJumpPressedTime = Time.time;
        controls.Movement.Jump.canceled += ctx => CutJump();

        // Dash input
        controls.Movement.Dash.performed += ctx => TryDash();

    }

    void OnEnable() => controls.Movement.Enable();
    void OnDisable() => controls.Movement.Disable();

    void Update()
    {
        if (!canMove && !grapplingHook.IsHooked()) return;

        // Ground check
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, stats.GroundCheckRadius, stats.GroundLayer);

        // Flip character if moving left/right
        if (moveInput.x > 0 && !facingRight) Flip();
        else if (moveInput.x < 0 && facingRight) Flip();

        if (wasGrounded && !isGrounded)
        {
            timeLeftGrounded = Time.time;
        }

        // Reset jumps only when landing (was in air last frame, now on ground)
        if (isGrounded && !wasGrounded)
        {
            jumpCount = stats.MaxJumps;
            consumedGroundJump = false;
        }

        // Store for next frame
        wasGrounded = isGrounded;

        bool wantsToJump = lastJumpPressedTime > 0 && Time.time < lastJumpPressedTime + stats.JumpBuffer;


        // Handle jump
        if (wantsToJump && !isDashing)
        {
            bool bufferedJump = (stats.JumpBuffer > 0f && Time.time < lastJumpPressedTime + stats.JumpBuffer);

            // Salto desde suelo o coyote time (NO gasta jumpCount)
            if (!consumedGroundJump && (isGrounded || (stats.CoyoteTime > 0f && Time.time < timeLeftGrounded + stats.CoyoteTime)))
            {
                Jump();
                consumedGroundJump = true;
                lastJumpPressedTime = -1f;
            }
            // Saltos en aire (solo si jumpCount > 0)
            else if (!isGrounded && jumpCount > 0)
            {
                Jump();
                jumpCount--;
                lastJumpPressedTime = -1f;
            }
        }

        // Handle dash timing
        if (isDashing)
        {
            rb.gravityScale = 0f; // cancel gravity while dashing

            dashTime -= Time.deltaTime;
            if (dashTime <= 0f)
            {
                isDashing = false;
                rb.gravityScale = originalGravityScale; // restore gravity
            }
        }

        if (dashCooldownTimer > 0f)
        {
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        if (!canMove && !grapplingHook.IsHooked()) return;

        if (isDashing)
        {
            if (grapplingHook.IsHooked())
            {
                grapplingHook.ReleaseHook();
            }

            // Aplicamos velocidad con inercia
            rb.linearVelocity = dashDirection * dashSpeed;

            // Reducimos la velocidad poco a poco (como Hollow Knight)
            dashSpeed = Mathf.MoveTowards(dashSpeed, 0f, stats.DashForce / stats.DashDuration * Time.fixedDeltaTime);

            // Control del tiempo de dash
            dashTime -= Time.fixedDeltaTime;
            if (dashTime <= 0f || dashSpeed <= 0.1f)
            {
                isDashing = false;
                rb.gravityScale = originalGravityScale; // restauramos gravedad
            }
            return; // salimos, no aplicamos movimiento normal
        }

        if (!grapplingHook.IsHooked())
        {
            // Apex modifier: mas velocidad horizontal en el pico del salto
            float apexModifier = 1f;
            if (!isGrounded)
            {
                float yVelocity = rb.linearVelocity.y;
                apexModifier = Mathf.Lerp(1f, stats.ApexBonus, 1f - Mathf.Abs(yVelocity) / stats.JumpForce);
            }

            // Horizontal movement con apex
            float targetSpeed = moveInput.x * stats.WalkSpeed * apexModifier;

            float accel = isGrounded ? stats.Acceleration : stats.AirAcceleration;
            float decel = isGrounded ? stats.Deceleration : stats.AirDeceleration;

            if (moveInput.x == 0)
                velocity.x = Mathf.MoveTowards(rb.linearVelocity.x, 0, decel * Time.fixedDeltaTime);
            else
                velocity.x = Mathf.MoveTowards(rb.linearVelocity.x, targetSpeed, accel * Time.fixedDeltaTime);

            rb.linearVelocity = new Vector2(velocity.x, rb.linearVelocity.y);
        }
    }

    private void Jump()
    {
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, stats.JumpForce);
    }
    private void CutJump()
    {
        if (rb.linearVelocity.y > 0f) // si está subiendo
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * stats.JumpCutMultiplier);
        }
    }
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
    private void TryDash()
    {
        if (!isDashing && dashCooldownTimer <= 0f)
        {
            isDashing = true;
            dashTime = stats.DashDuration;
            dashCooldownTimer = stats.DashCooldown;

            // Si está enganchado, hacer dash hacia el punto de enganche
            if (grapplingHook.IsHooked())
            {
                Vector2 hookPoint = grapplingHook.GetHookPoint();
                dashDirection = (hookPoint - (Vector2)transform.position).normalized;
            }
            else
            {
                // Comportamiento normal del dash
                if (moveInput.x != 0)
                {
                    dashDirection = new Vector2(moveInput.x, 0f).normalized;
                }
                else
                {
                    // If no input, dash in facing direction
                    dashDirection = new Vector2(facingRight ? 1f : -1f, 0f);
                }
            }

            dashSpeed = stats.DashForce;
        }
    }
    public bool IsGrounded()
    {
        return isGrounded;
    }
}
