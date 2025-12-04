using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// Gestiona el sistema de puntaje del juego
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Score Settings")]
    [SerializeField] private int currentScore = 0;
    [SerializeField] private int pointsPerCoin = 10; // Puntos que vale cada moneda
    [SerializeField] private int pointsPerEnemyKill = 100; // Puntos base por matar un enemigo

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;

    private static ScoreManager instance;

    void Awake()
    {
        // Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // Suscribirse al evento de monedas del CoinManager
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.OnCoinsChanged.AddListener(OnCoinsCollected);
        }

        // Actualizar UI inicial
        UpdateScoreUI();
    }

    /// <summary>
    /// Se llama cuando se recolectan monedas
    /// </summary>
    private void OnCoinsCollected(int totalCoins)
    {
        // Aquí puedes calcular el incremento basado en las monedas nuevas
        // o simplemente añadir puntos fijos por cada evento
        AddScore(pointsPerCoin);
    }

    /// <summary>
    /// Añade puntos al puntaje total
    /// </summary>
    public void AddScore(int points)
    {
        if (points <= 0) return;

        currentScore += points;
        Debug.Log($"Puntos añadidos: +{points}. Total: {currentScore}");

        UpdateScoreUI();
        OnScoreChanged?.Invoke(currentScore);
    }

    /// <summary>
    /// Se llama cuando un enemigo muere (llamar desde EnemyBase)
    /// </summary>
    public void OnEnemyKilled(int bonusPoints = 0)
    {
        int totalPoints = pointsPerEnemyKill + bonusPoints;
        AddScore(totalPoints);
    }

    /// <summary>
    /// Actualiza el texto de UI con el puntaje actual
    /// </summary>
    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = $"PTS: {currentScore}";
        }
    }

    /// <summary>
    /// Obtiene el puntaje actual
    /// </summary>
    public int GetScore()
    {
        return currentScore;
    }

    /// <summary>
    /// Reinicia el puntaje
    /// </summary>
    public void ResetScore()
    {
        currentScore = 0;
        UpdateScoreUI();
        OnScoreChanged?.Invoke(currentScore);
        Debug.Log("Puntaje reiniciado a 0");
    }

    /// <summary>
    /// Establece un puntaje específico
    /// </summary>
    public void SetScore(int score)
    {
        currentScore = Mathf.Max(0, score);
        UpdateScoreUI();
        OnScoreChanged?.Invoke(currentScore);
    }

    void OnDestroy()
    {
        // Desuscribirse del evento al destruir
        if (CoinManager.Instance != null)
        {
            CoinManager.Instance.OnCoinsChanged.RemoveListener(OnCoinsCollected);
        }
    }

    // Acceso estático
    public static ScoreManager Instance
    {
        get { return instance; }
    }
}