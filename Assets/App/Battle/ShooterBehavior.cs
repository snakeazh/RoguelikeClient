using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 长弓 / 魔法炮 / 魔法飞弹：朝最近的敌人打弹体。
    /// <see cref="WeaponKind.Homing"/> 与 <see cref="WeaponKind.Projectile"/> 共用这一份逻辑，
    /// 区别只在"是否把目标传给弹体让它追踪"和弹体精灵。
    /// </summary>
    public sealed class ShooterBehavior : IWeaponBehavior
    {
        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var target = host.FindNearestEnemy(weapon.Def.Range);
            if (target == null)
            {
                // 没有目标时停在"可开火"状态，出现目标立刻打
                weapon.ClearCooldown();
                return;
            }

            // 有炮台的武器（魔法炮 / 湮灭炮）：等炮管真的转到位了再从炮口打出去，
            // 这样视觉上永远是「慢慢转过去 → 蓄满 → 开炮」，不会出现瞬间甩头。
            var mount = weapon.Def.Mount == 1 ? host.Cannon : null;
            if (mount != null && mount.Exists)
            {
                var toTarget = (Vector2)(target as EnemyUnit).Position;
                // 转到位再开火；最多等 0.6 秒，避免目标一直绕圈时这门炮永远不开火
                if (!mount.IsAlignedWith(toTarget) && weapon.CooldownTimer > -CannonMount.FireGraceSeconds)
                {
                    return;
                }
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));

            var playerPosition = host.PlayerPosition;
            var origin = mount != null && mount.Exists ? mount.MuzzlePosition : playerPosition;
            var aim = ((Vector2)(target as EnemyUnit).Position - origin).normalized;

            var count = host.CountOf(weapon);
            var spread = count > 1 ? 22f : 0f;

            for (var i = 0; i < count; i++)
            {
                var offset = count > 1 ? Mathf.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                var dir = WeaponRuntime.Rotate(aim, offset);
                var damage = host.RollDamage(host.DamageOf(weapon));
                host.Context.SpawnPlayerProjectile(
                    origin,
                    dir,
                    weapon.Def.Speed,
                    damage,
                    weapon.Def.ProjectileRadius,
                    weapon.Def.Pierce,
                    weapon.Def.SplashRadius,
                    weapon.Def.Duration,
                    weapon.Def.Kind == WeaponKind.Homing ? target : null,
                    WeaponRuntime.ProjectileSprite(weapon.Def));
            }

            if (mount != null && mount.Exists)
            {
                mount.Kick();
            }
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            // 弹体自己会到期销毁，这里没有需要清理的表现物
        }
    }
}
