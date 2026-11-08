using System.Linq;
using UnityEngine;

namespace App.Battle
{
    /// <summary>权能法杖：半径内所有敌人持续掉血并减速，光环保留在玩家身上（视觉在 <see cref="WeaponRuntime"/> 里同步）。</summary>
    public sealed class AuraBehavior : IWeaponBehavior
    {
        private const float SlowFactor = 0.6f;

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));

            var radius = host.AreaOf(weapon, weapon.Def.Area);
            var damage = host.RollDamage(host.DamageOf(weapon));
            var origin = host.PlayerPosition;

            foreach (var enemy in host.Context.Enemies.ToArray())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                if (Vector2.Distance(origin, enemy.Position) <= radius + enemy.Radius)
                {
                    host.Context.DamageEnemy(enemy, damage);
                    enemy.ApplySlow(SlowFactor);
                }
            }
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
        }
    }
}
