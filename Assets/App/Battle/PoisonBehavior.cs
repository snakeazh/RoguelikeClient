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

        private const int MaxClouds = 4;

        private sealed class Cloud
        {
            public Vector2 Position;
            public IPoisonView View;
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
            SpawnCloud(host, weapon, target.Position, clouds);
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            if (!_clouds.TryGetValue(weapon, out var clouds))
            {
                return;
            }

            for (var i = 0; i < clouds.Count; i++)
            {
                clouds[i].View.Dispose();
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

        private void SpawnCloud(WeaponRuntime host, WeaponInstance weapon, Vector2 position, List<Cloud> clouds)
        {
            if (clouds.Count >= MaxClouds)
            {
                // 最老的一片提前收掉，避免满屏绿。
                clouds[0].View.Dispose();
                clouds.RemoveAt(0);
            }

            var radius = host.AreaOf(weapon, weapon.Def.Area);

            clouds.Add(new Cloud
            {
                Position = position,
                View = host.Views.CreatePoison(position, radius),
                Radius = radius,
                Damage = host.DamageOf(weapon),
                Life = weapon.Def.Duration,
                MaxLife = Mathf.Max(0.1f, weapon.Def.Duration),
                DamageTimer = 0f
            });

            // 落点先炸一下，避免「砸下去没反馈」。
            host.Views.Flash(position, radius * 0.9f, new Color(0.5f, 1f, 0.45f, 0.55f), 0.18f);
        }

        private void TickClouds(WeaponRuntime host, List<Cloud> clouds, float deltaTime)
        {
            for (var i = clouds.Count - 1; i >= 0; i--)
            {
                var cloud = clouds[i];
                cloud.Life -= deltaTime;
                if (cloud.Life <= 0f)
                {
                    cloud.View.Dispose();
                    clouds.RemoveAt(i);
                    continue;
                }

                // 越接近消散越淡，并轻微呼吸 —— 具体怎么画由视图决定。
                var remain = Mathf.Clamp01(cloud.Life / cloud.MaxLife);
                cloud.View.Tick(host.Context.ElapsedSeconds, remain, deltaTime);

                cloud.DamageTimer -= deltaTime;
                if (cloud.DamageTimer > 0f)
                {
                    continue;
                }

                cloud.DamageTimer += TickInterval;
                DamageCloud(host, cloud);
            }
        }

        private static void DamageCloud(WeaponRuntime host, Cloud cloud)
        {
            var center = cloud.Position;
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
                enemy.SlowUntil = Mathf.Max(enemy.SlowUntil, host.Context.ElapsedSeconds + SlowFactor);
            }
        }
    }
}
