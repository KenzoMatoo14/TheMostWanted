using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private CharacterController controller;
    private PlayerStats stats;
    private GrapplingHook hook;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        rb = controller.GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        hook = GetComponent<GrapplingHook>();
    }

    void Update()
    {
        if (!animator || !controller || !rb) return;

        // ---- ESTADOS BÁSICOS ----
        bool isGrounded = controller.IsGrounded();
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        bool isJumping = !isGrounded && rb.linearVelocity.y > 0.1f;
        bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f;

        // ---- DASH ----
        bool isDashing = rb.linearVelocity.magnitude > controller.stats.WalkSpeed * 1.2f;

        // ---- HOOK (colgado) ----
        bool isHanging = hook != null && hook.IsHooked();

        // ---- MUERTE ----
        animator.SetBool("isDeath", stats != null && stats.IsDead());

        // ---- APLICAR ANIMACIONES ----
        animator.SetBool("isRunning", isMoving && isGrounded);
        animator.SetBool("isJumping", isJumping && !isHanging);
        animator.SetBool("isFalling", isFalling && !isHanging);
        animator.SetBool("isDashing", isDashing && !isHanging);
        animator.SetBool("isHanging", isHanging);
    }
}