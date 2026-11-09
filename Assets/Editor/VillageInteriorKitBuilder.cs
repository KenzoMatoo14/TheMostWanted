using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds the Village interior kit from the "Legacy Vania Pack - Village" tileset:
// one Tile per sprite, rule tiles for big surfaces, a Tile Palette and a ready-made interior prefab.
// Menu: Tools > Village > Build Interior Kit (safe to run again; it rebuilds everything).
public static class VillageInteriorKitBuilder
{
    const string PackDir = "Assets/Sprites/Legacy Vania Pack - Village - Update 0.4";
    const string SheetPath = PackDir + "/Assets/Tiles.png";
    const string KitDir = PackDir + "/InteriorKit";
    const string ExtraPath = KitDir + "/Interior_Extra.png";
    const string LayoutPath = KitDir + "/InteriorLayout.txt";
    const string TilesDir = KitDir + "/Tiles";
    const string RuleDir = KitDir + "/RuleTiles";

    static Dictionary<string, Sprite> sprites;
    static Dictionary<string, Tile> tiles;

    [MenuItem("Tools/Village/Build Interior Kit")]
    public static void Build()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(ExtraPath, ImportAssetOptions.ForceUpdate);
        EnsureFolder(TilesDir);
        EnsureFolder(RuleDir);

        sprites = new Dictionary<string, Sprite>();
        foreach (var path in new[] { SheetPath, ExtraPath })
            foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                sprites[s.name] = s;
        if (sprites.Count == 0) { Debug.LogError("[VillageKit] No sprites found. Is Tiles.png sliced?"); return; }

        BuildTiles();
        var rules = BuildRuleTiles();
        BuildPalette(rules);
        BuildInteriorPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[VillageKit] Done: {tiles.Count} tiles, {rules.Count} rule tiles, palette and prefab in {KitDir}");
    }

    // ---------- Tiles ----------
    static void BuildTiles()
    {
        tiles = new Dictionary<string, Tile>();
        foreach (var kv in sprites)
        {
            string path = $"{TilesDir}/{kv.Key}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = kv.Value;
            tile.colliderType = Tile.ColliderType.Sprite;
            EditorUtility.SetDirty(tile);
            tiles[kv.Key] = tile;
        }
    }

    // The sheets are stored with every 16px tile in a 20px cell (2px of repeated edge around it),
    // so the camera never picks up a line from the neighbouring tile.
    const int Cell = 20, Pad = 2;
    static int Col(Sprite s) => ((int)s.rect.x - Pad) / Cell;
    static int RowFromTop(Sprite s) => ((int)(s.texture.height - s.rect.y) + Pad) / Cell - 1;

    // Sheet coordinates (column, row from the top) -> sprite.
    static Sprite S(int c, int r)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        return sprites.Values.First(s => s.texture == tex && Col(s) == c && RowFromTop(s) == r);
    }

    // ---------- Rule tiles ----------
    const int T = RuleTile.TilingRule.Neighbor.This;
    const int N = RuleTile.TilingRule.Neighbor.NotThis;
    static readonly Vector3Int Up = Vector3Int.up, Down = Vector3Int.down, Left = Vector3Int.left, Right = Vector3Int.right;

    static RuleTile.TilingRule Rule(Sprite[] spr, Tile.ColliderType col, params (Vector3Int pos, int state)[] nb)
    {
        var r = new RuleTile.TilingRule
        {
            m_Sprites = spr,
            m_Output = spr.Length > 1 ? RuleTile.TilingRuleOutput.OutputSprite.Random : RuleTile.TilingRuleOutput.OutputSprite.Single,
            m_ColliderType = col,
            m_Neighbors = nb.Select(n => n.state).ToList(),
            m_NeighborPositions = nb.Select(n => n.pos).ToList(),
        };
        return r;
    }

    static RuleTile MakeRuleTile(string name, Sprite def, Tile.ColliderType col, List<RuleTile.TilingRule> rules)
    {
        string path = $"{RuleDir}/{name}.asset";
        var rt = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
        if (rt == null) { rt = ScriptableObject.CreateInstance<RuleTile>(); AssetDatabase.CreateAsset(rt, path); }
        rt.m_DefaultSprite = def;
        rt.m_DefaultColliderType = col;
        for (int i = 0; i < rules.Count; i++) rules[i].m_Id = i;
        rt.m_TilingRules = rules;
        EditorUtility.SetDirty(rt);
        return rt;
    }

    static List<RuleTile> BuildRuleTiles()
    {
        var none = Tile.ColliderType.None;
        var grid = Tile.ColliderType.Grid;
        var list = new List<RuleTile>();

        // 1) Plank wall: paint any rectangle, borders are placed automatically.
        list.Add(MakeRuleTile("1_Pared_Tablones", S(23, 1), none, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(22, 0) }, none, (Up, N), (Left, N)),
            Rule(new[] { S(24, 0) }, none, (Up, N), (Right, N)),
            Rule(new[] { S(23, 0) }, none, (Up, N)),
            Rule(new[] { S(22, 2) }, none, (Down, N), (Left, N)),
            Rule(new[] { S(24, 2) }, none, (Down, N), (Right, N)),
            Rule(new[] { S(23, 2) }, none, (Down, N)),
            Rule(new[] { S(22, 1), S(22, 1), S(22, 3) }, none, (Left, N)),
            Rule(new[] { S(24, 1), S(24, 1), S(24, 3) }, none, (Right, N)),
            Rule(new[] { S(23, 1), S(23, 1), S(23, 1), S(23, 3) }, none),
        }));

        // 2) Stone floor / ceiling beam (solid).
        list.Add(MakeRuleTile("2_Piso_Piedra", S(17, 4), grid, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(17, 4), S(20, 3), S(19, 4), S(20, 3) }, grid),
        }));

        // 3) Wooden floor (solid).
        list.Add(MakeRuleTile("3_Piso_Madera", S(19, 0), grid, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(19, 0), S(20, 0), S(21, 0) }, grid),
        }));

        // 4) Stone pillar with the wood trim on the left (solid). Paint vertically.
        list.Add(MakeRuleTile("4_Pilar_Piedra", S(17, 1), grid, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(17, 0) }, grid, (Up, N)),
            Rule(new[] { S(17, 1), S(17, 2), S(17, 3) }, grid),
        }));

        // 5) Wooden support beam (decoration). Paint a "T": a row of 3 at the ceiling and the
        //    pole going down from the middle one. The row becomes the Y brace automatically.
        list.Add(MakeRuleTile("5_Viga_Madera", S(23, 4), none, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(21, 4) }, none, (Up, N), (Down, T)),   // top of the pole: center of the Y
            Rule(new[] { S(20, 4) }, none, (Down, N), (Right, T)), // left arm of the Y
            Rule(new[] { S(22, 4) }, none, (Down, N), (Left, T)),  // right arm of the Y
            Rule(new[] { S(23, 4) }, none),                        // pole
        }));

        // 6) Same stone pillar mirrored: wood trim on the right (solid). Paint vertically.
        list.Add(MakeRuleTile("6_Pilar_Piedra_Der", S(18, 1), grid, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(18, 0) }, grid, (Up, N)),
            Rule(new[] { S(18, 1), S(18, 2), S(18, 3) }, grid),
        }));

        // 7) Windows (decoration). One tile alone = lantern, 1x2 vertical = tall window, 2x2 = big window.
        list.Add(MakeRuleTile("7_Ventanas", S(25, 3), none, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(25, 3) }, none, (Up, N), (Down, N), (Left, N), (Right, N)), // alone: lantern
            Rule(new[] { S(26, 3) }, none, (Up, N), (Down, T), (Left, N), (Right, N)), // tall window top
            Rule(new[] { S(26, 4) }, none, (Up, T), (Down, N), (Left, N), (Right, N)), // tall window bottom
            Rule(new[] { S(24, 4) }, none, (Up, N), (Left, N), (Right, T), (Down, T)), // big window top-left
            Rule(new[] { S(25, 4) }, none, (Up, N), (Right, N), (Left, T), (Down, T)), // big window top-right
            Rule(new[] { S(24, 5) }, none, (Down, N), (Left, N), (Right, T), (Up, T)), // big window bottom-left
            Rule(new[] { S(25, 5) }, none, (Down, N), (Right, N), (Left, T), (Up, T)), // big window bottom-right
        }));

        // 8) Dark hole in the blue wall: paint any shape, the ragged blue border is added around it.
        var UL = new Vector3Int(-1, 1, 0); var UR = new Vector3Int(1, 1, 0);
        var DL = new Vector3Int(-1, -1, 0); var DR = new Vector3Int(1, -1, 0);
        list.Add(MakeRuleTile("8_Hueco_Azul", S(13, 1), none, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(12, 0) }, none, (Up, N), (Left, N)),
            Rule(new[] { S(14, 0) }, none, (Up, N), (Right, N)),
            Rule(new[] { S(12, 2) }, none, (Down, N), (Left, N)),
            Rule(new[] { S(14, 2) }, none, (Down, N), (Right, N)),
            Rule(new[] { S(13, 0) }, none, (Up, N)),
            Rule(new[] { S(13, 2) }, none, (Down, N)),
            Rule(new[] { S(12, 1) }, none, (Left, N)),
            Rule(new[] { S(14, 1) }, none, (Right, N)),
            Rule(new[] { S(12, 3) }, none, (UL, N)), // inner corners (L-shaped holes)
            Rule(new[] { S(13, 3) }, none, (UR, N)),
            Rule(new[] { S(12, 4) }, none, (DL, N)),
            Rule(new[] { S(13, 4) }, none, (DR, N)),
            Rule(new[] { S(13, 1) }, none),
        }));

        // 9) Blue roof edge: paint a vertical line, the peak goes on top.
        list.Add(MakeRuleTile("9_Borde_Techo_Azul", S(14, 4), none, new List<RuleTile.TilingRule>
        {
            Rule(new[] { S(14, 3) }, none, (Up, N)),
            Rule(new[] { S(14, 4), S(14, 4), S(14, 5) }, none),
        }));

        return list;
    }

    // ---------- Palette ----------
    static void BuildPalette(List<RuleTile> rules)
    {
        string path = $"{KitDir}/Village_Palette.prefab";
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        if (!exists)
            GridPaletteUtility.CreateNewPalette(KitDir, "Village_Palette", GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Manual, Vector3.one, GridLayout.CellSwizzle.XYZ);

        var root = PrefabUtility.LoadPrefabContents(path);
        var tm = root.GetComponentInChildren<Tilemap>();

        // Top row: rule tiles (refreshed every run, so new rule tiles show up).
        for (int i = 0; i < rules.Count; i++) tm.SetTile(new Vector3Int(i * 2, 2, 0), rules[i]);

        // An existing palette keeps whatever was added to it by hand.
        if (exists)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        // Whole sheet with the same layout as Tiles.png, so multi-tile furniture can be stamped.
        var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        var extra = AssetDatabase.LoadAssetAtPath<Texture2D>(ExtraPath);
        foreach (var kv in tiles)
        {
            var s = kv.Value.sprite;
            int c = Col(s);
            int rFromTop = RowFromTop(s);
            if (s.texture == sheet) tm.SetTile(new Vector3Int(c, -rFromTop, 0), kv.Value);
            else if (s.texture == extra) tm.SetTile(new Vector3Int(c, -(sheet.height / Cell) - 1 - rFromTop, 0), kv.Value);
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ---------- Ready-made interior ----------
    static void BuildInteriorPrefab()
    {
        string path = $"{KitDir}/Village_Interior.prefab";
        // Never overwrite it: it may already have hand edits. Delete it to rebuild from scratch.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject("Village_Interior");
        root.AddComponent<Grid>();

        var maps = new Dictionary<string, Tilemap>();
        string[] order = { "Background", "Detail", "Decor", "Props", "Ground" };
        int[] sort = { -20, -19, -18, -17, -16 };
        for (int i = 0; i < order.Length; i++)
        {
            var go = new GameObject(order[i]);
            go.transform.SetParent(root.transform, false);
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = sort[i];
            maps[order[i]] = tm;
        }

        // Only the Ground tilemap collides.
        var ground = maps["Ground"].gameObject;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) ground.layer = groundLayer;
        var rb = ground.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var tc = ground.AddComponent<TilemapCollider2D>();
        ground.AddComponent<CompositeCollider2D>();
        tc.compositeOperation = Collider2D.CompositeOperation.Merge;

        var flip = Matrix4x4.Scale(new Vector3(-1, 1, 1));
        foreach (var line in File.ReadAllLines(LayoutPath))
        {
            var p = line.Split(' ');
            if (p.Length < 5) continue;
            if (!tiles.TryGetValue(p[3], out var tile)) { Debug.LogWarning($"[VillageKit] Missing tile {p[3]}"); continue; }
            var pos = new Vector3Int(int.Parse(p[1]), int.Parse(p[2]), 0);
            var tm = maps[p[0]];
            tm.SetTile(pos, tile);
            if (p[4] == "1") tm.SetTransformMatrix(pos, flip);
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
