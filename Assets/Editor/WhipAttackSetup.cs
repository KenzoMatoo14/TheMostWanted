using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Tools/Setup Whip Attack: configura la importación de los sprite sheets del ataque de látigo,
/// hace el slicing por grid, genera los AnimationClips (con duraciones variables y Animation
/// Events) y deja listos los Animator Controllers. Se puede volver a correr si se reexportan
/// los sprites: conserva los spriteID y los GUID de los clips, así no se rompen referencias.
/// </summary>
public static class WhipAttackSetup
{
    private const string SpriteFolder = "Assets/Sprites/Player/";
    private const float PixelsPerUnit = 48f; // el mismo que CowboySlimeIdle / CowboySlimeDash

    // ---------- Cuerpo ----------
    private const string BodyTexturePath = SpriteFolder + "CowboySlimeWindupAttack.png";
    private static readonly Vector2Int BodyCell = new Vector2Int(64, 84);
    // Pivote centrado: la base del slime queda 36 px bajo el pivote, igual que en el Idle (64x80, pivote 0.5)
    private static readonly Vector2 BodyPivot = new Vector2(0.5f, 0.5f);
    private static readonly int[] BodyFrameStartMs = { 0, 150, 230, 300, 400 };
    private const int BodyEndMs = 520;
    private const string BodyClipPath = SpriteFolder + "WhipWindup.anim";
    private const string BodyControllerPath = SpriteFolder + "PlayerAnimator.controller";
    private const string BodyStateName = "WhipWindup";
    private const string BodyReturnStateName = "Indle";

    // ---------- Látigo ----------
    private const string WhipTexturePath = SpriteFolder + "CowboySlimeWhipAttack1.png";
    private static readonly Vector2Int WhipCell = new Vector2Int(192, 112);
    private static readonly Vector2 WhipPivot = new Vector2(0.297f, 0.321f);
    private static readonly int[] WhipFrameStartMs = { 0, 60, 90, 120, 150, 190, 230, 260, 310, 360, 400, 440, 480 };
    private const int WhipEndMs = 520;
    private const string WhipClipPath = SpriteFolder + "WhipSlash.anim";
    private const string WhipControllerPath = SpriteFolder + "WhipAnimator.controller";
    private const string WhipStateName = "WhipSlash";
    private const string WhipHiddenStateName = "Hidden";

    // ---------- Eventos (en el clip del látigo) ----------
    private const float HitboxOnTime = 0.26f;   // frame 8: chasquido
    private const float HitboxOffTime = 0.36f;  // fin del frame 9
    private const float CancelOpenTime = 0.40f; // inicio de la recuperación (frames 11-13)
    private const float EndTime = 0.52f;

    [MenuItem("Tools/Setup Whip Attack")]
    public static void Run()
    {
        if (!ConfigureAndSlice(BodyTexturePath, BodyCell, BodyPivot, BodyFrameStartMs.Length)) return;
        if (!ConfigureAndSlice(WhipTexturePath, WhipCell, WhipPivot, WhipFrameStartMs.Length)) return;

        Sprite[] bodySprites = LoadSortedSprites(BodyTexturePath);
        Sprite[] whipSprites = LoadSortedSprites(WhipTexturePath);

        if (bodySprites.Length != BodyFrameStartMs.Length || whipSprites.Length != WhipFrameStartMs.Length)
        {
            Debug.LogError($"[WhipAttackSetup] Cantidad de sprites inesperada. Cuerpo: {bodySprites.Length}/{BodyFrameStartMs.Length}, Látigo: {whipSprites.Length}/{WhipFrameStartMs.Length}");
            return;
        }

        AnimationClip bodyClip = BuildClip(BodyClipPath, bodySprites, BodyFrameStartMs, BodyEndMs, null);

        AnimationEvent[] whipEvents =
        {
            MakeEvent(HitboxOnTime, "WhipHitboxOn"),
            MakeEvent(HitboxOffTime, "WhipHitboxOff"),
            MakeEvent(CancelOpenTime, "WhipOpenCancel"),
            MakeEvent(EndTime, "WhipEnd"),
        };
        AnimationClip whipClip = BuildClip(WhipClipPath, whipSprites, WhipFrameStartMs, WhipEndMs, whipEvents);

        SetupBodyController(bodyClip);
        SetupWhipController(whipClip);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WhipAttackSetup] Listo. Clips: {BodyClipPath}, {WhipClipPath}. Controllers: {BodyControllerPath} (estado '{BodyStateName}'), {WhipControllerPath}.");
    }

    ///////////////////////// IMPORTACIÓN + SLICING

    private static bool ConfigureAndSlice(string path, Vector2Int cell, Vector2 pivot, int expectedFrames)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[WhipAttackSetup] No se encontró la textura en {path}");
            return false;
        }

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        int columns = width / cell.x;
        if (width % cell.x != 0 || height != cell.y)
        {
            Debug.LogWarning($"[WhipAttackSetup] {path} mide {width}x{height}, que no es múltiplo exacto de {cell.x}x{cell.y}. Se cortan {columns} frames.");
        }
        if (columns != expectedFrames)
        {
            Debug.LogWarning($"[WhipAttackSetup] {path} tiene {columns} frames, se esperaban {expectedFrames}.");
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        // Conservar los spriteID existentes por nombre para no romper referencias al reexportar
        Dictionary<string, GUID> existingIds = dataProvider.GetSpriteRects()
            .GroupBy(r => r.name)
            .ToDictionary(g => g.Key, g => g.First().spriteID);

        string baseName = System.IO.Path.GetFileNameWithoutExtension(path);
        var rects = new List<SpriteRect>();
        var nameIdPairs = new List<SpriteNameFileIdPair>();

        for (int i = 0; i < columns; i++)
        {
            string spriteName = $"{baseName}_{i}";
            GUID id = existingIds.TryGetValue(spriteName, out GUID oldId) ? oldId : GUID.Generate();

            rects.Add(new SpriteRect
            {
                name = spriteName,
                spriteID = id,
                rect = new Rect(i * cell.x, height - cell.y, cell.x, cell.y),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                border = Vector4.zero,
            });
            nameIdPairs.Add(new SpriteNameFileIdPair(spriteName, id));
        }

        dataProvider.SetSpriteRects(rects.ToArray());

        ISpriteNameFileIdDataProvider nameFileIdProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameFileIdProvider != null)
        {
            nameFileIdProvider.SetNameFileIdPairs(nameIdPairs);
        }

        dataProvider.Apply();
        importer.SaveAndReimport();
        return true;
    }

    private static Sprite[] LoadSortedSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => s.rect.x)
            .ToArray();
    }

    ///////////////////////// CLIPS

    private static AnimationEvent MakeEvent(float time, string functionName)
    {
        return new AnimationEvent { time = time, functionName = functionName };
    }

    private static AnimationClip BuildClip(string path, Sprite[] sprites, int[] startMs, int endMs, AnimationEvent[] events)
    {
        // 100 fps = pasos de 10 ms, así todos los tiempos de las tablas caen exactos en un frame
        var clip = new AnimationClip
        {
            name = System.IO.Path.GetFileNameWithoutExtension(path),
            frameRate = 100f,
        };

        var keys = new ObjectReferenceKeyframe[sprites.Length + 1];
        for (int i = 0; i < sprites.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = startMs[i] / 1000f, value = sprites[i] };
        }
        // Key final que sostiene el último sprite hasta el fin: define la duración total del clip
        keys[sprites.Length] = new ObjectReferenceKeyframe { time = endMs / 1000f, value = sprites[sprites.Length - 1] };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AnimationUtility.SetAnimationEvents(clip, events ?? new AnimationEvent[0]);

        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // Sobrescribir en el mismo asset para conservar su GUID (y las referencias del Animator)
        EditorUtility.CopySerialized(clip, existing);
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(clip);
        return existing;
    }

    ///////////////////////// ANIMATOR CONTROLLERS

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state.name == name) return child.state;
        }
        return null;
    }

    private static void SetupBodyController(AnimationClip bodyClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BodyControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[WhipAttackSetup] No se encontró {BodyControllerPath}");
            return;
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        AnimatorState attackState = FindState(stateMachine, BodyStateName);
        if (attackState == null)
        {
            attackState = stateMachine.AddState(BodyStateName, new Vector3(300f, 200f, 0f));
        }
        attackState.motion = bodyClip;
        attackState.speed = 1f;

        AnimatorState returnState = FindState(stateMachine, BodyReturnStateName) ?? stateMachine.defaultState;
        if (returnState == null)
        {
            Debug.LogError($"[WhipAttackSetup] No se encontró el estado '{BodyReturnStateName}' para volver al terminar el ataque.");
            return;
        }

        // Una sola transición de salida: al terminar el clip vuelve a Idle
        foreach (AnimatorStateTransition transition in attackState.transitions.ToArray())
        {
            attackState.RemoveTransition(transition);
        }
        AnimatorStateTransition toIdle = attackState.AddTransition(returnState);
        toIdle.hasExitTime = true;
        toIdle.exitTime = 1f;
        toIdle.hasFixedDuration = true;
        toIdle.duration = 0f;

        EditorUtility.SetDirty(controller);
    }

    private static void SetupWhipController(AnimationClip whipClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(WhipControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(WhipControllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Estado vacío por defecto: al activar WhipPivot no se reproduce nada hasta que WhipAttack lo pida
        AnimatorState hidden = FindState(stateMachine, WhipHiddenStateName);
        if (hidden == null)
        {
            hidden = stateMachine.AddState(WhipHiddenStateName, new Vector3(300f, 0f, 0f));
        }
        stateMachine.defaultState = hidden;

        // Sin transición de salida: se queda en el último frame y WhipAttack apaga el GameObject
        AnimatorState slash = FindState(stateMachine, WhipStateName);
        if (slash == null)
        {
            slash = stateMachine.AddState(WhipStateName, new Vector3(300f, 100f, 0f));
        }
        slash.motion = whipClip;
        slash.speed = 1f;

        EditorUtility.SetDirty(controller);
    }
}
