using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Sonidos del jugador. Los demás scripts (PlayerController, WhipAttack, PlayerStats) llaman
/// a estos métodos en el momento justo. Si un clip queda vacío, ese sonido simplemente no suena.
/// </summary>
public class PlayerSounds : MonoBehaviour
{
    [Header("Movimiento")]
    public AudioClip steps;
    public AudioClip jump;
    public AudioClip land;
    public AudioClip dash;

    [Header("Ataque")]
    [Tooltip("Suena cuando el latigazo golpea algo (en el suelo o sin rebote).")]
    public AudioClip attackHit;
    [Tooltip("Suena cuando se golpea algo hacia abajo en el aire y el jugador rebota (pogo).")]
    [FormerlySerializedAs("attackMiss")]
    public AudioClip pogoHit;

    [Header("Vida")]
    public AudioClip damage;
    public AudioClip death;

    [Header("Volumen")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 1f)] public float stepsVolume = 0.6f;

    [Header("Variación")]
    [Tooltip("Cambia un poco el tono en cada reproducción para que no suene repetitivo.")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;

    [Header("Pasos")]
    [Tooltip("Segundos entre pasos mientras camina.")]
    public float stepInterval = 0.4f;

    [Header("Aterrizaje")]
    [Tooltip("Tiempo mínimo en el aire para que suene el aterrizaje (evita que suene en saltitos o al aparecer).")]
    public float minAirTimeForLand = 0.15f;

    // Una fuente para sonidos sueltos y otra solo para pasos, así los pasos no se pisan con el resto
    private AudioSource sfxSource;
    private AudioSource stepSource;
    private float nextStepTime;

    private void Awake()
    {
        sfxSource = CreateSource();
        stepSource = CreateSource();
    }

    private AudioSource CreateSource()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f; // 2D
        return source;
    }

    private void Play(AudioClip clip, float clipVolume = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        sfxSource.PlayOneShot(clip, volume * clipVolume);
    }

    public void PlayJump() => Play(jump);
    public void PlayDash() => Play(dash);
    public void PlayPogoHit() => Play(pogoHit);
    public void PlayAttackHit() => Play(attackHit);
    public void PlayDamage() => Play(damage);
    public void PlayDeath() => Play(death);

    public void PlayLand(float airTime)
    {
        if (airTime >= minAirTimeForLand) Play(land);
    }

    /// <summary>Llamar cada frame mientras camina en el suelo; respeta el intervalo entre pasos.</summary>
    public void TickSteps()
    {
        if (steps == null || stepSource == null || Time.time < nextStepTime) return;

        nextStepTime = Time.time + stepInterval;
        stepSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        stepSource.PlayOneShot(steps, volume * stepsVolume);
    }
}
