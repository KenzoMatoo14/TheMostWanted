using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EnemyDummy : EnemyBase
{
    [Header("Dummy Specific Settings")]
    [SerializeField] private bool logDamageDetails = true;

    [Header("Revival Settings")]
    [SerializeField] private bool canRevive = true;
    [SerializeField] private float revivalTime = 3f;
    [SerializeField] private bool showRevivalCountdown = true;

    [Header("Stun Bar UI")]
    [SerializeField] private Slider stunBar;
    [SerializeField] private bool autoFindStunBar = true;
    [SerializeField] private Image stunBarFillImage;
    [SerializeField] private Gradient stunBarGradient;
    [SerializeField] private bool hideWhenZero = true;

    // Variables para el sistema de revival
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isReviving = false;

    protected override void Start()
    {
        // Guardar posición inicial
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        base.Start();

        if (stunBar == null && autoFindStunBar)
        {
            FindStunBar();
        }
    }

    ////////////////////////////////// REVIVAL SYSTEM

    private IEnumerator RevivalCoroutine()
    {
        isReviving = true;
        float elapsedTime = 0f;

        if (showRevivalCountdown)
        {
            Debug.Log($"{gameObject.name} revivirá en {revivalTime} segundos...");
        }

        // Esperar el tiempo de revival
        while (elapsedTime < revivalTime)
        {
            elapsedTime += Time.deltaTime;

            if (showRevivalCountdown)
            {
                float timeLeft = revivalTime - elapsedTime;
                if (timeLeft > 0)
                {
                    // Puedes usar esto para actualizar UI si lo deseas
                    // Por ejemplo: revivalText.text = $"Reviviendo en: {timeLeft:F1}s";
                }
            }

            yield return null;
        }

        // Revivir el enemigo
        ReviveEnemy();
    }

    private void ReviveEnemy()
    {
        Debug.Log($"{gameObject.name} ¡Ha revivido!");

        // Restaurar posición y rotación inicial
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        // Restaurar estado del enemigo
        isDead = false;
        isReviving = false;
        isCaptured = false;
        isBeingCaptured = false;

        // Restaurar vida completa
        currentHealth = GetMaxHealth();

        // Limpiar efectos
        ClearStunned();
        CancelKnockback();

        // Resetear velocidades si hay Rigidbody2D
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // Actualizar UI
        if (healthBar != null)
        {
            healthBar.UpdateHealthBar(currentHealth, GetMaxHealth());
            healthBar.gameObject.SetActive(true);
        }

        // Reinicializar la stun bar
        InitializeStunBar();

        // Reactivar el GameObject si estaba desactivado
        gameObject.SetActive(true);

        // Llamar método personalizable para efectos de revival
        OnReviveCustom();
    }

    protected virtual void OnReviveCustom()
    {
        // Las clases hijas pueden sobrescribir para efectos de revival
        // Por ejemplo: animación, partículas, sonido, etc.
        if (logDamageDetails)
        {
            Debug.Log($"{gameObject.name} - Revival completo");
        }
    }

    ////////////////////////////////// STUNNED

    private void InitializeStunBar()
    {
        if (stunBar == null && autoFindStunBar)
        {
            FindStunBar();
        }

        if (stunBar != null)
        {
            stunBar.minValue = 0f;
            stunBar.maxValue = 100f;
            stunBar.value = 0f;

            if (stunBarFillImage == null)
            {
                stunBarFillImage = stunBar.fillRect?.GetComponent<Image>();
            }

            if (hideWhenZero)
            {
                SetStunBarVisibility(false);
            }

            Debug.Log($"StunBar inicializada en {gameObject.name}");
        }
        else
        {
            Debug.LogWarning($"No se encontró StunBar en {gameObject.name}");
        }
    }

    private void FindStunBar()
    {
        Transform stunBarTransform = transform.Find("StunBar");

        if (stunBarTransform == null)
        {
            Slider[] sliders = GetComponentsInChildren<Slider>(true);
            foreach (Slider slider in sliders)
            {
                if (slider.gameObject.name.Contains("Stun"))
                {
                    stunBar = slider;
                    break;
                }
            }
        }
        else
        {
            stunBar = stunBarTransform.GetComponent<Slider>();
        }
    }

    protected override void OnStunnedChangedCustom(float stunnedValue)
    {
        base.OnStunnedChangedCustom(stunnedValue);
        UpdateStunBar(stunnedValue);

        if (logDamageDetails)
        {
            //Debug.Log($"Dummy Stun actualizado: {stunnedValue:F1}%");
        }
    }

    private void UpdateStunBar(float stunnedValue)
    {
        if (stunBar == null) return;

        stunBar.value = stunnedValue;

        if (hideWhenZero)
        {
            SetStunBarVisibility(stunnedValue > 0);
        }

        if (stunBarFillImage != null && stunBarGradient != null)
        {
            float normalizedValue = stunnedValue / 100f;
            stunBarFillImage.color = stunBarGradient.Evaluate(normalizedValue);
        }
    }

    private void SetStunBarVisibility(bool visible)
    {
        if (stunBar != null)
        {
            stunBar.gameObject.SetActive(visible);
        }
    }

    public void SetStunBar(Slider newStunBar)
    {
        stunBar = newStunBar;
        InitializeStunBar();
    }

    ////////////////////////////////////////////////////

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();
        Debug.Log($"Dummy {gameObject.name} inicializado con {GetMaxHealth()} puntos de vida");
        InitializeStunBar();
    }

    protected override void OnDamageTakenCustom(int damageAmount)
    {
        base.OnDamageTakenCustom(damageAmount);

        if (logDamageDetails)
        {
            Debug.Log($"Dummy recibió {damageAmount} de daño específico");
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
            // Si no puede revivir, destruir después de un tiempo
            Destroy(gameObject, 2f);
        }
    }

    // Método público para resetear la posición inicial si es necesario
    public void SetInitialPosition(Vector3 position)
    {
        initialPosition = position;
    }

    public void SetInitialRotation(Quaternion rotation)
    {
        initialRotation = rotation;
    }

    // Método para cancelar el revival (útil si necesitas interrumpirlo)
    public void CancelRevival()
    {
        if (isReviving)
        {
            StopAllCoroutines();
            isReviving = false;
            Destroy(gameObject);
        }
    }

    // Getter para saber si está en proceso de revival
    public bool IsReviving()
    {
        return isReviving;
    }
}