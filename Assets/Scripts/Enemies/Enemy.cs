using UnityEngine;

[CreateAssetMenu(fileName = "EnemyStats", menuName = "Stats/Enemy Stats")]
public class Enemy : ScriptableObject
{
    [Header("Health")]
    public int MaxHealth = 3;

    [Header("Visual Effects")]
    public float DamageFlashDuration = 0.5f;

    [Header("Knockback")]
    public KnockbackSettings Knockback = new KnockbackSettings();

    [Header("Capture")]
    public CaptureSettings Capture = new CaptureSettings();

    [Header("Stunned")]
    public StunSettings Stun = new StunSettings();
}

[System.Serializable]
public class KnockbackSettings
{
    public bool CanBeKnockback = true;
    public float MaxKnockbackDistance = 2.33f;
    public float KnockbackDuration = 0.2f; // Duraci�n del knockback en segundos
    public AnimationCurve KnockbackCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
}

[System.Serializable]
public class CaptureSettings
{
    public float CaptureDifficulty = 0.5f; // 0.0 = imposible, 1.0 = muy f�cil
    public Color CapturedColor = Color.green;
    public float StunToCaptureProgressMultiplier = 0.8f; // 80% del stun se convierte en progreso inicial
    public bool RequireMinimumStunToCapture = false;
    public float MinimumStunForCapture = 30f; // Stun m�nimo requerido para capturar
}

[System.Serializable]
public class StunSettings
{
    public float StunnedDecayBaseRate = 10f; // Velocidad base de reducci�n por segundo
    public float StunnedDecaySlowdownFactor = 0.5f; // Factor que ralentiza la reducci�n seg�n el nivel
    public float StunnedMovementImpactMax = 0.9f; // Reducci�n m�xima de velocidad (90%)
    public AnimationCurve StunnedMovementCurve = AnimationCurve.Linear(0, 0, 1, 1); // Curva de impacto
    public float StunnedThresholdForFullStop = 95f; // A partir de este % el enemigo se detiene completamente
}