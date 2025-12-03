using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    public float jumpForce = 7f;

    private Animator animator;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool facingRight = true;
    private bool isJumping = false;
    private float jumpAnimTimer = 0f; // 🔹 Controla cuánto dura la animación de salto

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // --- Movimiento horizontal (A / D) ---
        moveInput.x = Keyboard.current.aKey.isPressed ? -1 :
                      Keyboard.current.dKey.isPressed ? 1 : 0;

        // --- Saltar ---
        if (Keyboard.current.spaceKey.wasPressedThisFrame && !isJumping)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isJumping = true;
            jumpAnimTimer = 0.2f; // 🔹 Evita que cambie a "Caer" demasiado rápido
            animator.Play("Brincar", 0);
        }

        // --- Animaciones ---
        if (isJumping)
        {
            jumpAnimTimer -= Time.deltaTime;

            // 🔹 Si ya terminó el salto y empieza a caer
            if (jumpAnimTimer <= 0 && rb.linearVelocity.y < -0.1f)
            {
                if (!IsPlaying("Caer"))
                    animator.Play("Caer", 0);
            }

            // 🔹 Si ya aterrizó (velocidad vertical ~ 0)
            if (Mathf.Abs(rb.linearVelocity.y) < 0.01f)
            {
                isJumping = false;
            }
        }
        else
        {
            // 🔹 Movimiento en el suelo
            if (Mathf.Abs(moveInput.x) > 0)
            {
                if (!IsPlaying("Correr"))
                    animator.Play("Correr", 0);
            }
            else
            {
                if (!IsPlaying("Indle"))
                    animator.Play("Indle", 0);
            }
        }

        // --- Voltear sprite ---
        if (moveInput.x > 0 && !facingRight)
            Flip();
        else if (moveInput.x < 0 && facingRight)
            Flip();
    }

    void FixedUpdate()
    {
        // --- Movimiento físico ---
        rb.linearVelocity = new Vector2(moveInput.x * speed, rb.linearVelocity.y);
    }

    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    bool IsPlaying(string stateName)
    {
        var animState = animator.GetCurrentAnimatorStateInfo(0);
        return animState.IsName(stateName);
    }
}
