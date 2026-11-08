using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 毒雾（P2-7 的 POC）：冷却到点在最近的敌人脚下砸出一片毒云，
    /// 云内每 <c>TickInterval</c> 秒结算一次伤害并减速，持续时间走 json 的 <c>Duration</c>。
    ///
    /// 这个类的存在本身是「扩展性」的证据：新增一种武器形态只需要
    /// 1) 本文件 2) <see cref="WeaponBehaviors"/> 里一行注册 3) 一条 Weapons.json ——
    /// <see cref="WeaponRuntime"/> 一行都没改。
    /// </summary>
    public sealed class PoisonBehavior : IWeaponBehavior
    {
        private const float TickInterval = 0.5f;

        private const float SlowFactor = 0.75f;

        private const float CloudAlpha = 0.42f;

        private const int MaxClouds = 4;

        private sealed class Cloud
        {
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Ring;
            public float Radius;
            public float Damage;
            public float Life;
            public float MaxLife;
            public float DamageTimer;
        }

        private readonly Dictionary<WeaponInstance, List<Cloud>> _clouds =
            new Dictionary<WeaponInstance, List<Cloud>>();

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            var clouds = GetClouds(weapon);
            TickClouds(host, clouds, deltaTime);

            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var target = host.FindNearestEnemy(weapon.Def.Range);
            if (target == null)
            {
                // 没目标就不憋冷却，敌人一进射程立刻砸。
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));
            SpawnCloud(host, weapon, (target as EnemyUnit).Position, clouds);
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            if (!_clouds.TryGetValue(weapon, out var clouds))
            {
                return;
            }

            for (var i = 0; i < clouds.Count; i++)
            {
                DestroyCloud(clouds[i]);
            }

            clouds.Clear();
            _clouds.Remove(weapon);
        }

        private List<Cloud> GetClouds(WeaponInstance weapon)
        {
            if (_clouds.TryGetValue(weapon, out var clouds))
            {
                return clouds;
            }

            clouds = new List<Cloud>();
            _clouds[weapon] = clouds;
            return clouds;
        }

        private void SpawnCloud(WeaponRuntime host, WeaponInstance weapon, Vector3 position, List<Cloud> clouds)
        {
            if (clouds.Count >= MaxClouds)
            {
                // 最老的一片提前收掉，避免满屏绿。
                DestroyCloud(clouds[0]);
                clouds.RemoveAt(0);
            }

            var radius = host.AreaOf(weapon, weapon.Def.Area);
            var root = new GameObject("PoisonCloud");
            root.transform.SetParent(host.Context.Root, false);
            root.transform.position = new Vector3(position.x, position.y, 0f);

            // 两层：实心圆做"雾团"，外圈用光环精灵做边界，半透明叠起来像一团绿雾。
            var body = CreateLayer(root.transform, "Body", PlaceholderArt.Circle, BattleSort.Fx + 1);
            var ring = CreateLayer(root.transform, "Ring", BattleVisuals.GetSprite(BattleVisuals.Aura) ?? PlaceholderArt.Ring, BattleSort.Fx + 2);

            var scale = radius * 2f;
            root.transform.localScale = new Vector3(scale, scale, 1f);

            clouds.Add(new Cloud
            {
                Root = root,
                Body = body,
                Ring = ring,
                Radius = radius,
                Damage = host.DamageOf(weapon),
                Life = weapon.Def.Duration,
                MaxLife = Mathf.Max(0.1f, weapon.Def.Duration),
                DamageTimer = 0f
            });

            // 落点先炸一下，避免「砸下去没反馈」。
            TransientFx.Flash(position, radius * 0.9f, new Color(0.5f, 1f, 0.45f, 0.55f), 0.18f, 16);
        }

        private void TickClouds(WeaponRuntime host, List<Cloud> clouds, float deltaTime)
        {
            for (var i = clouds.Count - 1; i >= 0; i--)
            {
                var cloud = clouds[i];
                cloud.Life -= deltaTime;
                if (cloud.Life <= 0f || cloud.Root == null)
                {
                    DestroyCloud(cloud);
                    clouds.RemoveAt(i);
                    continue;
                }

                // 越接近消散越淡，并轻微呼吸，读起来像"雾"。
                var remain = Mathf.Clamp01(cloud.Life / cloud.MaxLife);
                var breathe = 0.9f + 0.1f * Mathf.Sin(host.Context.ElapsedSeconds * 6f);
                var fade = Mathf.Min(1f, remain * 2f) * breathe;
                Tint(cloud.Body, new Color(0.34f, 0.92f, 0.30f, CloudAlpha * 0.62f * fade));
                Tint(cloud.Ring, new Color(0.45f, 1f, 0.40f, CloudAlpha * fade));

                cloud.Root.transform.Rotate(0f, 0f, deltaTime * 18f);

                cloud.DamageTimer -= deltaTime;
                if (cloud.DamageTimer > 0f)
                {
                    continue;
                }

                cloud.DamageTimer += TickInterval;
                DamageCloud(host, cloud);
            }
        }

        private static SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = new Color(0.4f, 0.95f, 0.35f, 0f);
            return renderer;
        }

        private static void Tint(SpriteRenderer renderer, Color color)
        {
            if (renderer != null)
            {
                renderer.color = color;
            }
        }

        private static void DamageCloud(WeaponRuntime host, Cloud cloud)
        {
            var center = cloud.Root.transform.position;
            var enemies = host.Context.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (Vector2.Distance(center, enemy.Position) > cloud.Radius + enemy.Radius)
                {
                    continue;
                }

                host.Context.DamageEnemy(enemy, host.RollDamage(cloud.Damage));
                enemy.ApplySlow(SlowFactor, host.Context.ElapsedSeconds);
            }
        }

        private static void DestroyCloud(Cloud cloud)
        {
            if (cloud?.Root != null)
            {
                Object.Destroy(cloud.Root);
            }
        }
    }
}
