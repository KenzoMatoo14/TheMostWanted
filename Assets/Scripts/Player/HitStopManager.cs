using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;
    public static HitStopManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("HitStopManager");
                instance = go.AddComponent<HitStopManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private bool isHitStopping = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Pausa el juego por una fracción de segundo para dar feedback de impacto
    /// </summary>
    /// <param name="duration">Duración del hitstop en segundos</param>
    public void DoHitStop(float duration)
    {
        if (!isHitStopping)
        {
            StartCoroutine(HitStopCoroutine(duration));
        }
    }

    private IEnumerator HitStopCoroutine(float duration)
    {
        isHitStopping = true;

        // Guardar el timeScale original
        float originalTimeScale = Time.timeScale;

        // Pausar el tiempo
        Time.timeScale = 0f;

        // Esperar usando unscaledTime (no afectado por timeScale)
        yield return new WaitForSecondsRealtime(duration);

        // Restaurar el timeScale
        Time.timeScale = originalTimeScale;

        isHitStopping = false;
    }

    /// <summary>
    /// Verifica si actualmente está en hitstop
    /// </summary>
    public bool IsHitStopping()
    {
        return isHitStopping;
    }
}