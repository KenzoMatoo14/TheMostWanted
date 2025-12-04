using UnityEngine;
using UnityEngine.SceneManagement;

public class DeathManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathPanel;

    [Header("Player Reference")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Respawn Settings")]
    [SerializeField] private Transform respawnPoint;
    [SerializeField] private float respawnDelay = 0.5f;

    private void Start()
    {
        // Ocultar el panel de muerte al inicio
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }

        // Auto-encontrar PlayerStats si no está asignado
        if (playerStats == null)
        {
            playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogError("DeathManager: No se encontró PlayerStats en la escena!");
            }
        }

        // Suscribirse al evento de muerte
        if (playerStats != null)
        {
            playerStats.OnDeath.AddListener(OnPlayerDeath);
        }

        // Auto-encontrar el respawn point si no está asignado
        if (respawnPoint == null)
        {
            GameObject respawnObj = GameObject.FindGameObjectWithTag("Respawn");
            if (respawnObj != null)
            {
                respawnPoint = respawnObj.transform;
            }
            else
            {
                Debug.LogWarning("DeathManager: No se encontró punto de respawn. El jugador respawnará en su posición actual.");
            }
        }
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento al destruir
        if (playerStats != null)
        {
            playerStats.OnDeath.RemoveListener(OnPlayerDeath);
        }
    }

    /// <summary>
    /// Se llama cuando el jugador muere
    /// </summary>
    private void OnPlayerDeath()
    {
        // Mostrar el panel de muerte
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }

        Debug.Log("Panel de muerte mostrado");
    }

    /// <summary>
    /// Botón RETRY - Respawnea al jugador
    /// </summary>
    public void OnRetryButtonPressed()
    {
        Debug.Log("Retry presionado - Respawneando jugador...");

        // Reanudar el juego si estaba pausado
        Time.timeScale = 1f;

        // Ocultar el panel de muerte
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }

        // Respawnear al jugador
        StartCoroutine(RespawnPlayer());
    }

    /// <summary>
    /// Botón MAIN MENU - Vuelve al menú principal
    /// </summary>
    public void OnMainMenuButtonPressed()
    {
        Debug.Log("Volviendo al menú principal...");

        // Reanudar el tiempo antes de cambiar de escena
        Time.timeScale = 1f;

        // Cargar la escena del menú
        SceneManager.LoadScene("Menu"); // Cambia "Menu" por el nombre exacto de tu escena de menú
    }

    /// <summary>
    /// Corrutina que respawnea al jugador
    /// </summary>
    private System.Collections.IEnumerator RespawnPlayer()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (playerStats != null)
        {
            // Revivir al jugador
            playerStats.Revive();

            // Mover al jugador al punto de respawn
            if (respawnPoint != null)
            {
                playerStats.transform.position = respawnPoint.position;
                Debug.Log($"Jugador respawneado en: {respawnPoint.position}");
            }

            // Resetear la velocidad del Rigidbody2D
            Rigidbody2D rb = playerStats.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            Debug.Log("Jugador respawneado exitosamente");
        }
    }

    /// <summary>
    /// Permite cambiar el punto de respawn en runtime
    /// </summary>
    public void SetRespawnPoint(Transform newRespawnPoint)
    {
        respawnPoint = newRespawnPoint;
        Debug.Log($"Nuevo punto de respawn establecido en: {newRespawnPoint.position}");
    }

    /// <summary>
    /// Permite cambiar el punto de respawn por posición
    /// </summary>
    public void SetRespawnPoint(Vector3 position)
    {
        if (respawnPoint == null)
        {
            GameObject respawnObj = new GameObject("RespawnPoint");
            respawnPoint = respawnObj.transform;
        }
        respawnPoint.position = position;
        Debug.Log($"Nuevo punto de respawn establecido en: {position}");
    }
}