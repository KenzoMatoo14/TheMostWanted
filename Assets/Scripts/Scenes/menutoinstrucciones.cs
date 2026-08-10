using UnityEngine;
using UnityEngine.SceneManagement;

public class menutoinstrucciones : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Ir()
    {
        // Por si se navega estando en pausa (timeScale = 0) sin pasar por pausa.LoadMenu()
        Time.timeScale = 1f;
        pausa.GameIsPaused = false;

        SceneManager.LoadScene("Instrucciones");

        Debug.Log("ir a instrucciones...");
    }
}