using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float speed = 5f;
    private Animator animator;
    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool facingRight = true; // Para voltear el sprite

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Buscar el Animator dentro del hijo "Sprite"
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Movimiento horizontal con A y D o flechas
        moveInput.x = Input.GetAxisRaw("Horizontal");

        // Cambiar animación dependiendo si se mueve o no
        bool isRunning = Mathf.Abs(moveInput.x) > 0;
        animator.SetBool("isRunning", isRunning);

        // Voltear sprite si cambia de dirección
        if (moveInput.x > 0 && !facingRight)
            Flip();
        else if (moveInput.x < 0 && facingRight)
            Flip();
    }

    void FixedUpdate()
    {
        // Movimiento horizontal
        rb.linearVelocity = new Vector2(moveInput.x * speed, rb.linearVelocity.y);
    }

    void Flip()
    {
        facingRight = !facingRight;
        // Invierte el sprite visualmente
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
