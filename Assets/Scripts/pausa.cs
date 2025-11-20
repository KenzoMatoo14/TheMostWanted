using UnityEngine;
using UnityEngine.SceneManagement;
// IMPORTANTE: Esta línea es necesaria para usar el nuevo sistema de entrada
using UnityEngine.InputSystem; 

public class pausa : MonoBehaviour
{
    // Arrastra el objeto Panel_Pausa aquí desde el Inspector
    public GameObject pauseMenuUI; 
    
    // Variable para saber si estamos pausados o no. 
    public static bool GameIsPaused = false; 

    // Referencia al action para la pausa (puedes usar cualquier tecla, aquí usaremos Escape)
    // Se recomienda usar el mismo nombre que el Action Map de Unity (por ejemplo, "Pause")
    // Necesitas configurar un Input Action Asset o usar un control básico como se muestra abajo.
    private InputAction pauseAction;


    void Awake()
    {
        // 1. Inicializa el Input Action para la tecla Escape
        // Crea una acción que se active al presionar la tecla Escape.
        pauseAction = new InputAction("Pause", type: InputActionType.Button, binding: "<Keyboard>/escape");
        
        // 2. Asigna la función TogglePause al evento de que la tecla sea presionada
        pauseAction.performed += ctx => TogglePause();
    }

    void OnEnable()
    {
        // Habilita la acción de pausa cuando el script está activo
        pauseAction.Enable();
    }

    void OnDisable()
    {
        // Deshabilita la acción de pausa cuando el script está inactivo para ahorrar recursos
        pauseAction.Disable();
    }


    // --- Funciones Llamadas por los Botones y el Nuevo Input System ---

    // Función principal llamada por el botón de Pausa (y la tecla Escape) para alternar el estado
    public void TogglePause()
    {
        if (GameIsPaused)
        {
            Resume(); // Si está pausado, reanuda
        }
        else
        {
            Pause(); // Si no está pausado, pausa
        }
    }

    public void Resume()
    {
        // 1. Oculta el panel del menú de pausa (si existe)
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(false); 
        }
        
        // 2. Reanuda el tiempo normal
        Time.timeScale = 1f; 
        
        // 3. Marca el juego como no pausado
        GameIsPaused = false;
    }

    public void Pause()
    {
        // 1. Muestra el panel del menú de pausa (si existe)
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(true); 
        }
        
        // 2. Detiene el tiempo (0 = congela todo el juego)
        Time.timeScale = 0f; 

        // 3. Marca el juego como pausado
        GameIsPaused = true;
    }

    public void LoadMenu(string menuSceneName)
    {
        // 1. Asegura que el tiempo se reanude antes de cambiar de escena
        Time.timeScale = 1f;
        // 2. Carga la escena de menú especificada
        SceneManager.LoadScene(menuSceneName);
    }
}