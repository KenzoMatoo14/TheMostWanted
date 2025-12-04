using UnityEngine;
using UnityEngine.SceneManagement;

public class menutoinstrucciones : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Ir()
    {
         SceneManager.LoadScene("Instrucciones"); 

         Debug.Log("ir a instrucciones..."); 
    }
    }

   