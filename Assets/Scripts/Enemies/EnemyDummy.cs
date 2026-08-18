using UnityEngine;
using System.Collections;

public class EnemyDummy : EnemyBase
{
    [Header("Dummy Specific Settings")]
    [SerializeField] private bool logDamageDetails = true;

    [Header("Revival Settings")]
    [SerializeField] private bool canRevive = true;
    [SerializeField] private float revivalTime = 3f;
    [SerializeField] private bool showRevivalCountdown = true;

    // Variables para el sistema de revival
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isReviving = false;

    protected override void Start()
    {
        // Guardar posici�n inicial
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        base.Start();
    }

    ////////////////////////////////// REVIVAL SYSTEM

    private IEnumerator RevivalCoroutine()
    {
        isReviving = true;
        float elapsedTime = 0f;

        if (showRevivalCountdown)
        {
            Debug.Log($"{gameObject.name} revivir� en {revivalTime} segundos...");
        }

        // Esperar el tiempo de revival
        while (elapsedTime < revivalTime)
        {
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Revivir el enemigo
        ReviveEnemy();
    }

    private void ReviveEnemy()
    {
        Debug.Log($"{gameObject.name} �Ha revivido!");

        // Restaurar posici�n y rotaci�n inicial
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // Restaurar estado del enemigo
        isDead = false;
        isReviving = false;
        isCaptured = false;
        isBeingCaptured = false;

        // Restaurar vida completa
        currentHealth = GetMaxHealth();

        // Reactivar componentes de IA que hayan quedado apagados por una captura
        ReenableAIComponents();

        // Limpiar efectos
        ClearStunned();
        CancelKnockback();

        // Resetear velocidades si hay Rigidbody2D
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Reactivar el GameObject si estaba desactivado
        gameObject.SetActive(true);

        // Llamar m�todo personalizable para efectos de revival
        OnReviveCustom();
    }

    protected virtual void OnReviveCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos de revival
        // Por ejemplo: animaci�n, part�culas, sonido, etc.
        if (logDamageDetails)
        {
            Debug.Log($"{gameObject.name} - Revival completo");
        }
    }

    ////////////////////////////////////////////////////

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();
        Debug.Log($"Dummy {gameObject.name} inicializado con {GetMaxHealth()} puntos de vida");
    }

    protected override void OnDamageTakenCustom(int damageAmount)
    {
        base.OnDamageTakenCustom(damageAmount);

        if (logDamageDetails)
        {
            Debug.Log($"Dummy recibi� {damageAmount} de da�o espec�fico");
        }
    }

    protected override void OnDeathCustom()
    {
        base.OnDeathCustom();

        Debug.Log("Dummy ejecutando muerte personalizada");

        // Verificar si puede revivir
        if (canRevive && !isReviving)
        {
            // Iniciar proceso de revival
            StartCoroutine(RevivalCoroutine());
        }
        else
        {
            // Si no puede revivir, destruir despu�s de un tiempo
            Destroy(gameObject, 2f);
        }
    }

    // M�todo p�blico para resetear la posici�n inicial si es necesario
    public void SetInitialPosition(Vector3 position)
    {
        initialPosition = position;
    }

    public void SetInitialRotation(Quaternion rotation)
    {
        initialRotation = rotation;
    }

    // M�todo para cancelar el revival (�til si necesitas interrumpirlo)
    public void CancelRevival()
    {
        if (isReviving)
        {
            StopAllCoroutines();
            isReviving = false;
            Destroy(gameObject);
        }
    }

    // Getter para saber si est� en proceso de revival
    public bool IsReviving()
    {
        return isReviving;
    }
}