using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private CharacterController controller;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        rb = controller.GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        bool isGrounded = controller.IsGrounded();
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f;
        bool isJumping = !isGrounded && rb.linearVelocity.y > 0.1f;
        bool isFalling = !isGrounded && rb.linearVelocity.y < -0.1f;

        // Detección simple de dash
        bool isDashing = rb.linearVelocity.magnitude > controller.stats.WalkSpeed * 1.2f;

        animator.SetBool("isRunning", isMoving && isGrounded);
        animator.SetBool("isJumping", isJumping);
        animator.SetBool("isFalling", isFalling);
        animator.SetBool("isDashing", isDashing);
    }
}
