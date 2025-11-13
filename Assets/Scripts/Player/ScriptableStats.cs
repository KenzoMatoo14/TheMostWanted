using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Stats/Player Stats")]
public class ScriptableStats : ScriptableObject
{
    [Header("--------HEALTH--------")]
    public int maxHealth = 100;

    [Header("--------VISUAL EFFECTS--------")]
    public GameObject HitParticleEffect;

    [Header("--------MOVEMENT--------")]
    public float WalkSpeed = 5f;
    public float Acceleration = 25f;       // rapidez al ganar velocidad
    public float Deceleration = 35f;       // rapidez al frenar
    public float AirAcceleration = 15f;    // en aire
    public float AirDeceleration = 20f;    // en aire
    public float ApexBonus = 2f;           // multiplicador de velocidad cerca del pico del salto

    [Header("DASH")]
    public float DashForce = 12f;
    public float DashDuration = 0.2f;
    public float DashCooldown = 1f;

    [Header("JUMP")]
    public float JumpForce = 7f;
    public int MaxJumps = 2;
    public float JumpCutMultiplier = 0.5f;
    public float CoyoteTime = 0.15f;
    public float JumpBuffer = 0.2f;

    [Header("GROUND CHECK")]
    public float GroundCheckRadius = 0.2f;
    public LayerMask GroundLayer;

    [Header("--------GRAPPLING HOOK--------")]
    [Header("Grapple Settings")]
    public float MaxGrappleDistance = 15f;
    public LayerMask GrappleLayer;

    [Header("Pull Effect")]
    public float PullStrength = 12f;
    public float PullTime = 0.3f;
    public float RopeShorten = 0.75f;

    [Header("Swing Physics")]
    public float SwingForce = 5f;
    public float ConstraintForce = 50f;
    public float ConstraintSpeed = 15f;
    public float Dampening = 0.95f;

    [Header("Grappling Hook Cooldown")]
    public float CooldownTime = 1f;

    [Header("--------COMBAT--------")]
    [Header("Melee Attack")]
    public int MeleeDamage = 10;
    public float AttackCooldown = 0.5f;
    public LayerMask EnemyLayers;

    [Header("Stun System")]
    [Tooltip("Multiplicador de stun basado en el % de daño. Ej: 1.5 = si haces 10% de daño, aplicas 15% de stun")]
    public float StunMultiplier = 1.5f;
    public bool ApplyStunOnHit = true;
    [Tooltip("Stun adicional si el enemigo está por debajo de 50% vida")]
    public float LowHealthStunBonus = 5f;
    [Tooltip("Aplicar bonus de stun por vida baja")]
    public bool ApplyLowHealthBonus = false;

    [Header("Capture System")]
    [Tooltip("Tiempo para completar la captura (en segundos)")]
    public float CaptureTime = 5f;
    [Tooltip("Distancia máxima para capturar enemigos")]
    public float CaptureRange = 4f;

    [Header("Hover Detection")]
    [Tooltip("Radio del raycast circular para detectar hover")]
    public float HoverDetectionRadius = 0.5f;
    [Tooltip("Distancia máxima del raycast para hover")]
    public float HoverDetectionDistance = 10f;
}
