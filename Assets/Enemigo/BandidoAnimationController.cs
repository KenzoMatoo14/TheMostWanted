using UnityEngine;

public class BandidoAnimationController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody2D rb;
    private EnemyBandido enemy;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        // Buscar componentes en el padre (donde está el enemigo principal)
        rb = GetComponentInParent<Rigidbody2D>();
        enemy = GetComponentInParent<EnemyBandido>();

        // Validaciones
        if (animator == null)
        {
            Debug.LogError($"{gameObject.name}: ¡No hay Animator component!");
            return;
        }

        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError($"{gameObject.name}: ¡El Animator NO tiene un Animator Controller asignado!");
            return;
        }

        if (rb == null)
        {
            Debug.LogError($"{gameObject.name}: ¡No se encontró Rigidbody2D en el padre!");
            Debug.LogError("Asegúrate de que este script esté en un hijo del GameObject con Rigidbody2D");
        }
        else
        {
            Debug.Log($"✅ Rigidbody2D encontrado en: {rb.gameObject.name}");
        }

        if (enemy == null)
        {
            Debug.LogError($"{gameObject.name}: ¡No se encontró EnemyBandido en el padre!");
            Debug.LogError("Asegúrate de que este script esté en un hijo del GameObject con EnemyBandido");
        }
        else
        {
            Debug.Log($"✅ EnemyBandido encontrado en: {enemy.gameObject.name}");
        }

        // Verificar SpriteRenderer
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"{gameObject.name}: No hay SpriteRenderer en este GameObject");
        }
        else
        {
            Debug.Log($"✅ SpriteRenderer encontrado correctamente");
        }

        // Verificar animaciones
        if (animator.runtimeAnimatorController != null)
        {
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            Debug.Log($"📊 {clips.Length} animaciones cargadas");
        }
    }

    private void Update()
    {
        if (animator == null || enemy == null || rb == null) return;

        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        // ===== PRIORIDAD 1: MUERTE (siempre tiene máxima prioridad) =====
        if (enemy.isDead)
        {
            animator.SetBool("isDead", true);
            // Desactivar todos los demás parámetros cuando está muerto
            animator.SetBool("isRunning", false);
            animator.SetBool("isJumping", false);
            animator.SetBool("isFalling", false);
            animator.SetBool("isAttacking", false);

            if (showDebugLogs) Debug.Log($"{gameObject.name}: Estado MUERTE activado");
            return; // No procesar más animaciones si está muerto
        }
        else
        {
            animator.SetBool("isDead", false);
        }

        // ===== PRIORIDAD 2: ATAQUE =====
        bool isAttacking = enemy.isAttacking;
        animator.SetBool("isAttacking", isAttacking);

        if (isAttacking)
        {
            // Durante ataque, desactivar movimiento
            animator.SetBool("isRunning", false);
            if (showDebugLogs) Debug.Log($"{enemy.gameObject.name}: Estado ATAQUE activado");
            return; // No procesar movimiento durante ataque
        }

        // ===== PRIORIDAD 3: ESTADOS AÉREOS (Salto/Caída) =====
        bool isGrounded = enemy.isGrounded;
        float verticalSpeed = rb.linearVelocity.y;

        bool isJumping = !isGrounded && verticalSpeed > 0.1f;
        bool isFalling = !isGrounded && verticalSpeed < -0.1f;

        animator.SetBool("isJumping", isJumping);
        animator.SetBool("isFalling", isFalling);

        // Si está en el aire, no mostrar animación de correr
        if (isJumping || isFalling)
        {
            animator.SetBool("isRunning", false);
            if (showDebugLogs)
                Debug.Log($"{enemy.gameObject.name}: Estado AÉREO - Jump:{isJumping} Fall:{isFalling}");
            return;
        }

        // ===== PRIORIDAD 4: MOVIMIENTO HORIZONTAL =====
        float horizontalSpeed = Mathf.Abs(rb.linearVelocity.x);
        bool isRunning = horizontalSpeed > 0.1f && isGrounded;

        animator.SetBool("isRunning", isRunning);

        if (showDebugLogs && isRunning)
        {
            Debug.Log($"{enemy.gameObject.name}: Estado CORRIENDO - Speed: {horizontalSpeed:F2}");
        }
    }

    // Método opcional para forzar un estado específico
    public void ForceIdleState()
    {
        if (animator == null) return;

        animator.SetBool("isRunning", false);
        animator.SetBool("isJumping", false);
        animator.SetBool("isFalling", false);
        animator.SetBool("isAttacking", false);
        // No tocar isDead
    }

    // Método para debug manual
    public void PrintCurrentState()
    {
        if (animator == null) return;

        Debug.Log($"=== {gameObject.name} Animation State ===");
        Debug.Log($"isDead: {animator.GetBool("isDead")}");
        Debug.Log($"isAttacking: {animator.GetBool("isAttacking")}");
        Debug.Log($"isJumping: {animator.GetBool("isJumping")}");
        Debug.Log($"isFalling: {animator.GetBool("isFalling")}");
        Debug.Log($"isRunning: {animator.GetBool("isRunning")}");
    }

    // Método para debug visual en el editor
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && enemy != null && rb != null)
        {
            // Mostrar velocidad
            Vector3 pos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(pos, $"Speed: {rb.linearVelocity.x:F2}");
        }
    }
}