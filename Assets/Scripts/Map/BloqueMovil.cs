using UnityEngine;
using UnityEngine.Tilemaps;

public class BloqueMovil : MonoBehaviour
{
    [Header("Configuración de Caída")]
    [SerializeField] private float velocidadCaida = 5f;
    [SerializeField] private float distanciaCaida = 5f; // Cuántos tiles baja
    [SerializeField] private LayerMask capaSuelo; // Para detectar colisiones

    private Tilemap tilemap;
    private Vector3 posicionInicial;
    private Vector3 posicionFinal;
    private bool estaCayendo = false;
    private bool haTerminado = false;

    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();
        posicionInicial = transform.position;
        posicionFinal = posicionInicial + Vector3.down * distanciaCaida;
    }

    public void IniciarCaida()
    {
        if (!haTerminado)
        {
            estaCayendo = true;
        }
    }

    private void Update()
    {
        if (estaCayendo && !haTerminado)
        {
            // Mover el tilemap hacia abajo
            transform.position = Vector3.MoveTowards(
                transform.position,
                posicionFinal,
                velocidadCaida * Time.deltaTime
            );

            // Verificar si llegó a la posición final
            if (Vector3.Distance(transform.position, posicionFinal) < 0.01f)
            {
                transform.position = posicionFinal;
                estaCayendo = false;
                haTerminado = true;

                // Hacer que el bloque sea sólido (si no lo era)
                if (tilemap != null)
                {
                    GetComponent<TilemapCollider2D>().enabled = true;
                }
            }
        }
    }
}