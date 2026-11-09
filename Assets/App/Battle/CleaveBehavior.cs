using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 大剑：只朝玩家当前面向挥出扇形刀光（不自动索敌），命中扇形内的所有敌人。
    /// 想改成"自动瞄准最近敌人"就把 <c>aim</c> 换成 <c>host.FindNearestEnemy(...)</c> 的方向。
    /// </summary>
    public sealed class CleaveBehavior : IWeaponBehavior
    {
        /// <summary>扇形刀光张开的总角度（度）。改这个就能调"刀光有多宽"。</summary>
        public const float SpreadDegrees = 120f;

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var player = host.Player;
            if (player == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));

            var origin = player.Position;
            var aim = player.FacingDirection;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.up;
            }

            aim.Normalize();

            var angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var radius = host.AreaOf(weapon, weapon.Def.Area);
            var halfAngle = SpreadDegrees * 0.5f;
            var damage = host.RollDamage(host.DamageOf(weapon));

            var hits = new List<EnemyState>();
            foreach (var enemy in host.Context.Enemies.ToArray())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                var offset = (Vector2)enemy.Position - origin;
                if (offset.magnitude > radius + enemy.Radius)
                {
                    continue;
                }

                if (offset.sqrMagnitude > 0.0001f && Vector2.Angle(offset, aim) > halfAngle)
                {
                    continue;
                }

                hits.Add(enemy);
            }

            foreach (var enemy in hits)
            {
                host.Context.DamageEnemy(enemy, damage);
            }

            host.Views.Fan(origin, angle, radius * 1.05f, SpreadDegrees, new Color(1f, 0.97f, 0.86f, 1f), 0.26f);
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
        }
    }
}
