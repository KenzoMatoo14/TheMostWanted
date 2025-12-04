using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona el contador de monedas del jugador
/// </summary>
public class CoinManager : MonoBehaviour
{
    [Header("Coin Count")]
    [SerializeField] private int currentCoins = 0;

    [Header("Events")]
    public UnityEvent<int> OnCoinsChanged; // Se invoca cuando cambia el número de monedas

    private static CoinManager instance;

    void Awake()
    {
        // Singleton pattern para asegurar que solo hay un CoinManager
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // Opcional: mantener entre escenas
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Agrega monedas al contador
    /// </summary>
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;

        currentCoins += amount;
        Debug.Log($"Monedas recogidas: +{amount}. Total: {currentCoins}");

        // Invocar evento para actualizar UI
        OnCoinsChanged?.Invoke(currentCoins);
    }

    /// <summary>
    /// Resta monedas del contador (para sistema de tienda, etc.)
    /// </summary>
    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || currentCoins < amount)
        {
            Debug.Log("No hay suficientes monedas!");
            return false;
        }

        currentCoins -= amount;
        Debug.Log($"Monedas gastadas: -{amount}. Total: {currentCoins}");

        // Invocar evento para actualizar UI
        OnCoinsChanged?.Invoke(currentCoins);
        return true;
    }

    /// <summary>
    /// Obtiene la cantidad actual de monedas
    /// </summary>
    public int GetCoins()
    {
        return currentCoins;
    }

    /// <summary>
    /// Reinicia el contador de monedas
    /// </summary>
    public void ResetCoins()
    {
        currentCoins = 0;
        OnCoinsChanged?.Invoke(currentCoins);
        Debug.Log("Monedas reiniciadas a 0");
    }

    /// <summary>
    /// Establece un valor específico de monedas
    /// </summary>
    public void SetCoins(int amount)
    {
        currentCoins = Mathf.Max(0, amount);
        OnCoinsChanged?.Invoke(currentCoins);
    }

    // Acceso estático para facilitar el uso desde otros scripts
    public static CoinManager Instance
    {
        get { return instance; }
    }
}