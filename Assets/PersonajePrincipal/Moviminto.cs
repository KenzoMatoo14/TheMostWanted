using UnityEngine;
using UnityEngine.InputSystem; // <- necesario para el nuevo Input System

public class Moviminto : MonoBehaviour
{
    private Animator animator;
    public ScriptableStats stats;
    private Vector2 moveInput;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // Detectar movimiento con el nuevo Input System
        moveInput = Vector2.zero;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            moveInput.x = -1;
        else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            moveInput.x = 1;

        // Determinar si se está moviendo
        bool isRunning = Mathf.Abs(moveInput.x) > 0.1f || Mathf.Abs(moveInput.y) > 0.1f;

        // Actualizar animación
        animator.SetBool("isRunning", isRunning);
    }
}