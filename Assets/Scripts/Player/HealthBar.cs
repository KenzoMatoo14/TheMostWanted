using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image fillImage;

    [Header("Stats (ScriptableObject)")]
    [SerializeField] private Enemy enemyStats;
    [SerializeField] private ScriptableStats playerStats;

    [Header("Visual Settings")]
    [SerializeField] private Color fullHealthColor = Color.green;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private float lowHealthThreshold = 0.3f;
    [SerializeField] private bool smoothTransition = true;
    [SerializeField] private float transitionSpeed = 5f;

    private float targetHealthPercentage;
    private int maxHealth;
    private void Start()
    {
        // Auto-encontrar el slider si no está asignado
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
            if (healthSlider == null)
            {
                healthSlider = GetComponentInChildren<Slider>();
            }
        }

        // Auto-encontrar el fill image si no está asignado
        if (fillImage == null && healthSlider != null)
        {
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        DetermineMaxHealth();
        InitializeHealthBar();
    }
    private void Update()
    {
        if (smoothTransition && healthSlider != null)
        {
            // Transición suave de la barra de vida
            healthSlider.value = Mathf.Lerp(healthSlider.value, targetHealthPercentage,
                                           Time.deltaTime * transitionSpeed);
        }

        UpdateHealthBarColor();
    }
    private void DetermineMaxHealth()
    {
        // Prioridad: primero playerStats, luego enemyStats
        if (playerStats != null)
        {
            maxHealth = playerStats.maxHealth;
        }
        else if (enemyStats != null)
        {
            // Asumiendo que Enemy tiene un campo maxHealth
            // Ajusta esto según tu implementación de Enemy
            maxHealth = enemyStats.MaxHealth;
        }
        else
        {
            Debug.LogWarning("HealthBar: No se asignó ningún ScriptableObject de stats. Usando valor por defecto de 100.");
            maxHealth = 100;
        }
    }
    private void InitializeHealthBar()
    {
        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f; // Usaremos porcentajes (0 a 1)
            healthSlider.value = 1f; // Empezar en vida completa
        }

        targetHealthPercentage = 1f;
    }
    public void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0) return;

        float healthPercentage = (float)currentHealth / maxHealth;
        targetHealthPercentage = healthPercentage;

        if (!smoothTransition && healthSlider != null)
        {
            healthSlider.value = healthPercentage;
        }
    }
    private void UpdateHealthBarColor()
    {
        if (fillImage == null) return;

        float currentPercentage = healthSlider != null ? healthSlider.value : targetHealthPercentage;

        if (currentPercentage <= lowHealthThreshold)
        {
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor,
                                       currentPercentage / lowHealthThreshold);
        }
        else
        {
            fillImage.color = fullHealthColor;
        }
    }
    public void SetMaxHealth(int newMaxHealth)
    {
        maxHealth = newMaxHealth;
    }
    public int GetMaxHealth()
    {
        return maxHealth;
    }
}
