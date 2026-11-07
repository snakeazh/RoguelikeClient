using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// 生成战斗用的 2D 精灵资源（PNG）与显示用预制体。
    /// 美术是程序化画的占位精灵：形状/描边/高光都在代码里，正式美术直接覆盖同名 PNG 即可，
    /// 但请保持「1 个精灵 = 1 个世界单位」的导入设置（Pixels Per Unit = 图片像素尺寸）。
    /// 菜单：Tools/Client/Rebuild Battle Sprites
    /// </summary>
    public static class BattleSpriteBuilder
    {
        private const string SpriteDir = "Assets/Res/Battle/Sprites";
        private const string PrefabDir = "Assets/Res/Battle/Prefabs";

        [MenuItem("Tools/Client/Rebuild Battle Sprites", priority = 30)]
        public static void RebuildAll()
        {
            EnsureFolder("Assets/Res/Battle");
            EnsureFolder(SpriteDir);
            EnsureFolder(PrefabDir);

            BuildSprites();
            AssetDatabase.Refresh();

            BuildPrefabs();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BattleSpriteBuilder] battle sprites + prefabs rebuilt.");
        }

        // ---------------- 精灵 ----------------

        private static void BuildSprites()
        {
            // 地图：地砖 1 世界单位一格，横向/纵向墙各一块（Tiled 平铺，见 ConfigureSprite 的 tile 参数）。
            DrawTile("tile_floor", 64, FloorTile);
            DrawTile("wall_h", 64, WallHorizontalTile);
            DrawTile("wall_v", 64, WallVerticalTile);

            Draw("ground", 128, Ground);
            Draw("player", 64, Player);

            Draw("enemy_goblin", 64, p => Blob(p, 0.42f, Hex("8BC34A"), Hex("558B2F"), eyes: true));
            Draw("enemy_bunny", 64, Bunny);
            Draw("enemy_mochi", 64, p => Blob(p, 0.46f, Hex("FFCC80"), Hex("E0A860"), sleepy: true));
            Draw("enemy_gunner", 64, Gunner);
            Draw("enemy_bomb", 64, BombSlime);
            Draw("enemy_split", 64, SplitSlime);
            Draw("enemy_slug", 64, Slug);
            Draw("enemy_small", 48, p => Blob(p, 0.44f, Hex("81D4FA"), Hex("4FA3C7"), eyes: true));

            Draw("boss_gargoyle", 128, Gargoyle);
            Draw("boss_abyss", 128, AbyssLord);
            Draw("boss_slime", 128, BossSlime);

            Draw("proj_arrow", 32, Arrow);
            Draw("proj_orb", 32, Orb);
            Draw("proj_magic", 32, Star);
            Draw("proj_bomb", 32, Bomb);
            Draw("proj_slash", 32, Slash);
            Draw("proj_enemy", 32, p => OrbWith(p, Hex("FF5252"), Hex("FFCDD2")));

            Draw("pickup_exp", 40, p => Gem(p, Hex("7BE88B"), Hex("2E7D32")));
            Draw("pickup_exp_big", 48, p => Gem(p, Hex("B9F6CA"), Hex("2E7D32")));
            Draw("pickup_cake", 48, Cake);
            Draw("pickup_gold", 40, Coin);
            Draw("pickup_chest", 56, Chest);
            Draw("pickup_magnet", 44, Magnet);
            Draw("pickup_bomb", 44, p => BombShape(p, Hex("37474F"), Hex("90A4AE")));
            Draw("pickup_hourglass", 44, Hourglass);

            Draw("blade", 128, Blade);
            Draw("aura", 128, Aura);
            Draw("slash_arc", 256, SlashArc);
            Draw("cannon", 64, Cannon);
            Draw("cannon_base", 48, CannonBase);
            Draw("shadow", 64, Shadow);
        }

        private static void Draw(string name, int size, System.Action<Pix> paint)
        {
            var pix = new Pix(size);
            paint(pix);
            pix.Outline(Hex("000000", 0.55f), 2);
            var path = $"{SpriteDir}/{name}.png";
            File.WriteAllBytes(path, pix.ToPng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(path, size);
        }

        /// <summary>
        /// 画可平铺的地砖/墙面：不描边（描边会在每次平铺接缝处画出黑线），
        /// 并让图集边缘的砖缝自己接上，所以图案必须首尾相接。
        /// </summary>
        private static void DrawTile(string name, int size, System.Action<Pix> paint)
        {
            var pix = new Pix(size);
            paint(pix);
            var path = $"{SpriteDir}/{name}.png";
            File.WriteAllBytes(path, pix.ToPng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(path, size, tile: true);
        }

        private static void ConfigureSprite(string path, int size, bool tile = false)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // 1 个精灵 = 1 个世界单位
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;

            // 平铺用：必须 Repeat + FullRect，否则 SpriteRenderer.Tiled 只会画一个拉伸的图。
            importer.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = tile ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);

            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---------------- 预制体 ----------------

        private static void BuildPrefabs()
        {
            SavePrefab("Player", "player", 12, true);
            SavePrefab("Enemy", "enemy_goblin", 10, true);
            SavePrefab("Projectile", "proj_orb", 20, false);
            SavePrefab("Pickup", "pickup_exp", 15, false);
            SavePrefab("Blade", "blade", 18, false);
        }

        private static void SavePrefab(string prefabName, string spriteName, int order, bool withShadow)
        {
            var root = new GameObject(prefabName);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var sr = body.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spriteName);
            sr.sortingOrder = order;

            if (withShadow)
            {
                var shadow = new GameObject("Shadow");
                shadow.transform.SetParent(root.transform, false);
                shadow.transform.localPosition = new Vector3(0f, -0.28f, 0f);
                shadow.transform.localScale = new Vector3(1.1f, 0.55f, 1f);
                var shadowSr = shadow.AddComponent<SpriteRenderer>();
                shadowSr.sprite = LoadSprite("shadow");
                shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
                shadowSr.sortingOrder = order - 2;
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{prefabName}.prefab");
            Object.DestroyImmediate(root);
        }

        private static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
        }

        // ---------------- 画法 ----------------

        private static void Ground(Pix p)
        {
            var baseColor = Hex("1A1F2B");
            var lineColor = Hex("2B3448", 0.85f);
            p.Clear(baseColor);

            // 8×8 网格：竖屏拉伸后仍是方块，读起来像地砖
            var step = p.Size / 8;
            for (var i = 0; i <= 8; i++)
            {
                p.HLine(i * step, lineColor);
                p.VLine(i * step, lineColor);
            }
        }

        private static void FloorTile(Pix p)
        {
            var baseColor = Hex("1A1F2B");
            var lineColor = Hex("2B3448", 0.9f);
            var speckColor = Hex("222B3D", 0.7f);
            p.Clear(baseColor);

            // 边缘两像素画砖缝：平铺时相邻两块的缝正好拼成 2px，看起来是连续网格。
            p.HLine(0, lineColor);
            p.HLine(1, lineColor);
            p.VLine(0, lineColor);
            p.VLine(1, lineColor);

            // 固定伪随机的小斑点，避免大片纯色；位置写成常量保证重复生成结果一致。
            for (var i = 0; i < 14; i++)
            {
                var x = (i * 37 + 11) % p.Size;
                var y = (i * 53 + 7) % p.Size;
                p.Set(x, y, speckColor);
                p.Set(x + 1, y, speckColor);
            }
        }

        private static void WallHorizontalTile(Pix p)
        {
            // 64×64 里只画中间 40px：世界单位 1 高的 tile 中，墙面视觉厚度约 0.62。
            var band = Hex("39435A");
            var brick = Hex("2F3749");
            var top = Hex("4C5871");
            var c = p.Size / 2f;

            p.Rect(c, c, p.Size, 40f, band);
            p.HLine((int)(c + 19f), top);
            p.HLine((int)(c - 19f), brick);
            p.HLine((int)(c + 1f), brick);

            // 竖缝错开，平铺后砖块交错。
            p.VLine(0, brick);
            p.VLine(32, brick);
            p.Rect(16f, c + 10f, 2f, 18f, brick);
            p.Rect(48f, c - 10f, 2f, 18f, brick);
        }

        private static void WallVerticalTile(Pix p)
        {
            var band = Hex("39435A");
            var brick = Hex("2F3749");
            var highlight = Hex("4C5871");
            var c = p.Size / 2f;

            p.Rect(c, c, 40f, p.Size, band);
            p.VLine((int)(c - 19f), highlight);
            p.VLine((int)(c + 19f), brick);
            p.VLine((int)(c - 1f), brick);

            p.HLine(0, brick);
            p.HLine(32, brick);
            p.Rect(c + 10f, 16f, 18f, 2f, brick);
            p.Rect(c - 10f, 48f, 18f, 2f, brick);
        }

        private static void Player(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c, p.Size * 0.40f, Hex("4FDBFF"));
            p.Circle(c, c - p.Size * 0.06f, p.Size * 0.30f, Hex("B3F2FF", 0.85f));
            // 面朝上的白色小三角
            p.Triangle(c, p.Size * 0.80f, p.Size * 0.13f, p.Size * 0.11f, Hex("FFFFFF", 0.95f));
            p.Circle(c - p.Size * 0.12f, c - p.Size * 0.14f, p.Size * 0.035f, Hex("FFFFFF", 0.7f));
        }

        private static void Bunny(Pix p)
        {
            var c = p.Size / 2f;
            p.Ellipse(c, c - p.Size * 0.04f, p.Size * 0.34f, p.Size * 0.30f, Hex("F5F5F5"));
            p.Ellipse(c - p.Size * 0.13f, c + p.Size * 0.28f, p.Size * 0.07f, p.Size * 0.20f, Hex("F5F5F5"));
            p.Ellipse(c + p.Size * 0.13f, c + p.Size * 0.28f, p.Size * 0.07f, p.Size * 0.20f, Hex("F5F5F5"));
            p.Ellipse(c - p.Size * 0.13f, c + p.Size * 0.28f, p.Size * 0.035f, p.Size * 0.14f, Hex("F8BBD0"));
            p.Ellipse(c + p.Size * 0.13f, c + p.Size * 0.28f, p.Size * 0.035f, p.Size * 0.14f, Hex("F8BBD0"));
            Eyes(p, c, c - p.Size * 0.02f, p.Size * 0.10f, Hex("3E2723"));
        }

        private static void Gunner(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c - p.Size * 0.05f, p.Size * 0.33f, Hex("A5D6A7"));
            p.Ellipse(c, c + p.Size * 0.20f, p.Size * 0.36f, p.Size * 0.10f, Hex("8D6E63")); // 帽檐
            p.Ellipse(c, c + p.Size * 0.27f, p.Size * 0.20f, p.Size * 0.08f, Hex("6D4C41"));
            p.Rect(c + p.Size * 0.18f, c - p.Size * 0.02f, p.Size * 0.10f, p.Size * 0.22f, Hex("546E7A")); // 炮管
            Eyes(p, c - p.Size * 0.02f, c, p.Size * 0.09f, Hex("1B1B1B"));
        }

        private static void BombSlime(Pix p)
        {
            var c = p.Size / 2f;
            p.Blob(c, c, p.Size * 0.40f, Hex("FF8A65"));
            p.Circle(c - p.Size * 0.10f, c + p.Size * 0.10f, p.Size * 0.06f, Hex("FFCCBC", 0.9f));
            p.Line(c + p.Size * 0.10f, c + p.Size * 0.30f, c + p.Size * 0.18f, c + p.Size * 0.42f, 3, Hex("4E342E"));
            p.Circle(c + p.Size * 0.19f, c + p.Size * 0.43f, p.Size * 0.045f, Hex("FFD54F"));
            Eyes(p, c, c - p.Size * 0.02f, p.Size * 0.09f, Hex("3E2723"));
        }

        private static void SplitSlime(Pix p)
        {
            var c = p.Size / 2f;
            p.Blob(c, c, p.Size * 0.42f, Hex("81D4FA"));
            p.Line(c - p.Size * 0.02f, c + p.Size * 0.26f, c + p.Size * 0.04f, c + p.Size * 0.05f, 2, Hex("0D47A1", 0.8f));
            p.Line(c + p.Size * 0.04f, c + p.Size * 0.05f, c - p.Size * 0.03f, c - p.Size * 0.16f, 2, Hex("0D47A1", 0.8f));
            Eyes(p, c, c + p.Size * 0.05f, p.Size * 0.09f, Hex("0D47A1"));
        }

        private static void Slug(Pix p)
        {
            var c = p.Size / 2f;
            p.Ellipse(c, c - p.Size * 0.05f, p.Size * 0.42f, p.Size * 0.24f, Hex("CE93D8"));
            p.Ellipse(c, c - p.Size * 0.12f, p.Size * 0.34f, p.Size * 0.12f, Hex("8E24AA", 0.55f));
            p.Line(c - p.Size * 0.12f, c + p.Size * 0.16f, c - p.Size * 0.20f, c + p.Size * 0.34f, 2, Hex("6A1B9A"));
            p.Line(c + p.Size * 0.12f, c + p.Size * 0.16f, c + p.Size * 0.20f, c + p.Size * 0.34f, 2, Hex("6A1B9A"));
            p.Circle(c - p.Size * 0.20f, c + p.Size * 0.34f, p.Size * 0.05f, Hex("F3E5F5"));
            p.Circle(c + p.Size * 0.20f, c + p.Size * 0.34f, p.Size * 0.05f, Hex("F3E5F5"));
        }

        private static void Gargoyle(Pix p)
        {
            var c = p.Size / 2f;
            p.Triangle(c - p.Size * 0.30f, c + p.Size * 0.10f, p.Size * 0.26f, p.Size * 0.18f, Hex("78909C"));
            p.Triangle(c + p.Size * 0.30f, c + p.Size * 0.10f, p.Size * 0.26f, p.Size * 0.18f, Hex("78909C"));
            p.Circle(c, c - p.Size * 0.02f, p.Size * 0.32f, Hex("90A4AE"));
            p.Rect(c - p.Size * 0.22f, c + p.Size * 0.30f, p.Size * 0.06f, p.Size * 0.12f, Hex("546E7A"));
            p.Rect(c + p.Size * 0.22f, c + p.Size * 0.30f, p.Size * 0.06f, p.Size * 0.12f, Hex("546E7A"));
            Eyes(p, c, c, p.Size * 0.12f, Hex("FF7043"));
        }

        private static void AbyssLord(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c - p.Size * 0.04f, p.Size * 0.36f, Hex("3F51B5"));
            p.Triangle(c - p.Size * 0.24f, c + p.Size * 0.26f, p.Size * 0.24f, p.Size * 0.22f, Hex("5C6BC0"));
            p.Triangle(c + p.Size * 0.24f, c + p.Size * 0.26f, p.Size * 0.24f, p.Size * 0.22f, Hex("5C6BC0"));
            p.Circle(c, c + p.Size * 0.02f, p.Size * 0.18f, Hex("1A237E", 0.85f));
            p.Circle(c - p.Size * 0.07f, c + p.Size * 0.04f, p.Size * 0.045f, Hex("B388FF"));
            p.Circle(c + p.Size * 0.07f, c + p.Size * 0.04f, p.Size * 0.045f, Hex("B388FF"));
        }

        private static void BossSlime(Pix p)
        {
            var c = p.Size / 2f;
            p.Blob(c, c - p.Size * 0.02f, p.Size * 0.40f, Hex("7E57C2"));
            p.Ellipse(c, c + p.Size * 0.06f, p.Size * 0.30f, p.Size * 0.20f, Hex("9575CD", 0.55f));
            // 王冠
            p.Rect(c - p.Size * 0.20f, c + p.Size * 0.34f, p.Size * 0.40f, p.Size * 0.07f, Hex("FFD54F"));
            p.Triangle(c - p.Size * 0.16f, c + p.Size * 0.42f, p.Size * 0.10f, p.Size * 0.10f, Hex("FFD54F"));
            p.Triangle(c, c + p.Size * 0.44f, p.Size * 0.12f, p.Size * 0.12f, Hex("FFD54F"));
            p.Triangle(c + p.Size * 0.16f, c + p.Size * 0.42f, p.Size * 0.10f, p.Size * 0.10f, Hex("FFD54F"));
            Eyes(p, c, c - p.Size * 0.04f, p.Size * 0.13f, Hex("311B92"));
        }

        private static void Arrow(Pix p)
        {
            var c = p.Size / 2f;
            p.Rect(c, c, p.Size * 0.10f, p.Size * 0.62f, Hex("E8F5E9"));
            p.Triangle(c, p.Size * 0.94f, p.Size * 0.34f, p.Size * 0.26f, Hex("AEEA00"));
            p.Rect(c, c - p.Size * 0.28f, p.Size * 0.34f, p.Size * 0.08f, Hex("8BC34A"));
        }

        private static void Orb(Pix p)
        {
            OrbWith(p, Hex("4FC3F7"), Hex("E1F5FE"));
        }

        private static void OrbWith(Pix p, Color body, Color core)
        {
            var c = p.Size / 2f;
            p.Circle(c, c, p.Size * 0.40f, body);
            p.Circle(c, c, p.Size * 0.22f, core);
        }

        private static void Star(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c, p.Size * 0.34f, Hex("9C27B0", 0.85f));
            p.Triangle(c, p.Size * 0.94f, p.Size * 0.44f, p.Size * 0.22f, Hex("EA80FC"));
            p.Triangle(c, p.Size * 0.06f, p.Size * 0.44f, p.Size * 0.22f, Hex("EA80FC"));
            p.Triangle(c - p.Size * 0.44f, c, p.Size * 0.22f, p.Size * 0.44f, Hex("EA80FC"), true);
            p.Triangle(c + p.Size * 0.44f, c, p.Size * 0.22f, p.Size * 0.44f, Hex("EA80FC"), true);
            p.Circle(c, c, p.Size * 0.14f, Hex("FFFFFF"));
        }

        private static void Bomb(Pix p)
        {
            BombShape(p, Hex("263238"), Hex("607D8B"));
        }

        private static void BombShape(Pix p, Color body, Color shine)
        {
            var c = p.Size / 2f;
            p.Circle(c, c - p.Size * 0.04f, p.Size * 0.36f, body);
            p.Circle(c - p.Size * 0.12f, c + p.Size * 0.10f, p.Size * 0.07f, shine);
            p.Line(c + p.Size * 0.10f, c + p.Size * 0.28f, c + p.Size * 0.20f, c + p.Size * 0.42f, 3, Hex("8D6E63"));
            p.Circle(c + p.Size * 0.21f, c + p.Size * 0.43f, p.Size * 0.06f, Hex("FFCA28"));
        }

        private static void Slash(Pix p)
        {
            var c = p.Size / 2f;
            p.Triangle(c, p.Size * 0.92f, p.Size * 0.60f, p.Size * 0.36f, Hex("FFF9C4", 0.9f));
            p.Triangle(c, p.Size * 0.12f, p.Size * 0.60f, p.Size * 0.36f, Hex("FFF9C4", 0.35f));
        }

        private static void Gem(Pix p, Color top, Color bottom)
        {
            var c = p.Size / 2f;
            p.Triangle(c, p.Size * 0.94f, p.Size * 0.40f, p.Size * 0.44f, top);
            p.Triangle(c, p.Size * 0.06f, p.Size * 0.40f, p.Size * 0.44f, bottom, true);
            p.Circle(c - p.Size * 0.06f, c + p.Size * 0.16f, p.Size * 0.05f, Hex("FFFFFF", 0.8f));
        }

        private static void Cake(Pix p)
        {
            var c = p.Size / 2f;
            p.Rect(c, c - p.Size * 0.08f, p.Size * 0.62f, p.Size * 0.34f, Hex("FFE0B2"));
            p.Rect(c, c + p.Size * 0.14f, p.Size * 0.62f, p.Size * 0.12f, Hex("F48FB1"));
            p.Circle(c, c + p.Size * 0.26f, p.Size * 0.07f, Hex("E53935"));
            p.Rect(c, c - p.Size * 0.26f, p.Size * 0.62f, p.Size * 0.06f, Hex("BCAAA4"));
        }

        private static void Coin(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c, p.Size * 0.40f, Hex("FFC107"));
            p.Circle(c, c, p.Size * 0.28f, Hex("FFE082"));
            p.Rect(c, c, p.Size * 0.08f, p.Size * 0.30f, Hex("F9A825"));
        }

        private static void Chest(Pix p)
        {
            var c = p.Size / 2f;
            p.Rect(c, c - p.Size * 0.10f, p.Size * 0.70f, p.Size * 0.40f, Hex("8D6E63"));
            p.Rect(c, c + p.Size * 0.14f, p.Size * 0.70f, p.Size * 0.14f, Hex("A1887F"));
            p.Rect(c, c - p.Size * 0.10f, p.Size * 0.12f, p.Size * 0.40f, Hex("FFD54F"));
            p.Circle(c, c - p.Size * 0.10f, p.Size * 0.06f, Hex("5D4037"));
        }

        private static void Magnet(Pix p)
        {
            var c = p.Size / 2f;
            p.Circle(c, c, p.Size * 0.34f, Hex("E53935"));
            p.Circle(c, c - p.Size * 0.08f, p.Size * 0.18f, Hex("000000", 0f));
            p.Rect(c, c - p.Size * 0.34f, p.Size * 0.68f, p.Size * 0.14f, Hex("000000", 0f));
            p.Rect(c - p.Size * 0.24f, c - p.Size * 0.30f, p.Size * 0.14f, p.Size * 0.14f, Hex("ECEFF1"));
            p.Rect(c + p.Size * 0.24f, c - p.Size * 0.30f, p.Size * 0.14f, p.Size * 0.14f, Hex("ECEFF1"));
        }

        private static void Hourglass(Pix p)
        {
            var c = p.Size / 2f;
            p.Triangle(c, p.Size * 0.90f, p.Size * 0.46f, p.Size * 0.36f, Hex("B3E5FC"), true);
            p.Triangle(c, p.Size * 0.10f, p.Size * 0.46f, p.Size * 0.36f, Hex("B3E5FC"));
            p.Rect(c, c + p.Size * 0.40f, p.Size * 0.58f, p.Size * 0.06f, Hex("6D4C41"));
            p.Rect(c, c - p.Size * 0.40f, p.Size * 0.58f, p.Size * 0.06f, Hex("6D4C41"));
        }

        /// <summary>
        /// 大剑刀光：一张 120° 的月牙弧，弧心在正中央、朝 +X 张开（0 度方向 = 弧的最高点）。
        /// 径向 56%→100% 为弧带，中间最亮、两边渐隐；角度两端收细，看起来像挥出去的一道刀风。
        /// </summary>
        private static void SlashArc(Pix p)
        {
            var s = p.Size;
            var center = (s - 1) * 0.5f;
            var inner = 0.80f;
            var outer = 1f;
            const float halfSpread = 60f;

            for (var y = 0; y < s; y++)
            {
                for (var x = 0; x < s; x++)
                {
                    var nx = (x + 0.5f - center) / center;
                    var ny = (y + 0.5f - center) / center;
                    var distance = Mathf.Sqrt(nx * nx + ny * ny);
                    if (distance < inner || distance > outer)
                    {
                        continue;
                    }

                    var angle = Mathf.Atan2(ny, nx) * Mathf.Rad2Deg;
                    if (Mathf.Abs(angle) > halfSpread)
                    {
                        continue;
                    }

                    // 径向：弧带中间最亮，内外两侧渐隐（避免出现硬边）
                    var radial = Mathf.InverseLerp(inner, outer, distance);
                    var band = Mathf.Pow(Mathf.Sin(radial * Mathf.PI), 1.1f);

                    // 角度：中间最宽最亮，两端收细
                    var across = Mathf.Abs(angle) / halfSpread;
                    var taper = Mathf.Pow(Mathf.Cos(across * Mathf.PI * 0.5f), 2.4f);

                    var alpha = band * taper * 0.85f;
                    if (alpha <= 0.01f)
                    {
                        continue;
                    }

                    var color = Color.Lerp(Hex("FFF4DA"), Hex("FFFFFF"), alpha * alpha);
                    p.Set(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }
        }

        /// <summary>
        /// 回旋刃：木纹回旋镖。两臂夹角约 75°，正前方（+Y）是飞行/外抛方向，
        /// 臂上刻着蜥蜴纹样（参考实物回旋镖的澳洲原住民图腾感），边缘深棕描边。
        /// </summary>
        private static void Blade(Pix p)
        {
            var s = p.Size;
            var apex = new Vector2(s * 0.50f, s * 0.72f);
            var left = new Vector2(s * 0.20f, s * 0.26f);
            var right = new Vector2(s * 0.80f, s * 0.28f);
            var halfWidth = s * 0.125f;
            var tipWidth = s * 0.096f;
            var leftLength = Vector2.Distance(apex, left);
            var rightLength = Vector2.Distance(apex, right);

            var woodDark = Hex("A9713C");
            var woodLight = Hex("EDC489");
            var ink = Hex("4A3222");

            // 木纹：按「离中线的距离」做圆柱明暗，再叠一层沿臂方向的正弦纹理；两头略收窄
            for (var y = 0; y < s; y++)
            {
                for (var x = 0; x < s; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    var dLeft = SegmentDistance(point, apex, left, out var uLeft);
                    var dRight = SegmentDistance(point, apex, right, out var uRight);
                    var useLeft = dLeft <= dRight;
                    var distance = useLeft ? dLeft : dRight;
                    var along = useLeft ? uLeft : uRight;
                    var fraction = Mathf.Clamp01(along / (useLeft ? leftLength : rightLength));
                    var localHalf = Mathf.Lerp(halfWidth, tipWidth, fraction);
                    if (distance > localHalf)
                    {
                        continue;
                    }

                    var signed = useLeft
                        ? SegmentSignedDistance(point, apex, left)
                        : SegmentSignedDistance(point, apex, right);
                    var t = Mathf.Clamp01(distance / localHalf);
                    var cylinder = Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                    var grain = Mathf.Sin(signed * 0.55f + Mathf.Sin(along * 0.10f) * 2.6f) * 0.5f + 0.5f;
                    var color = Color.Lerp(woodDark, woodLight, 0.30f + 0.45f * grain);
                    var shade = (0.74f + 0.38f * cylinder) * (1f - 0.18f * t * t);
                    p.Set(x, y, new Color(color.r * shade, color.g * shade, color.b * shade, 1f));
                }
            }

            // 蜥蜴刻纹：两臂各两只，朝向交错，看起来像手工刻上去的
            PaintLizard(p, apex, left, 0.30f, 1f, ink);
            PaintLizard(p, apex, left, 0.62f, -1f, ink);
            PaintLizard(p, apex, right, 0.34f, -1f, ink);
            PaintLizard(p, apex, right, 0.66f, 1f, ink);

            // 顺着臂补两条深色木纹，远看也能看出是木头
            for (var i = 0; i < 2; i++)
            {
                var offset = (i == 0 ? -0.24f : 0.26f) * halfWidth;
                foreach (var arm in new[] { (apex, left), (apex, right) })
                {
                    var dir = (arm.Item2 - arm.Item1).normalized;
                    var perp = new Vector2(-dir.y, dir.x);
                    for (var step = 0; step < 70; step++)
                    {
                        var t = step / 69f;
                        var wobble = Mathf.Sin(t * 9f + i * 1.7f) * 1.1f;
                        var point = Vector2.Lerp(arm.Item1, arm.Item2, t) + perp * (offset + wobble);
                        p.Set(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.y), Hex("8A5A2E", 0.22f));
                    }
                }
            }

            // 高光：沿两条臂的上侧扫一道浅色，让木头的立体感更强
            for (var i = 0; i < 40; i++)
            {
                var t = i / 39f;
                foreach (var arm in new[] { (apex, left), (apex, right) })
                {
                    var dir = (arm.Item2 - arm.Item1).normalized;
                    var perp = new Vector2(-dir.y, dir.x);
                    var center = Vector2.Lerp(arm.Item1, arm.Item2, t) - perp * halfWidth * 0.46f;
                    p.Set(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y), Hex("FFE7C2", 0.20f));
                }
            }

            p.Outline(Hex("5A3A20", 0.95f), 2);
        }

        /// <summary>在回旋镖的一条臂上刻一只蜥蜴；<paramref name="t"/> 是沿着臂的位置，<paramref name="facing"/> 控制头朝哪边。</summary>
        private static void PaintLizard(Pix p, Vector2 from, Vector2 to, float t, float facing, Color color)
        {
            var center = Vector2.Lerp(from, to, t);
            var dir = (to - from).normalized * facing;
            var perp = new Vector2(-dir.y, dir.x);
            var scale = p.Size / 128f;

            var minX = Mathf.FloorToInt(center.x - 26f * scale);
            var maxX = Mathf.CeilToInt(center.x + 26f * scale);
            var minY = Mathf.FloorToInt(center.y - 10f * scale);
            var maxY = Mathf.CeilToInt(center.y + 10f * scale);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var rel = new Vector2(x + 0.5f, y + 0.5f) - center;
                    var u = Vector2.Dot(rel, dir) / scale;
                    var v = Vector2.Dot(rel, perp) / scale;
                    if (LizardContains(u, v))
                    {
                        p.Set(x, y, color);
                    }
                }
            }
        }

        private static float SegmentSignedDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var length = ab.magnitude;
            if (length <= 0.0001f)
            {
                return 0f;
            }

            var perpendicular = new Vector2(-ab.y, ab.x) / length;
            return Vector2.Dot(point - a, perpendicular);
        }

        private static bool LizardContains(float u, float v)
        {
            // 身体（带点弧度），头 + 吻部，尾巴由粗到细
            if (u >= -6.5f && u <= 5.5f)
            {
                var bodyHalf = 2.2f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(u / 6.5f, 2f) * 0.8f));
                if (Mathf.Abs(v) <= bodyHalf)
                {
                    return true;
                }
            }

            if ((new Vector2(u - 7.4f, v)).sqrMagnitude <= 4.6f)
            {
                return true;
            }

            if ((new Vector2((u - 10f) / 1.15f, v / 0.5f)).sqrMagnitude <= 3.2f)
            {
                return true;
            }

            if (u >= -16.5f && u <= -6f)
            {
                var tailHalf = Mathf.Lerp(0.4f, 1.6f, (u + 16.5f) / 10.5f);
                if (Mathf.Abs(v) <= tailHalf)
                {
                    return true;
                }
            }

            // 四条腿：从身体两侧斜向后下方伸出
            for (var i = 0; i < 4; i++)
            {
                var root = 4.2f - i * 2.9f;
                foreach (var side in new[] { 1f, -1f })
                {
                    var a = new Vector2(root, side * 1.2f);
                    var b = new Vector2(root - 2.3f, side * 3.8f);
                    if (SegmentDistance(new Vector2(u, v), a, b, out _) <= 0.52f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b, out float along)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            var length = Mathf.Sqrt(lengthSq);
            var projection = lengthSq <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            along = projection * length;
            return Vector2.Distance(point, a + ab * projection);
        }

        private static void Aura(Pix p)
        {
            var c = p.Size / 2f;
            p.Ring(c, c, p.Size * 0.46f, p.Size * 0.40f, Hex("FFD54F", 0.35f));
            p.Ring(c, c, p.Size * 0.40f, p.Size * 0.37f, Hex("FFF8E1", 0.30f));
        }

        private static void Shadow(Pix p)
        {
            var c = p.Size / 2f;
            p.Ellipse(c, c, p.Size * 0.44f, p.Size * 0.40f, Hex("000000", 0.55f));
        }

        /// <summary>随身炮台：炮管朝上（+Y），使用时按瞄准方向旋转。</summary>
        private static void Cannon(Pix p)
        {
            var s = p.Size;
            var c = s / 2f;
            var body = Hex("4A5568");
            var dark = Hex("2D3748");
            var light = Hex("8494AB", 0.9f);

            // 炮尾 + 炮身 + 炮口
            p.Circle(c, s * 0.24f, s * 0.19f, body);
            p.Rect(c, s * 0.52f, s * 0.30f, s * 0.56f, body);
            p.Circle(c, s * 0.79f, s * 0.16f, body);

            // 边缘高光与箍带
            p.Rect(c - s * 0.09f, s * 0.52f, s * 0.06f, s * 0.54f, light);
            p.Rect(c, s * 0.40f, s * 0.32f, s * 0.05f, dark);
            p.Rect(c, s * 0.66f, s * 0.30f, s * 0.05f, dark);

            // 炮口膛线（暗孔 + 一圈亮边）
            p.Circle(c, s * 0.80f, s * 0.095f, Hex("10151F"));
            p.Ring(c, s * 0.80f, s * 0.155f, s * 0.125f, Hex("B8C7DB", 0.85f));

            // 炮尾的蓝色能量核心（蓄能时光球就长在炮口，核心给个呼应）
            p.Circle(c, s * 0.20f, s * 0.075f, Hex("7FD4FF", 0.95f));
            p.Circle(c, s * 0.20f, s * 0.040f, Hex("FFFFFF", 0.85f));
        }

        /// <summary>炮台底座：一圈金属支架，压在炮管下面。</summary>
        private static void CannonBase(Pix p)
        {
            var s = p.Size;
            var c = s / 2f;
            p.Ring(c, c, s * 0.46f, s * 0.34f, Hex("3B4657", 0.95f));
            p.Ring(c, c, s * 0.34f, s * 0.30f, Hex("5B6B84", 0.75f));

            // 四根支架
            for (var i = 0; i < 4; i++)
            {
                var angle = (45f + i * 90f) * Mathf.Deg2Rad;
                p.Line(
                    c + Mathf.Cos(angle) * s * 0.26f,
                    c + Mathf.Sin(angle) * s * 0.26f,
                    c + Mathf.Cos(angle) * s * 0.45f,
                    c + Mathf.Sin(angle) * s * 0.45f,
                    3,
                    Hex("4A5568"));
            }
        }

        private static void Blob(Pix p, float radius, Color body, Color outline, bool eyes = false, bool sleepy = false)
        {
            var c = p.Size / 2f;
            p.Blob(c, c, p.Size * radius, body);
            p.Ellipse(c, c - p.Size * 0.08f, p.Size * radius * 0.80f, p.Size * radius * 0.55f, Lighten(body, 0.28f));
            if (eyes)
            {
                Eyes(p, c, c - p.Size * 0.02f, p.Size * 0.09f, Hex("1B1B1B"));
            }

            if (sleepy)
            {
                p.Rect(c - p.Size * 0.16f, c, p.Size * 0.14f, p.Size * 0.03f, Hex("5D4037"));
                p.Rect(c + p.Size * 0.16f, c, p.Size * 0.14f, p.Size * 0.03f, Hex("5D4037"));
            }
        }

        private static void Eyes(Pix p, float cx, float cy, float r, Color color)
        {
            p.Circle(cx - p.Size * 0.13f, cy, r, color);
            p.Circle(cx + p.Size * 0.13f, cy, r, color);
            p.Circle(cx - p.Size * 0.13f + r * 0.35f, cy + r * 0.35f, r * 0.32f, Hex("FFFFFF", 0.85f));
            p.Circle(cx + p.Size * 0.13f + r * 0.35f, cy + r * 0.35f, r * 0.32f, Hex("FFFFFF", 0.85f));
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static Color Hex(string hex, float alpha = 1f)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var color)
                ? new Color(color.r, color.g, color.b, alpha)
                : new Color(1f, 0f, 1f, alpha);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        // ---------------- 极简像素画布 ----------------

        private sealed class Pix
        {
            private readonly Color32[] _px;

            public Pix(int size)
            {
                Size = size;
                _px = new Color32[size * size];
            }

            public int Size { get; }

            public void Clear(Color color)
            {
                for (var i = 0; i < _px.Length; i++)
                {
                    _px[i] = color;
                }
            }

            public void Set(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size || color.a <= 0f)
                {
                    return;
                }

                var index = y * Size + x;
                var dst = _px[index];
                if (dst.a > 0)
                {
                    color = Color.Lerp(dst, color, color.a);
                }

                _px[index] = color;
            }

            public void Circle(float cx, float cy, float r, Color color)
            {
                Ellipse(cx, cy, r, r, color);
            }

            /// <summary>略微压扁的圆，用来画软乎乎的身体（史莱姆、麻薯之类）。</summary>
            public void Blob(float cx, float cy, float r, Color color)
            {
                Ellipse(cx, cy, r, r * 0.92f, color);
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color color)
            {
                for (var y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                {
                    for (var x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                    {
                        var dx = (x - cx) / Mathf.Max(0.001f, rx);
                        var dy = (y - cy) / Mathf.Max(0.001f, ry);
                        if (dx * dx + dy * dy <= 1f)
                        {
                            Set(x, y, color);
                        }
                    }
                }
            }

            public void Rect(float cx, float cy, float w, float h, Color color)
            {
                for (var y = Mathf.FloorToInt(cy - h * 0.5f); y <= Mathf.CeilToInt(cy + h * 0.5f); y++)
                {
                    for (var x = Mathf.FloorToInt(cx - w * 0.5f); x <= Mathf.CeilToInt(cx + w * 0.5f); x++)
                    {
                        Set(x, y, color);
                    }
                }
            }

            public void Triangle(float cx, float apexY, float halfWidth, float height, Color color, bool downward = false)
            {
                var baseY = downward ? apexY + height : apexY - height;
                for (var y = Mathf.FloorToInt(Mathf.Min(apexY, baseY)); y <= Mathf.CeilToInt(Mathf.Max(apexY, baseY)); y++)
                {
                    var t = Mathf.InverseLerp(apexY, baseY, y);
                    var half = halfWidth * t;
                    for (var x = Mathf.FloorToInt(cx - half); x <= Mathf.CeilToInt(cx + half); x++)
                    {
                        Set(x, y, color);
                    }
                }
            }

            public void Line(float x0, float y0, float x1, float y1, int thickness, Color color)
            {
                var steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)) * 2f);
                for (var i = 0; i <= steps; i++)
                {
                    var t = i / (float)Mathf.Max(1, steps);
                    var x = Mathf.Lerp(x0, x1, t);
                    var y = Mathf.Lerp(y0, y1, t);
                    Circle(x, y, thickness * 0.5f, color);
                }
            }

            public void Ring(float cx, float cy, float outer, float inner, Color color)
            {
                for (var y = Mathf.FloorToInt(cy - outer); y <= Mathf.CeilToInt(cy + outer); y++)
                {
                    for (var x = Mathf.FloorToInt(cx - outer); x <= Mathf.CeilToInt(cx + outer); x++)
                    {
                        var d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                        if (d <= outer && d >= inner)
                        {
                            Set(x, y, color);
                        }
                    }
                }
            }

            public void HLine(int y, Color color)
            {
                for (var x = 0; x < Size; x++)
                {
                    Set(x, y, color);
                }
            }

            public void VLine(int x, Color color)
            {
                for (var y = 0; y < Size; y++)
                {
                    Set(x, y, color);
                }
            }

            /// <summary>沿 alpha 边缘描一圈，让精灵在深色背景上有轮廓。</summary>
            public void Outline(Color color, int width)
            {
                var source = (Color32[])_px.Clone();
                for (var y = 0; y < Size; y++)
                {
                    for (var x = 0; x < Size; x++)
                    {
                        if (source[y * Size + x].a > 40)
                        {
                            continue;
                        }

                        var near = false;
                        for (var dy = -width; dy <= width && !near; dy++)
                        {
                            for (var dx = -width; dx <= width; dx++)
                            {
                                var nx = x + dx;
                                var ny = y + dy;
                                if (nx < 0 || ny < 0 || nx >= Size || ny >= Size)
                                {
                                    continue;
                                }

                                if (source[ny * Size + nx].a > 140)
                                {
                                    near = true;
                                    break;
                                }
                            }
                        }

                        if (near)
                        {
                            _px[y * Size + x] = color;
                        }
                    }
                }
            }

            public byte[] ToPng()
            {
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.SetPixels32(_px);
                tex.Apply();
                var bytes = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                return bytes;
            }
        }
    }
}
