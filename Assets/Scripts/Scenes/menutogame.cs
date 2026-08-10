using UnityEngine;
using UnityEngine.SceneManagement;

public class menutogame : MonoBehaviour
{
    public void IniciarJuego()
    {
        // Por si se navega estando en pausa (timeScale = 0) sin pasar por pausa.LoadMenu()
        Time.timeScale = 1f;
        pausa.GameIsPaused = false;

        SceneManager.LoadScene("Game");

        Debug.Log("Iniciando Juego...");
    }

}