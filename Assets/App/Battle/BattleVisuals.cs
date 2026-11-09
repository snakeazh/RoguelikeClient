using System.Collections.Generic;
using System.Threading.Tasks;
using App.Resources;
using Framework.Assets;
using Framework.Log;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗的美术资源入口：精灵图与预制体都走 <see cref="IResourceService"/>（Assets/Res 下的 key），
    /// 缺资源时自动退回 <see cref="PlaceholderArt"/> 的纯色几何，保证没有美术也能跑。
    /// </summary>
    public static class BattleVisuals
    {
        // 精灵（相对 Assets/Res，无扩展名）。
        public const string Player = "Battle/Sprites/player";
        public const string Ground = "Battle/Sprites/ground";
        public const string TileFloor = "Battle/Sprites/tile_floor";
        public const string WallHorizontal = "Battle/Sprites/wall_h";
        public const string WallVertical = "Battle/Sprites/wall_v";
        public const string EnemyFallback = "Battle/Sprites/enemy_goblin";
        public const string ProjectileFallback = "Battle/Sprites/proj_orb";
        public const string PickupFallback = "Battle/Sprites/pickup_exp";
        public const string Blade = "Battle/Sprites/blade";
        public const string Aura = "Battle/Sprites/aura";
        public const string SlashArc = "Battle/Sprites/slash_arc";
        public const string Cannon = "Battle/Sprites/cannon";
        public const string CannonBase = "Battle/Sprites/cannon_base";

        // 预制体。
        public const string PlayerPrefab = "Battle/Prefabs/Player";
        public const string EnemyPrefab = "Battle/Prefabs/Enemy";
        public const string ProjectilePrefab = "Battle/Prefabs/Projectile";
        public const string PickupPrefab = "Battle/Prefabs/Pickup";
        public const string BladePrefab = "Battle/Prefabs/Blade";

        private static readonly Dictionary<string, Sprite> s_sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, GameObject> s_prefabs = new Dictionary<string, GameObject>();

        public static bool IsLoaded { get; private set; }

        /// <summary>战斗开始前调用：把用到的精灵与预制体一次性读进来（失败只 Warn，不阻断）。</summary>
        public static async Task LoadAsync(IResourceService resources)
        {
            s_sprites.Clear();
            s_prefabs.Clear();

            if (resources == null)
            {
                IsLoaded = false;
                return;
            }

            foreach (var key in SpriteKeys())
            {
                var sprite = await TryLoad<Sprite>(resources, key);
                if (sprite != null)
                {
                    s_sprites[key] = sprite;
                }
            }

            foreach (var key in PrefabKeys())
            {
                var prefab = await TryLoad<GameObject>(resources, key);
                if (prefab != null)
                {
                    s_prefabs[key] = prefab;
                }
            }

            IsLoaded = true;
            AppLog.Info(LogChannel.Assets,
                $"[BattleVisuals] sprites={s_sprites.Count} prefabs={s_prefabs.Count}");

            if (s_sprites.Count == 0)
            {
                AppLog.Warn(LogChannel.Assets,
                    "[BattleVisuals] no battle sprite found, falling back to placeholder shapes");
            }
        }

        public static Sprite GetSprite(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return s_sprites.TryGetValue(key, out var sprite) ? sprite : null;
        }

        /// <summary>视图池的建厂函数：敌人 / 弹体 / 拾取物各自一个预制体，缺预制体也能跑。</summary>
        public static EnemyUnit CreateEnemyView()
        {
            var go = Spawn(EnemyPrefab, "Enemy", null);
            return go.GetComponent<EnemyUnit>() ?? go.AddComponent<EnemyUnit>();
        }

        public static ProjectileUnit CreateProjectileView()
        {
            var go = Spawn(ProjectilePrefab, "Projectile", null);
            return go.GetComponent<ProjectileUnit>() ?? go.AddComponent<ProjectileUnit>();
        }

        public static PickupUnit CreatePickupView()
        {
            var go = Spawn(PickupPrefab, "Pickup", null);
            return go.GetComponent<PickupUnit>() ?? go.AddComponent<PickupUnit>();
        }

        /// <summary>按预制体建一个战斗对象；预制体缺失时退化成空 GameObject，逻辑照样跑。</summary>
        public static GameObject Spawn(string prefabKey, string name, Transform parent)
        {
            GameObject go = null;
            if (!string.IsNullOrEmpty(prefabKey) && s_prefabs.TryGetValue(prefabKey, out var prefab) && prefab != null)
            {
                go = Object.Instantiate(prefab);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        /// <summary>
        /// 取（没有就建）根节点下的 Body 渲染器并设置外观。
        /// 精灵存在时用精灵，不存在时用 fallback 形状 + 颜色。
        /// </summary>
        public static SpriteRenderer Body(
            Transform root,
            string spriteKey,
            Color color,
            float diameter,
            int order,
            Sprite fallbackShape = null)
        {
            var body = root.Find("Body");
            SpriteRenderer sr;
            if (body == null)
            {
                var go = new GameObject("Body");
                go.transform.SetParent(root, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            else
            {
                sr = body.GetComponent<SpriteRenderer>();
                if (sr == null)
                {
                    sr = body.gameObject.AddComponent<SpriteRenderer>();
                }
            }

            var sprite = GetSprite(spriteKey);
            if (sprite != null)
            {
                sr.sprite = sprite;
                sr.color = color;
                sr.transform.localScale = Vector3.one * diameter;
            }
            else
            {
                sr.sprite = fallbackShape != null ? fallbackShape : PlaceholderArt.Circle;
                sr.color = color;
                sr.transform.localScale = Vector3.one * diameter;
            }

            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// 建整张地图：地砖按 1×1 世界单位平铺（SpriteRenderer Tiled），四周加边界墙。
        /// 没有地砖精灵时退回旧的单张 ground 拉伸，没有 ground 时什么都不建。
        /// </summary>
        public static void CreateMap(Transform parent, Rect bounds, Rect walkable, int order = -100)
        {
            if (parent == null)
            {
                return;
            }

            var root = new GameObject("Map");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);

            var floor = GetSprite(TileFloor);
            if (floor != null)
            {
                var floorGo = new GameObject("Floor");
                floorGo.transform.SetParent(root.transform, false);
                floorGo.transform.localPosition = Vector3.zero;
                var sr = floorGo.AddComponent<SpriteRenderer>();
                sr.sprite = floor;
                sr.color = Color.white;
                sr.sortingOrder = order;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(bounds.width, bounds.height);
            }
            else
            {
                // 旧资源兜底：一张 ground 拉伸铺满。
                var legacy = GetSprite(Ground);
                if (legacy == null)
                {
                    return;
                }

                var floorGo = new GameObject("Floor");
                floorGo.transform.SetParent(root.transform, false);
                floorGo.transform.localPosition = Vector3.zero;
                var sr = floorGo.AddComponent<SpriteRenderer>();
                sr.sprite = legacy;
                sr.color = Color.white;
                sr.sortingOrder = order;
                var size = legacy.bounds.size;
                if (size.x > 0.001f && size.y > 0.001f)
                {
                    floorGo.transform.localScale = new Vector3(bounds.width / size.x, bounds.height / size.y, 1f);
                }
            }

            // 墙：贴在地图内沿，视觉厚度 = BattleMap.WallThickness。
            var wallH = GetSprite(WallHorizontal);
            var wallV = GetSprite(WallVertical);
            var half = BattleMap.WallThickness * 0.5f;

            if (wallH != null)
            {
                CreateTiled(root.transform, "WallTop", wallH,
                    new Vector3(0f, bounds.yMax - half, 0f), new Vector2(bounds.width, 1f), order + 2);
                CreateTiled(root.transform, "WallBottom", wallH,
                    new Vector3(0f, bounds.yMin + half, 0f), new Vector2(bounds.width, 1f), order + 2);
            }

            if (wallV != null)
            {
                CreateTiled(root.transform, "WallLeft", wallV,
                    new Vector3(bounds.xMin + half, 0f, 0f), new Vector2(1f, bounds.height), order + 2);
                CreateTiled(root.transform, "WallRight", wallV,
                    new Vector3(bounds.xMax - half, 0f, 0f), new Vector2(1f, bounds.height), order + 2);
            }

            _ = walkable; // 目前墙就是可走范围的边界，保留参数给以后做地形高低差。
        }

        private static void CreateTiled(Transform parent, string name, Sprite sprite, Vector3 localPosition, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
        }

        private static async Task<T> TryLoad<T>(IResourceService resources, string key) where T : Object
        {
            try
            {
                return await resources.LoadAsync<T>(key);
            }
            catch (System.Exception e)
            {
                AppLog.Warn(LogChannel.Assets, $"[BattleVisuals] load '{key}' failed: {e.Message}");
                return null;
            }
        }

        private static IEnumerable<string> SpriteKeys()
        {
            yield return Player;
            yield return Ground;
            yield return TileFloor;
            yield return WallHorizontal;
            yield return WallVertical;
            yield return Blade;
            yield return Aura;
            yield return SlashArc;
            yield return Cannon;
            yield return CannonBase;

            foreach (var enemy in EnemySprites.All)
            {
                yield return "Battle/Sprites/" + enemy;
            }

            foreach (var projectile in new[] { "proj_arrow", "proj_orb", "proj_magic", "proj_bomb", "proj_slash", "proj_enemy" })
            {
                yield return "Battle/Sprites/" + projectile;
            }

            foreach (var pickup in new[] { "pickup_exp", "pickup_exp_big", "pickup_cake", "pickup_gold", "pickup_chest", "pickup_magnet", "pickup_bomb", "pickup_hourglass" })
            {
                yield return "Battle/Sprites/" + pickup;
            }
        }

        private static IEnumerable<string> PrefabKeys()
        {
            yield return PlayerPrefab;
            yield return EnemyPrefab;
            yield return ProjectilePrefab;
            yield return PickupPrefab;
            yield return BladePrefab;
        }
    }

    /// <summary>敌人精灵名，Enemies.json 的 Sprite 字段和美术资源生成脚本共用这份列表。</summary>
    public static class EnemySprites
    {
        public static readonly string[] All =
        {
            "enemy_goblin", "enemy_bunny", "enemy_mochi", "enemy_gunner", "enemy_bomb",
            "enemy_split", "enemy_slug", "enemy_small", "boss_gargoyle", "boss_abyss", "boss_slime"
        };
    }
}
