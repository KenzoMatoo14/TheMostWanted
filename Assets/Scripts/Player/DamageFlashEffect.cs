using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageFlashEffect : MonoBehaviour
{
    private SpriteRenderer[] spriteRenderers;
    private Dictionary<SpriteRenderer, Color> originalColors;
    private Dictionary<SpriteRenderer, Material> originalMaterials;
    private Material whiteFlashMaterial;
    private bool isFlashing = false;

    private void Awake()
    {
        CreateWhiteFlashMaterial();
        CacheSprites();
    }

    /// <summary>
    /// Crea un material temporal para el flash blanco
    /// </summary>
    private void CreateWhiteFlashMaterial()
    {
        Debug.Log("=== INICIANDO CREACIÓN DE MATERIAL ===");

        // Buscar nuestro shader personalizado
        Shader whiteFlashShader = Shader.Find("Custom/WhiteFlash");

        if (whiteFlashShader == null)
        {
            Debug.LogError("❌ NO SE ENCONTRÓ el shader 'Custom/WhiteFlash'");
            Debug.LogError("Verifica que el archivo WhiteFlash.shader existe y su primera línea dice: Shader \"Custom/WhiteFlash\"");

            // Fallback a un shader por defecto
            whiteFlashShader = Shader.Find("Sprites/Default");
            Debug.LogWarning("Usando shader fallback: Sprites/Default");
        }
        else
        {
            Debug.Log("✓ Shader 'Custom/WhiteFlash' encontrado correctamente!");
        }

        whiteFlashMaterial = new Material(whiteFlashShader);
        whiteFlashMaterial.name = "WhiteFlashMaterial";
        whiteFlashMaterial.color = Color.white;

        Debug.Log($"Material creado: {whiteFlashMaterial.name} usando shader: {whiteFlashMaterial.shader.name}");
    }

    /// <summary>
    /// Cachea todos los SpriteRenderers y sus colores/materiales originales
    /// </summary>
    private void CacheSprites()
    {
        // Obtener todos los SpriteRenderers (incluidos los hijos)
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();

        originalColors = new Dictionary<SpriteRenderer, Color>();
        originalMaterials = new Dictionary<SpriteRenderer, Material>();

        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
            {
                originalColors[sr] = sr.color;
                originalMaterials[sr] = sr.material;
            }
        }

        Debug.Log($"{gameObject.name} - {spriteRenderers.Length} sprites cacheados para efecto de daño");
    }

    /// <summary>
    /// Inicia el efecto de flash blanco
    /// </summary>
    public void Flash(float duration)
    {
        Debug.Log($"⚡ FLASH LLAMADO en {gameObject.name} - Duración: {duration}s");

        if (isFlashing)
        {
            Debug.LogWarning("Ya estaba flasheando, reiniciando...");
            StopAllCoroutines();
        }

        StartCoroutine(FlashCoroutine(duration));
    }

    private IEnumerator FlashCoroutine(float duration)
    {
        isFlashing = true;
        Debug.Log($">>> Iniciando flash en {spriteRenderers.Length} sprites");

        // Cambiar todos los sprites al material de flash blanco
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
            {
                Debug.Log($"  - Cambiando sprite '{sr.name}' de material '{sr.material.name}' a '{whiteFlashMaterial.name}'");
                sr.material = whiteFlashMaterial;
                sr.color = Color.white;
            }
        }

        Debug.Log($"⏱ Esperando {duration} segundos...");

        // Esperar la duración (usando unscaled time para que funcione con hitstop)
        yield return new WaitForSecondsRealtime(duration);

        Debug.Log("⏱ Tiempo cumplido, restaurando...");

        // Restaurar materiales y colores originales
        RestoreOriginalState();

        isFlashing = false;
        Debug.Log("<<< Flash completado");
    }

    /// <summary>
    /// Restaura los materiales y colores originales de todos los sprites
    /// </summary>
    private void RestoreOriginalState()
    {
        foreach (SpriteRenderer sr in spriteRenderers)
        {
            if (sr != null)
            {
                // Restaurar material original
                if (originalMaterials.ContainsKey(sr))
                {
                    sr.material = originalMaterials[sr];
                    Debug.Log($"  - Restaurado '{sr.name}' a material '{originalMaterials[sr].name}'");
                }

                // Restaurar color original
                if (originalColors.ContainsKey(sr))
                {
                    sr.color = originalColors[sr];
                }
            }
        }
    }

    /// <summary>
    /// Actualiza el caché de sprites (útil si se añaden/eliminan sprites dinámicamente)
    /// </summary>
    public void RefreshCache()
    {
        CacheSprites();
    }

    /// <summary>
    /// Fuerza la restauración de materiales y colores originales
    /// </summary>
    public void ForceRestore()
    {
        if (isFlashing)
        {
            StopAllCoroutines();
            RestoreOriginalState();
            isFlashing = false;
        }
    }

    private void OnDestroy()
    {
        // Asegurar que los materiales se restauren al destruir el objeto
        if (isFlashing)
        {
            ForceRestore();
        }

        // Destruir el material temporal para evitar memory leaks
        if (whiteFlashMaterial != null)
        {
            Destroy(whiteFlashMaterial);
        }
    }
}