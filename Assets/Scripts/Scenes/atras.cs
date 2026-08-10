using UnityEngine;
using UnityEngine.SceneManagement;

public class atras : MonoBehaviour

{
    public void Instrucciones()
    {
        // Por si se navega estando en pausa (timeScale = 0) sin pasar por pausa.LoadMenu()
        Time.timeScale = 1f;
        pausa.GameIsPaused = false;

        SceneManager.LoadScene("Menu");

        Debug.Log("regresando a  menu...");
    }

}