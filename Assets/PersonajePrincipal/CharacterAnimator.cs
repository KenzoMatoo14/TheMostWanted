using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private CharacterController controller;
    private PlayerStats stats;
    private GrapplingHook hook;

    private bool wasDead = false; // Para detectar cuando revive

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        controller = GetComponent<CharacterController>();
        rb = controller.GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();
        hook = GetComponent<GrapplingHook>();

        // Suscribirse al evento de cambio de vida para detectar revival
        if (stats != null)
        {
            stats.OnHealthChanged.AddListener(OnHealthChanged);
        }
    }

    void OnDestroy()
    {
        // Desuscribirse del evento
        if (stats != null)
        {
            stats.OnHealthChanged.RemoveListener(OnHealthChanged);
        }
    }

    private void OnHealthChanged(int newHealth)
    {
        // Si estaba muerto y ahora tiene vida, significa que revivió
        if (wasDead && newHealth > 0 && stats != null && !stats.IsDead())
        {
            ResetToIdle();
        }
    }

    void Update()
    {
        if (!animator || !controller || !rb) return;

        // ---- VERIFICAR ESTADO DE MUERTE ----
        bool isDead = stats != null && stats.IsDead();

        // Si está muerto, solo actualizar la animación de muerte y salir
        if (isDead)
        {
            animator.SetBool("isDeath", true);

            // Resetear todos los otros parámetros
            animator.SetBool("isRunning", false);
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
            animator.SetBool("isDashing", false);
            animator.SetBool("isHanging", false);

            wasDead = true;
            return;
        }

        // Si acaba de revivir, resetear
        if (wasDead && !isDead)
        {
            ResetToIdle();
        }

        wasDead = false;

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
        animator.SetBool("isDeath", false);

        // ---- APLICAR ANIMACIONES ----
        animator.SetBool("isRunning", isMoving && isGrounded);
        animator.SetBool("isJumping", isJumping && !isHanging);
        animator.SetBool("isFalling", isFalling && !isHanging);
        animator.SetBool("isDashing", isDashing && !isHanging);
        animator.SetBool("isHanging", isHanging);
    }

    /// <summary>
    /// Resetea el animator al estado Idle
    /// </summary>
    private void ResetToIdle()
    {
        if (animator == null) return;

        // Resetear todos los parámetros booleanos
        animator.SetBool("isDeath", false);
        animator.SetBool("isRunning", false);
        animator.SetBool("isJumping", false);
        animator.SetBool("isFalling", false);
        animator.SetBool("isDashing", false);
        animator.SetBool("isHanging", false);

        // Forzar la transición a Idle
        animator.Play("Indle", 0, 0f);

        Debug.Log("Animator reseteado a Idle");
    }

    /// <summary>
    /// Método público para resetear manualmente el animator (por si acaso)
    /// </summary>
    public void ForceResetToIdle()
    {
        ResetToIdle();
        wasDead = false;
    }
}