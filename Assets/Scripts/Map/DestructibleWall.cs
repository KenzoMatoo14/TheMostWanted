using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Tilemap))]
[RequireComponent(typeof(TilemapCollider2D))]
public class DestructibleWall : MonoBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private int tileHealth = 10;
    [SerializeField] private bool regenerateTiles = false;
    [SerializeField] private float regenerationTime = 5f;

    [Header("Damage Settings")]
    [SerializeField] private LayerMask projectileLayers;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject destructionParticlePrefab;
    [SerializeField] private AudioClip destructionSound;
    [SerializeField] private bool showDamageEffect = true;
    [SerializeField] private Color damageColor = new Color(1f, 0.5f, 0.5f, 1f);
    [SerializeField] private float damageFlashDuration = 0.1f;

    [Header("Tile Damage Visualization")]
    [SerializeField] private Sprite[] damagedTileSprites;
    [SerializeField] private bool useDamagedSprites = true;

    private Tilemap tilemap;
    private TilemapCollider2D tilemapCollider;
    private Dictionary<Vector3Int, TileData> tileHealthMap;
    private AudioSource audioSource;

    private class TileData
    {
        public int currentHealth;
        public int maxHealth;
        public TileBase originalTile;
        public Coroutine regenerationCoroutine;
        public TileData(int health, TileBase tile) { currentHealth = health; maxHealth = health; originalTile = tile; regenerationCoroutine = null; }
        public float GetHealthPercentage() => (float)currentHealth / maxHealth;
    }

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
        tilemapCollider = GetComponent<TilemapCollider2D>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
        InitializeTileHealth();
    }

    void InitializeTileHealth()
    {
        tileHealthMap = new Dictionary<Vector3Int, TileData>();
        BoundsInt bounds = tilemap.cellBounds;

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int tilePos = new Vector3Int(x, y, 0);
                TileBase tile = tilemap.GetTile(tilePos);
                if (tile != null)
                    tileHealthMap[tilePos] = new TileData(tileHealth, tile);
            }
        }

        Debug.Log($"[DestructibleWall] Inicializada con {tileHealthMap.Count} tiles");
    }

    // --------------------------------------------------------------------------------
    // Collision handlers and robust point extraction
    // --------------------------------------------------------------------------------
    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"[DestructibleWall] OnCollisionEnter2D called. Other: {collision.gameObject.name}, layer {collision.gameObject.layer}");
        if (((1 << collision.gameObject.layer) & projectileLayers.value) == 0)
        {
            Debug.Log("[DestructibleWall] Colisi�n ignorada: capa no en projectileLayers");
            return;
        }

        Vector2? impactPoint = GetImpactPointFromCollision(collision);
        if (impactPoint == null)
        {
            Debug.LogWarning("[DestructibleWall] No se pudo determinar punto de impacto desde Collision2D.contacts. Intentando ClosestPoint...");
            impactPoint = collision.collider.ClosestPoint(transform.position); // fallback
        }

        if (impactPoint == null)
        {
            Debug.LogError("[DestructibleWall] No se obtuvo punto de impacto v�lido.");
            return;
        }

        Vector3Int tilePos = WorldToTilePos((Vector2)impactPoint);
        Debug.Log($"[DestructibleWall] Impacto en world {impactPoint.Value} -> tile {tilePos}");

        if (!tileHealthMap.ContainsKey(tilePos))
        {
            // Try small offsets around point in case of rounding
            bool found = TryFindNearbyTile(tilePos, (Vector2)impactPoint, out Vector3Int foundPos);
            if (!found)
            {
                Debug.Log($"[DestructibleWall] No hay tile en {tilePos} (ni en cercanos). Abortando.");
                return;
            }
            tilePos = foundPos;
            Debug.Log($"[DestructibleWall] Encontrado tile cercano en {tilePos}");
        }

        int damage = CalculateDamageFromCollision(collision);
        DamageTile(tilePos, damage, (Vector2)tilemap.GetCellCenterWorld(tilePos));
    }

    // Extra handlers to increase chance of detection
    void OnCollisionStay2D(Collision2D collision)
    {
        // small chance to catch something missed in enter
    }

    Vector2? GetImpactPointFromCollision(Collision2D collision)
    {
        if (collision == null) return null;
        if (collision.contacts != null && collision.contacts.Length > 0)
        {
            // take first contact (or average)
            Vector2 sum = Vector2.zero;
            foreach (var c in collision.contacts) sum += c.point;
            return sum / collision.contacts.Length;
        }
        return null;
    }

    Vector3Int WorldToTilePos(Vector2 worldPoint)
    {
        // convert world to cell, using tilemap transform and cell anchor by using WorldToCell
        return tilemap.WorldToCell((Vector3)worldPoint);
    }

    bool TryFindNearbyTile(Vector3Int basePos, Vector2 worldPoint, out Vector3Int found)
    {
        int search = 1;
        for (int r = 0; r <= search; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    Vector3Int p = basePos + new Vector3Int(dx, dy, 0);
                    if (tileHealthMap.ContainsKey(p))
                    {
                        found = p;
                        return true;
                    }
                }
            }
        }
        found = Vector3Int.zero;
        return false;
    }

    int CalculateDamageFromCollision(Collision2D collision)
    {
        Rigidbody2D rb = collision.rigidbody;
        if (rb == null) return 1;
        float velocity = rb.linearVelocity.magnitude;
        int damage = Mathf.CeilToInt(velocity / 2f);
        damage = Mathf.Clamp(damage, 1, tileHealth);
        return damage;
    }

    public void DamageTile(Vector3Int tilePos, int damage, Vector2 worldPosition)
    {
        if (!tileHealthMap.ContainsKey(tilePos))
        {
            Debug.LogWarning($"[DestructibleWall] DamageTile llamado en tile sin data: {tilePos}");
            return;
        }

        TileData tileData = tileHealthMap[tilePos];
        tileData.currentHealth -= damage;
        Debug.Log($"[DestructibleWall] Tile {tilePos} recibi� {damage}. Vida {tileData.currentHealth}/{tileData.maxHealth}");

        if (showDamageEffect) StartCoroutine(FlashTileDamage(tilePos));

        if (useDamagedSprites && damagedTileSprites != null && damagedTileSprites.Length > 0)
            UpdateTileDamageSprite(tilePos, tileData);

        if (tileData.currentHealth <= 0)
            DestroyTile(tilePos, worldPosition);
        else
            tilemap.RefreshTile(tilePos);
    }

    void UpdateTileDamageSprite(Vector3Int tilePos, TileData tileData)
    {
        float hp = tileData.GetHealthPercentage();
        int idx = Mathf.FloorToInt((1f - hp) * damagedTileSprites.Length);
        idx = Mathf.Clamp(idx, 0, damagedTileSprites.Length - 1);

        if (hp < 1f && hp > 0f)
        {
            Tile damagedTile = ScriptableObject.CreateInstance<Tile>();
            damagedTile.sprite = damagedTileSprites[idx];
            damagedTile.colliderType = Tile.ColliderType.Sprite;
            tilemap.SetTile(tilePos, damagedTile);
        }
    }

    IEnumerator FlashTileDamage(Vector3Int tilePos)
    {
        Color original = tilemap.GetColor(tilePos);
        tilemap.SetColor(tilePos, damageColor);
        yield return new WaitForSeconds(damageFlashDuration);
        if (tileHealthMap.ContainsKey(tilePos))
            tilemap.SetColor(tilePos, original);
    }

    void DestroyTile(Vector3Int tilePos, Vector2 worldPosition)
    {
        if (destructionParticlePrefab != null) Instantiate(destructionParticlePrefab, worldPosition, Quaternion.identity);
        if (destructionSound != null && audioSource != null) audioSource.PlayOneShot(destructionSound);

        tilemap.SetTile(tilePos, null);
        Debug.Log($"[DestructibleWall] Tile destruido en {tilePos}");

        if (regenerateTiles)
        {
            TileData td = tileHealthMap[tilePos];
            td.regenerationCoroutine = StartCoroutine(RegenerateTile(tilePos, td));
        }
        else
        {
            tileHealthMap.Remove(tilePos);
        }

        // Update collider: ProcessTilemapChanges can be used but it's okay llamar RefreshTile en el �rea
        tilemap.RefreshAllTiles();
        if (tilemapCollider != null)
        {
            // En algunos casos ProcessTilemapChanges no es accesible en runtime dependiendo de versi�n;
            // RefreshAllTiles + esperar un frame tambi�n ayuda.
            StartCoroutine(DelayedColliderUpdate());
        }
    }

    IEnumerator DelayedColliderUpdate()
    {
        yield return null;
        // Si tu versi�n tiene ProcessTilemapChanges y es p�blico, lo puedes llamar aqu�:
        // tilemapCollider.ProcessTilemapChanges();
    }

    IEnumerator RegenerateTile(Vector3Int tilePos, TileData tileData)
    {
        yield return new WaitForSeconds(regenerationTime);
        tilemap.SetTile(tilePos, tileData.originalTile);
        tilemap.SetColor(tilePos, Color.white);
        tileData.currentHealth = tileData.maxHealth;
        tileData.regenerationCoroutine = null;
        tilemap.RefreshTile(tilePos);
        Debug.Log($"[DestructibleWall] Tile regenerado en {tilePos}");
    }

    // Public helper - �til para testing desde otro script o bot�n
    public void DamageAtWorldPoint(Vector2 worldPoint, int damage = 1)
    {
        Vector3Int tilePos = WorldToTilePos(worldPoint);
        Debug.Log($"[DestructibleWall] DamageAtWorldPoint llamado: world {worldPoint} -> tile {tilePos}");
        if (tileHealthMap.ContainsKey(tilePos))
            DamageTile(tilePos, damage, (Vector2)tilemap.GetCellCenterWorld(tilePos));
        else
            Debug.LogWarning($"[DestructibleWall] DamageAtWorldPoint: no hay tile en {tilePos}");
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (tilemap == null) tilemap = GetComponent<Tilemap>();
        if (tilemap != null && tileHealthMap != null)
        {
            foreach (var kvp in tileHealthMap)
            {
                Vector3 worldPos = tilemap.GetCellCenterWorld(kvp.Key);
                float healthPercent = kvp.Value.GetHealthPercentage();
                Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);
                Gizmos.DrawWireCube(worldPos, tilemap.cellSize * 0.9f);
            }
        }
    }
#endif
}
