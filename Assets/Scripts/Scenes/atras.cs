using UnityEngine;
using UnityEngine.SceneManagement;

public class atras : MonoBehaviour

{
    public void Instrucciones()
    {
        SceneManager.LoadScene("Menu"); 
        
        Debug.Log("regresando a  menu..."); 
    }

}  


