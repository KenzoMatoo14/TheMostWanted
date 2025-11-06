using UnityEngine;
using UnityEngine.UI;

public class EnemyDummy : EnemyBase
{
    [Header("Dummy Specific Settings")]
    [SerializeField] private bool logDamageDetails = true;

    [Header("Stun Bar UI")]
    [SerializeField] private Slider stunBar;
    [SerializeField] private bool autoFindStunBar = true;
    [SerializeField] private Image stunBarFillImage; // Opcional: para cambiar color
    [SerializeField] private Gradient stunBarGradient; // Opcional: gradiente de colores
    [SerializeField] private bool hideWhenZero = true; // Ocultar cuando no hay stun
    protected override void Start()
    {
        base.Start();

        // Asegurar que la StunBar esté inicializada
        if (stunBar == null && autoFindStunBar)
        {
            FindStunBar();
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

            // Obtener el Image del fill si no está asignado
            if (stunBarFillImage == null)
            {
                stunBarFillImage = stunBar.fillRect?.GetComponent<Image>();
            }

            // Ocultar la barra al inicio si está configurado
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
        // Buscar por nombre
        Transform stunBarTransform = transform.Find("StunBar");

        if (stunBarTransform == null)
        {
            // Buscar en todos los hijos
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

        // Actualizar el valor
        stunBar.value = stunnedValue;

        // Mostrar/ocultar según configuración
        if (hideWhenZero)
        {
            SetStunBarVisibility(stunnedValue > 0);
        }

        // Actualizar color si hay gradiente configurado
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

        // Aquí puedes agregar comportamiento específico del dummy al recibir daño
        // Por ejemplo: cambiar color, reproducir sonido específico, etc.
    }
    protected override void OnDeathCustom()
    {
        base.OnDeathCustom();

        Debug.Log("Dummy ejecutando muerte personalizada");
        // Comportamiento específico de muerte del dummy
        // Por ejemplo: animación específica, efectos de partículas, etc.

        // Opcional: destruir el objeto después de un tiempo
        Destroy(gameObject, 2f);
    }
}