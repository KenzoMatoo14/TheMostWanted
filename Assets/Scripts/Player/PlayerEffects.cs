using UnityEngine;

/// <summary>
/// Gotitas de slime que salpican al saltar, aterrizar, atacar y rebotar (pogo).
/// Todo se crea por código: un solo ParticleSystem hijo al que se le "disparan" gotas con
/// velocidad propia (ParticleSystem.Emit), así cada acción tiene su forma de salpicar.
/// Las gotas son cuadraditos de pixel con los tonos del slime y chocan con el suelo.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerEffects : MonoBehaviour
{
    [Header("Aspecto de las gotas")]
    [Tooltip("Colores al azar para cada gota (por defecto, los tonos del slime).")]
    public Color[] dropletColors =
    {
        new Color32(223, 223, 223, 255),
        new Color32(214, 214, 214, 255),
        new Color32(202, 202, 202, 255),
        new Color32(186, 186, 186, 255),
    };
    [Tooltip("Pixeles por unidad del sprite del jugador, para que las gotas midan pixeles enteros.")]
    public float pixelsPerUnit = 48f;
    [Tooltip("Tamaño mínimo y máximo de una gota, en pixeles.")]
    public Vector2Int dropletSizePx = new Vector2Int(2, 4);
    [Tooltip("Cuánto viven las gotas (segundos, mínimo y máximo).")]
    public Vector2 lifetime = new Vector2(0.35f, 0.7f);
    [Tooltip("Qué tanto les afecta la gravedad.")]
    public float gravity = 2.5f;
    [Tooltip("Las gotas rebotan/se quedan en el suelo en vez de atravesarlo.")]
    public bool collideWithGround = true;

    [Header("Salto")]
    public bool jumpEnabled = true;
    public int jumpCount = 8;
    [Tooltip("Las gotas del salto en el aire (doble salto / soltar gancho) son menos.")]
    public int airJumpCount = 4;
    public Vector2 jumpSpeed = new Vector2(1.5f, 4f);

    [Header("Aterrizaje")]
    public bool landEnabled = true;
    [Tooltip("Gotas con una caída corta y con una caída larga.")]
    public Vector2Int landCount = new Vector2Int(5, 16);
    [Tooltip("Tiempo mínimo en el aire para salpicar al aterrizar (evita gotas en saltitos).")]
    public float landMinAirTime = 0.15f;
    [Tooltip("Tiempo en el aire que cuenta como 'caída fuerte' (salpicadura máxima).")]
    public float landMaxAirTime = 0.9f;
    public Vector2 landSpeed = new Vector2(2f, 6f);

    [Header("Golpe del ataque (salen del punto de impacto)")]
    public bool attackEnabled = true;
    public int attackCount = 8;
    public Vector2 attackSpeed = new Vector2(2.5f, 6f);
    [Tooltip("Qué tan abierto sale el chorro de gotas hacia el mouse (grados).")]
    public float attackSpread = 40f;

    [Header("Recibir daño")]
    public bool damageEnabled = true;
    public int damageCount = 12;
    public Vector2 damageSpeed = new Vector2(2f, 5f);
    [Tooltip("Qué tan abierta sale la salpicadura, alejándose del golpe (grados).")]
    public float damageSpread = 60f;

    [Header("Rebote (pogo)")]
    public bool pogoEnabled = true;
    public int pogoCount = 10;
    public Vector2 pogoSpeed = new Vector2(2f, 5f);

    private PlayerController playerController;
    private SpriteRenderer bodyRenderer;
    private ParticleSystem particles;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        bodyRenderer = GetComponent<SpriteRenderer>();
        particles = CreateParticleSystem();
    }

    ///////////////////////// ACCIONES (las llaman PlayerController y WhipAttack)

    /// <param name="fromGround">false para saltos en el aire: salen menos gotas.</param>
    public void PlayJump(bool fromGround)
    {
        if (!jumpEnabled) return;
        int count = fromGround ? jumpCount : airJumpCount;

        // Salpica hacia los lados y un poco hacia arriba desde los pies
        for (int i = 0; i < count; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector2 velocity = new Vector2(side * Random.Range(jumpSpeed.x, jumpSpeed.y),
                                           Random.Range(0.3f, 2.5f));
            Emit(FeetPosition(), velocity);
        }
    }

    public void PlayLand(float airTime)
    {
        if (!landEnabled || airTime < landMinAirTime) return;

        // Mientras más tiempo cayó, más gotas y más fuerte salen
        float strength = Mathf.Clamp01(airTime / Mathf.Max(0.01f, landMaxAirTime));
        int count = Mathf.RoundToInt(Mathf.Lerp(landCount.x, landCount.y, strength));
        float speedScale = Mathf.Lerp(0.6f, 1f, strength);

        for (int i = 0; i < count; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector2 velocity = new Vector2(side * Random.Range(landSpeed.x, landSpeed.y),
                                           Random.Range(1f, 3.5f)) * speedScale;
            Emit(FeetPosition(), velocity);
        }
    }

    /// <summary>Gotas que salpican desde donde el látigo golpeó, siguiendo la dirección del golpe.</summary>
    public void PlayAttackHit(Vector2 hitPoint, Vector2 direction)
    {
        if (!attackEnabled || direction == Vector2.zero) return;

        for (int i = 0; i < attackCount; i++)
        {
            Vector2 dir = Rotate(direction.normalized, Random.Range(-attackSpread, attackSpread));
            Emit(hitPoint, dir * Random.Range(attackSpeed.x, attackSpeed.y));
        }
    }

    /// <summary>
    /// Salpicadura desde el cuerpo del slime, alejándose de lo que le pegó.
    /// Sin posición de origen (default), las gotas salen hacia arriba.
    /// </summary>
    public void PlayDamage(Vector2 damageSourcePosition)
    {
        if (!damageEnabled) return;

        Vector2 center = bodyRenderer != null ? (Vector2)bodyRenderer.bounds.center : (Vector2)transform.position;
        Vector2 away = damageSourcePosition == default ? Vector2.up : center - damageSourcePosition;
        // Un poco hacia arriba para que salten en vez de irse planas
        away = (away.normalized + Vector2.up * 0.5f).normalized;

        for (int i = 0; i < damageCount; i++)
        {
            Vector2 dir = Rotate(away, Random.Range(-damageSpread, damageSpread));
            Emit(center, dir * Random.Range(damageSpeed.x, damageSpeed.y));
        }
    }

    /// <summary>Salpicadura hacia abajo y a los lados al rebotar sobre algo.</summary>
    public void PlayPogo()
    {
        if (!pogoEnabled) return;

        for (int i = 0; i < pogoCount; i++)
        {
            Vector2 dir = Rotate(Vector2.down, Random.Range(-70f, 70f));
            Emit(FeetPosition(), dir * Random.Range(pogoSpeed.x, pogoSpeed.y));
        }
    }

    ///////////////////////// INTERNO

    private Vector3 FeetPosition()
    {
        if (playerController != null && playerController.groundCheck != null)
            return playerController.groundCheck.position;
        return transform.position;
    }

    private void Emit(Vector3 position, Vector2 velocity)
    {
        if (particles == null) return;

        int px = Random.Range(dropletSizePx.x, dropletSizePx.y + 1);
        var emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = px / pixelsPerUnit,
            startLifetime = Random.Range(lifetime.x, lifetime.y),
            startColor = dropletColors != null && dropletColors.Length > 0
                ? dropletColors[Random.Range(0, dropletColors.Length)]
                : Color.white,
        };
        particles.Emit(emit, 1);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private ParticleSystem CreateParticleSystem()
    {
        // Se crea apagado para configurarlo antes de que empiece a emitir solo
        GameObject go = new GameObject("SlimeParticles");
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // las gotas no siguen al jugador
        main.scalingMode = ParticleSystemScalingMode.Local; // ignora el Flip (escala -1) del jugador
        main.gravityModifier = gravity;
        main.maxParticles = 200;
        main.startSpeed = 0f;

        var emission = ps.emission;
        emission.enabled = false; // solo emitimos a mano con Emit()

        var shape = ps.shape;
        shape.enabled = false;

        // Se encogen y se desvanecen al final de su vida
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.3f)));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        if (collideWithGround && playerController != null && playerController.stats != null)
        {
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = playerController.stats.GroundLayer;
            collision.bounce = 0.2f;
            collision.dampen = 0.6f;
            collision.radiusScale = 0.5f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
        }

        // Cuadradito blanco de pixel; el color lo pone cada gota
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(CreatePixelSprite());

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (bodyRenderer != null)
        {
            // Mismo material y capa que el slime, una por delante
            renderer.sharedMaterial = bodyRenderer.sharedMaterial;
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = bodyRenderer.sortingOrder + 1;
        }

        go.SetActive(true);
        return ps;
    }

    private static Sprite CreatePixelSprite()
    {
        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "SlimeDropletTex",
        };
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
