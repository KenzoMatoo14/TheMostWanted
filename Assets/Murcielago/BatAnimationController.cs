using UnityEngine;

/// <summary>
/// Controla las animaciones del murciélago (idle, ataque y muerte)
/// Este script debe agregarse al mismo GameObject que tiene EnemyEvilBat1
/// </summary>
[RequireComponent(typeof(EnemyEvilBat))]
[RequireComponent(typeof(Animator))]
public class BatAnimationController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;

    [Header("Configuración")]
    [SerializeField] private bool debugAnimations = false;

    private EnemyEvilBat enemyBat;
    private bool isDead = false;
    private bool isAttacking = false;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                UnityEngine.Debug.LogError($"{gameObject.name}: No se encontró Animator!");
                enabled = false;
                return;
            }
        }

        enemyBat = GetComponent<EnemyEvilBat>();

        if (debugAnimations)
        {
            UnityEngine.Debug.Log($"{gameObject.name}: BatAnimationController inicializado");
        }
    }

    public void TriggerDeath()
    {
        isDead = true;
        animator.SetBool("isDead", true);

        if (debugAnimations)
        {
            UnityEngine.Debug.Log($"{gameObject.name}: Animación de muerte activada");
        }
    }

    /// <summary>
    /// Resetea las animaciones
    /// </summary>
    public void ResetAnimations()
    {
        isDead = false;
        isAttacking = false;
        animator.SetBool("isDead", false);
    }

    /// <summary>
    /// Fuerza la activación manual de la animación idle (opcional)
    /// </summary>
    public void ForceIdle()
    {
        if (!isDead && !isAttacking)
        {
            animator.Play("VolarMurcielago"); // Reemplaza con el nombre exacto de tu animación idle
        }
    }

    // Getters públicos por si necesitas verificar el estado
    public bool IsDead => isDead;
    public bool IsAttacking => isAttacking;
}