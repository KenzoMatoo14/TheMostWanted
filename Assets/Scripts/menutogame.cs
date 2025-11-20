using UnityEngine;
using UnityEngine.SceneManagement;

public class menutogame : MonoBehaviour
{
    public void IniciarJuego()
    {
        SceneManager.LoadScene("Game"); 
        
        Debug.Log("Iniciando Juego..."); 
    }

}  

