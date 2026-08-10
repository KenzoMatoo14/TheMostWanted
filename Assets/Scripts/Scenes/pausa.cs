using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class pausa : MonoBehaviour
{
    public GameObject pauseMenuUI;
    public static bool GameIsPaused = false;

    private InputAction pauseAction;

    void Awake()
    {
        pauseAction = new InputAction("Pause", type: InputActionType.Button, binding: "<Keyboard>/escape");
        pauseAction.performed += ctx => TogglePause();
    }

    void OnEnable()
    {
        pauseAction.Enable();
    }

    void OnDisable()
    {
        pauseAction.Disable();
    }

    // ===== FUNCIÓN PARA EL BOTÓN PLAY =====
    // Esta función reanuda el juego (igual que presionar ESC)
    public void OnPlayButtonPressed()
    {
        Resume();
    }

    // ===== FUNCIÓN PARA EL BOTÓN MAIN MENU =====
    // Esta función carga la escena del menú principal
    public void OnMainMenuButtonPressed()
    {
        LoadMenu("Menu");
    }

    public void TogglePause()
    {
        if (GameIsPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Resume()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false);
        }

        // Cancelamos cualquier hitstop en curso para que no nos pise el timeScale después
        HitStopManager.Instance.ForceStop();
        Time.timeScale = 1f;
        GameIsPaused = false;
    }

    public void Pause()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true);
        }

        // Cancelamos cualquier hitstop en curso para que no nos pise el timeScale después
        HitStopManager.Instance.ForceStop();
        Time.timeScale = 0f;
        GameIsPaused = true;
    }

    public void LoadMenu(string menuSceneName)
    {
        HitStopManager.Instance.ForceStop();
        Time.timeScale = 1f;
        GameIsPaused = false; // Asegura que el estado de pausa se resetee
        SceneManager.LoadScene(menuSceneName);
    }
}