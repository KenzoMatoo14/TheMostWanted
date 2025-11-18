using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImpactFrameManager : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Nombre de la capa para el efecto de impacto")]
    public string impactLayerName = "Impact";

    [Tooltip("Shader que pinta todo de blanco")]
    public Shader whiteShader;

    [Tooltip("Duración del efecto en segundos")]
    public float duration = 0.15f;

    [Tooltip("Tiempo de fade in/out (0 = instantáneo)")]
    public float fadeTime = 0.05f;

    [Tooltip("Aplicar hitstop (pause del juego)")]
    public bool applyHitstop = true;

    [Header("Audio")]
    [Tooltip("Reproducir sonido al activar el impact frame")]
    public bool playSound = true;

    [Tooltip("Clip de audio para el efecto de impacto")]
    public AudioClip impactSound;

    [Tooltip("Volumen del sonido (0-1)")]
    [Range(0f, 1f)]
    public float soundVolume = 1f;

    [Tooltip("Pitch del sonido (0.5-2)")]
    [Range(0.5f, 2f)]
    public float soundPitch = 1f;

    [Tooltip("Variación aleatoria del pitch")]
    [Range(0f, 0.5f)]
    public float pitchVariation = 0.1f;

    [Tooltip("AudioSource a usar (se crea automáticamente si está vacío)")]
    public AudioSource audioSource;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private Camera impactCamera;
    private int impactLayer;
    private Dictionary<Transform, int> originalLayers = new Dictionary<Transform, int>();
    private bool isActive = false;

    private void Awake()
    {
        InitializeImpactLayer();
        CreateImpactCamera();
        InitializeAudioSource();
    }

    /// <summary>
    /// Inicializa la capa de impacto
    /// </summary>
    private void InitializeImpactLayer()
    {
        impactLayer = LayerMask.NameToLayer(impactLayerName);

        if (impactLayer == -1)
        {
            Debug.LogError($"❌ La capa '{impactLayerName}' no existe. Créala en Edit → Project Settings → Tags and Layers");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"✓ Capa '{impactLayerName}' encontrada (Layer {impactLayer})");
        }
    }

    /// <summary>
    /// Inicializa el AudioSource para el efecto
    /// </summary>
    private void InitializeAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        if (playSound && impactSound == null)
        {
            Debug.LogWarning("⚠️ playSound está activo pero no hay impactSound asignado");
        }
        else if (showDebugLogs && impactSound != null)
        {
            Debug.Log($"✓ Audio configurado: {impactSound.name}");
        }
    }

    /// <summary>
    /// Crea la cámara auxiliar para el efecto de impacto
    /// </summary>
    private void CreateImpactCamera()
    {
        GameObject camGO = new GameObject("ImpactCamera");
        camGO.transform.SetParent(transform);
        impactCamera = camGO.AddComponent<Camera>();

        // Configurar cámara basada en la cámara principal
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            impactCamera.fieldOfView = mainCam.fieldOfView;
            impactCamera.nearClipPlane = mainCam.nearClipPlane;
            impactCamera.farClipPlane = mainCam.farClipPlane;
            impactCamera.orthographic = mainCam.orthographic;
            impactCamera.orthographicSize = mainCam.orthographicSize;
        }

        // Configurar para renderizar solo la capa de impacto
        impactCamera.cullingMask = 1 << impactLayer;
        impactCamera.clearFlags = CameraClearFlags.SolidColor;
        impactCamera.backgroundColor = Color.black;

        // Renderizar DESPUÉS de la cámara principal (encima)
        impactCamera.depth = (mainCam != null) ? mainCam.depth + 10 : 100;

        // Desactivar por defecto
        impactCamera.enabled = false;

        if (whiteShader == null)
        {
            Debug.LogWarning("⚠️ whiteShader no asignado. Asigna 'Custom/WhiteUnlit' en el inspector.");
        }
        else if (showDebugLogs)
        {
            Debug.Log($"✓ Cámara de impacto creada con shader: {whiteShader.name}");
        }
    }

    /// <summary>
    /// Dispara el efecto de impact frame
    /// </summary>
    /// <param name="objectA">Primer objeto en colisión</param>
    /// <param name="objectB">Segundo objeto en colisión</param>
    public void TriggerImpact(GameObject objectA, GameObject objectB = null)
    {
        if (isActive)
        {
            if (showDebugLogs) Debug.LogWarning("Impact frame ya está activo, ignorando...");
            return;
        }

        if (impactLayer == -1)
        {
            Debug.LogError("No se puede disparar impact frame: capa no configurada");
            return;
        }

        StartCoroutine(ImpactFrameCoroutine(objectA, objectB));
    }

    /// <summary>
    /// Reproduce el sonido de impacto con variación de pitch
    /// </summary>
    private void PlayImpactSound()
    {
        if (!playSound || impactSound == null || audioSource == null) return;

        // Aplicar variación de pitch
        float randomPitch = soundPitch + Random.Range(-pitchVariation, pitchVariation);
        audioSource.pitch = randomPitch;

        // Reproducir el sonido
        audioSource.PlayOneShot(impactSound, soundVolume);

        if (showDebugLogs)
        {
            Debug.Log($"🔊 Reproduciendo sonido de impacto (pitch: {randomPitch:F2})");
        }
    }

    private IEnumerator ImpactFrameCoroutine(GameObject objectA, GameObject objectB)
    {
        isActive = true;

        if (showDebugLogs)
        {
            Debug.Log($"⚡ IMPACT FRAME iniciado - Objetos: {objectA.name}" +
                     (objectB != null ? $" y {objectB.name}" : ""));
        }

        // 1. Guardar capas originales y cambiar a capa de impacto
        originalLayers.Clear();
        StoreAndSetLayerRecursive(objectA.transform, impactLayer);
        if (objectB != null && objectB != objectA)
        {
            StoreAndSetLayerRecursive(objectB.transform, impactLayer);
        }

        // 2. Sincronizar cámara de impacto con cámara principal
        SyncCameraWithMain();

        // 3. Aplicar shader de reemplazo y activar cámara
        if (whiteShader != null)
        {
            impactCamera.SetReplacementShader(whiteShader, null);
        }
        impactCamera.enabled = true;

        // 4. Reproducir sonido de impacto
        PlayImpactSound();

        // 5. Aplicar hitstop si está habilitado
        float originalTimeScale = Time.timeScale;
        if (applyHitstop)
        {
            Time.timeScale = 0f;
        }

        // 6. Fade in (opcional)
        if (fadeTime > 0f)
        {
            yield return new WaitForSecondsRealtime(fadeTime);
        }

        // 7. Mantener el efecto visible
        yield return new WaitForSecondsRealtime(duration);

        // 8. Fade out (opcional)
        if (fadeTime > 0f)
        {
            yield return new WaitForSecondsRealtime(fadeTime);
        }

        // 9. Restaurar hitstop
        if (applyHitstop)
        {
            Time.timeScale = originalTimeScale;
        }

        // 10. Desactivar cámara y limpiar
        impactCamera.ResetReplacementShader();
        impactCamera.enabled = false;

        // 11. Restaurar capas originales
        RestoreOriginalLayers();

        if (showDebugLogs)
        {
            Debug.Log("✓ Impact frame completado");
        }

        isActive = false;
    }

    /// <summary>
    /// Sincroniza la posición y configuración de la cámara de impacto con la principal
    /// </summary>
    private void SyncCameraWithMain()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        impactCamera.transform.position = mainCam.transform.position;
        impactCamera.transform.rotation = mainCam.transform.rotation;
        impactCamera.fieldOfView = mainCam.fieldOfView;
        impactCamera.orthographicSize = mainCam.orthographicSize;
        impactCamera.nearClipPlane = mainCam.nearClipPlane;
        impactCamera.farClipPlane = mainCam.farClipPlane;
    }

    /// <summary>
    /// Guarda la capa original y cambia recursivamente la capa de un objeto y sus hijos
    /// </summary>
    private void StoreAndSetLayerRecursive(Transform t, int newLayer)
    {
        if (t == null) return;

        if (!originalLayers.ContainsKey(t))
        {
            originalLayers[t] = t.gameObject.layer;
        }

        t.gameObject.layer = newLayer;

        foreach (Transform child in t)
        {
            StoreAndSetLayerRecursive(child, newLayer);
        }
    }

    /// <summary>
    /// Restaura las capas originales de todos los objetos modificados
    /// </summary>
    private void RestoreOriginalLayers()
    {
        foreach (var kvp in originalLayers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.gameObject.layer = kvp.Value;
            }
        }
        originalLayers.Clear();
    }

    /// <summary>
    /// Fuerza la cancelación del efecto (útil para debugging o cambios de escena)
    /// </summary>
    public void ForceCancel()
    {
        if (!isActive) return;

        StopAllCoroutines();

        if (impactCamera != null)
        {
            impactCamera.ResetReplacementShader();
            impactCamera.enabled = false;
        }

        RestoreOriginalLayers();

        if (applyHitstop)
        {
            Time.timeScale = 1f;
        }

        isActive = false;

        if (showDebugLogs)
        {
            Debug.Log("Impact frame cancelado forzosamente");
        }
    }

    private void OnDestroy()
    {
        ForceCancel();
    }
}