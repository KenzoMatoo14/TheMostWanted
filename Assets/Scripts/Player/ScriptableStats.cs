using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Stats/Player Stats")]
public class ScriptableStats : ScriptableObject
{
    [Header("--------HEALTH--------")]
    public int maxHealth = 100;

    [Header("--------VISUAL EFFECTS--------")]
    public GameObject HitParticleEffect;
    public float HitStopDuration = 0.1f;

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
    [Tooltip("Radio de tolerancia del disparo del gancho (circle cast en vez de un raycast de una sola linea). Da un poco de perdon si el mouse no apunta exactamente al borde del objeto.")]
    public float GrappleAimRadius = 0.3f;

    [Header("Pull Effect")]
    public float PullStrength = 12f;
    public float PullTime = 0.3f;
    public float RopeShorten = 0.75f;

    [Header("Swing Physics")]
    public float SwingForce = 5f;
    public float ConstraintForce = 50f;
    public float ConstraintSpeed = 15f;
    public float Dampening = 0.95f;
    [Tooltip("Velocidad maxima (unidades/seg) que el jugador puede alcanzar mientras esta enganchado. Evita que el swing se acelere sin limite (por ejemplo, bombeando el input en circulos) y se vuelva incontrolable.")]
    public float MaxSwingSpeed = 20f;
    [Tooltip("Angulo maximo (en grados, medido desde recto hacia abajo del gancho) que el jugador puede alcanzar hacia cada lado. Evita que el swing de una vuelta completa alrededor del gancho, lo cual lo vuelve impredecible para diseñar niveles de parkour. Mantener bien por debajo de 180 para evitar el punto opuesto al gancho.")]
    [Range(0f, 170f)]
    public float MaxSwingAngle = 100f;

    [Header("Grappling Hook Cooldown")]
    public float CooldownTime = 1f;

    [Header("--------COMBAT--------")]
    [Header("Melee Attack")]
    public int MeleeDamage = 10;
    public float AttackCooldown = 0.5f;
    public LayerMask EnemyLayers;

    [Header("Stun System")]
    [Tooltip("Multiplicador de stun basado en el % de da�o. Ej: 1.5 = si haces 10% de da�o, aplicas 15% de stun")]
    public float StunMultiplier = 1.5f;
    public bool ApplyStunOnHit = true;
    [Tooltip("Stun adicional si el enemigo est� por debajo de 50% vida")]
    public float LowHealthStunBonus = 5f;
    [Tooltip("Aplicar bonus de stun por vida baja")]
    public bool ApplyLowHealthBonus = false;

    [Header("Capture System")]
    [Tooltip("Tiempo para completar la captura (en segundos)")]
    public float CaptureTime = 5f;
    [Tooltip("Distancia m�xima para capturar enemigos")]
    public float CaptureRange = 4f;
    [Tooltip("Cuanto progreso suma cada click del boton de captura, como fraccion de CaptureTime (0-1). Ej: 0.15 = necesitas ~7 clicks para llenar la barra")]
    [Range(0f, 1f)]
    public float CaptureChargePerClick = 0.15f;
    [Tooltip("Velocidad a la que decae solo el progreso de captura cuando no se hace click, como fraccion de CaptureTime por segundo. Mas bajo = decae mas lento")]
    public float CaptureDecayRate = 0.2f;

    [Header("Hover Detection")]
    [Tooltip("Radio del raycast circular para detectar hover")]
    public float HoverDetectionRadius = 0.5f;
    [Tooltip("Distancia m�xima del raycast para hover")]
    public float HoverDetectionDistance = 10f;
}
