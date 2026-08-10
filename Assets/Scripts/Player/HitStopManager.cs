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

    private Coroutine hitStopCoroutine;
    private float hitStopEndTime = 0f; // en Time.realtimeSinceStartup
    private float preHitStopTimeScale = 1f;

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
    /// Pausa el juego para dar feedback de impacto. Es el único punto del proyecto
    /// que debe modificar Time.timeScale para este propósito - cualquier otro sistema
    /// (impact frames, etc.) debe pedirle la pausa a este manager en vez de tocar
    /// Time.timeScale directamente, para evitar que dos pausas se pisen entre sí.
    /// Si ya hay un hitstop en curso, esta llamada EXTIENDE la pausa hasta cubrir
    /// el pedido más largo, en vez de ignorarse o reiniciar el contador.
    /// </summary>
    /// <param name="duration">Duración del hitstop en segundos (tiempo real)</param>
    public void DoHitStop(float duration)
    {
        float requestedEndTime = Time.realtimeSinceStartup + duration;

        if (hitStopCoroutine == null)
        {
            preHitStopTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            hitStopEndTime = requestedEndTime;
            hitStopCoroutine = StartCoroutine(HitStopCoroutine());
        }
        else
        {
            // Ya hay un hitstop activo: extender si el nuevo pedido dura más
            hitStopEndTime = Mathf.Max(hitStopEndTime, requestedEndTime);
        }
    }

    private IEnumerator HitStopCoroutine()
    {
        // Usamos tiempo real (no afectado por timeScale) para saber cuándo terminar
        while (Time.realtimeSinceStartup < hitStopEndTime)
        {
            yield return null;
        }

        Time.timeScale = preHitStopTimeScale;
        hitStopCoroutine = null;
    }

    /// <summary>
    /// Verifica si actualmente está en hitstop
    /// </summary>
    public bool IsHitStopping()
    {
        return hitStopCoroutine != null;
    }

    /// <summary>
    /// Cancela un hitstop en curso SIN restaurar Time.timeScale por su cuenta.
    /// Pensado para sistemas externos (como el menú de pausa) que necesitan tomar
    /// control total de Time.timeScale y quieren asegurarse de que este manager
    /// no se lo pise después con su propia restauración.
    /// </summary>
    public void ForceStop()
    {
        if (hitStopCoroutine != null)
        {
            StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = null;
        }
    }
}