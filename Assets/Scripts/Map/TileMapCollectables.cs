using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Gestiona coleccionables en un Tilemap (monedas, pociones, etc.)
/// </summary>
[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class TilemapCollectables : MonoBehaviour
{
    [Header("Tilemap Type")]
    [SerializeField] private CollectableType collectableType = CollectableType.Coin;

    [Header("Coin Settings")]
    [SerializeField] private int coinValue = 1;

    [Header("Healing Settings")]
    [SerializeField] private int healAmount = 20;
    [SerializeField] private bool healByPercentage = false;
    [Range(0f, 1f)]
    [SerializeField] private float healPercentage = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip collectSound;
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.7f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject collectEffectPrefab;

    [Header("Collision Detection")]
    [Tooltip("Radio de detección alrededor del jugador (aumenta si no detecta bien)")]
    [SerializeField] private float detectionRadius = 0.3f;

    private Tilemap tilemap;
    private TilemapCollider2D tilemapCollider;

    public enum CollectableType
    {
        Coin,
        HealthPotion
    }

    void Start()
    {
        tilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<TilemapCollider2D>();

        if (tilemap == null)
        {
            Debug.LogError("TilemapCollectables: No se encontró componente Tilemap!");
            return;
        }

        if (tilemapCollider == null)
        {
            Debug.LogError("TilemapCollectables: Se requiere TilemapCollider2D!");
            return;
        }

        // CRÍTICO: Configurar el TilemapCollider2D como Trigger
        tilemapCollider.isTrigger = true;

        // Asegurar que tiene un Rigidbody2D
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        // Configurar el Rigidbody2D
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        Debug.Log($"TilemapCollectables ({collectableType}) configurado correctamente como Trigger");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Verificar si es el jugador
        if (!collision.CompareTag("Player")) return;

        CheckAndCollectNearbyTiles(collision.transform.position);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // Verificar constantemente mientras el jugador está dentro del área
        if (!collision.CompareTag("Player")) return;

        CheckAndCollectNearbyTiles(collision.transform.position);
    }

    private void CheckAndCollectNearbyTiles(Vector3 playerPosition)
    {
        // Obtener la posición del jugador en el grid
        Vector3Int centerCell = tilemap.WorldToCell(playerPosition);

        // Revisar la celda central y las adyacentes
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkCell = centerCell + new Vector3Int(x, y, 0);

                // Verificar si hay un tile y si está lo suficientemente cerca
                if (tilemap.HasTile(checkCell))
                {
                    Vector3 tileCenter = tilemap.GetCellCenterWorld(checkCell);
                    float distance = Vector3.Distance(playerPosition, tileCenter);

                    // Si el jugador está lo suficientemente cerca, recoger el tile
                    if (distance <= detectionRadius)
                    {
                        ProcessCollectable(checkCell, playerPosition);
                    }
                }
            }
        }
    }

    private void ProcessCollectable(Vector3Int cellPosition, Vector3 playerPosition)
    {
        switch (collectableType)
        {
            case CollectableType.Coin:
                CollectCoin(cellPosition);
                break;

            case CollectableType.HealthPotion:
                CollectHealthPotion(cellPosition, playerPosition);
                break;
        }
    }

    private void CollectCoin(Vector3Int cellPosition)
    {
        // Agregar monedas al contador
        CoinManager coinManager = FindObjectOfType<CoinManager>();
        if (coinManager != null)
        {
            coinManager.AddCoins(coinValue);
        }
        else
        {
            Debug.LogWarning("TilemapCollectables: No se encontró CoinManager!");
        }

        // Reproducir efectos
        PlayCollectEffects(cellPosition);

        // Eliminar el tile
        tilemap.SetTile(cellPosition, null);
    }

    private void CollectHealthPotion(Vector3Int cellPosition, Vector3 playerPosition)
    {
        // Buscar el jugador desde la posición
        Collider2D[] colliders = Physics2D.OverlapCircleAll(playerPosition, 0.5f);
        PlayerStats playerStats = null;

        foreach (Collider2D col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                playerStats = col.GetComponent<PlayerStats>();
                break;
            }
        }

        if (playerStats == null)
        {
            Debug.LogWarning("TilemapCollectables: No se encontró PlayerStats en el jugador!");
            return;
        }

        // Verificar si el jugador necesita curación
        if (playerStats.GetCurrentHealth() >= playerStats.GetMaxHealth())
        {
            Debug.Log("El jugador ya tiene la vida completa!");
            return; // No consumir la poción
        }

        // Calcular curación
        int actualHealAmount = healAmount;
        if (healByPercentage)
        {
            actualHealAmount = Mathf.RoundToInt(playerStats.GetMaxHealth() * healPercentage);
        }

        // Curar al jugador
        playerStats.Heal(actualHealAmount);

        // Reproducir efectos
        PlayCollectEffects(cellPosition);

        // Eliminar el tile
        tilemap.SetTile(cellPosition, null);
    }

    private void PlayCollectEffects(Vector3Int cellPosition)
    {
        // Obtener la posición mundial del centro del tile
        Vector3 worldPosition = tilemap.GetCellCenterWorld(cellPosition);

        // Reproducir sonido
        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, worldPosition, soundVolume);
        }

        // Crear efecto visual
        if (collectEffectPrefab != null)
        {
            GameObject effect = Instantiate(collectEffectPrefab, worldPosition, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    // Visualización en el editor
    private void OnDrawGizmosSelected()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        Gizmos.color = collectableType == CollectableType.Coin ? Color.yellow : Color.green;

        // Dibujar un círculo en cada tile
        foreach (Vector3Int pos in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(pos))
            {
                Vector3 worldPos = tilemap.GetCellCenterWorld(pos);
                Gizmos.DrawWireSphere(worldPos, detectionRadius);
            }
        }
    }
}