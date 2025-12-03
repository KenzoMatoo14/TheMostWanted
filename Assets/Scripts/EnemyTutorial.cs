using UnityEngine;

public class EnemyTutorial : MonoBehaviour
{
    [Header("Tutorial Settings")]
    public GameObject bloqueParaCaer; // Arrastra aquí el GameObject del bloque morado

    private void OnDestroy()
    {
        // Cuando el enemigo muera, activar la caída del bloque
        if (bloqueParaCaer != null)
        {
            BloqueMovil bloque = bloqueParaCaer.GetComponent<BloqueMovil>();
            if (bloque != null)
            {
                bloque.IniciarCaida();
            }
        }
    }
}