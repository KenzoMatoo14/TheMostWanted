using UnityEngine;

/// <summary>
/// Controla las animaciones del murci�lago (idle, ataque y muerte)
/// Este script debe agregarse al mismo GameObject que tiene EnemyEvilBat1
/// </summary>
[RequireComponent(typeof(EnemyEvilBat))]
[RequireComponent(typeof(Animator))]
public class BatAnimationController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator;

    [Header("Configuraci�n")]
    [SerializeField] private bool debugAnimations = false;

    private EnemyEvilBat enemyBat;
    private bool isDead = false;

    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                UnityEngine.Debug.LogError($"{gameObject.name}: No se encontr� Animator!");
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
            UnityEngine.Debug.Log($"{gameObject.name}: Animaci�n de muerte activada");
        }
    }

    // Getter p�blico por si necesitas verificar el estado
    public bool IsDead => isDead;
}