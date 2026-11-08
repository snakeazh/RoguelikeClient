using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// <see cref="WeaponKind"/> → 行为实例。
    ///
    /// 行为对象本身无状态（状态都存在各自的"按武器实例"的字典里），所以这里可以一个形态只 new 一次、全局共用。
    /// 新增武器形态：写一个 <see cref="IWeaponBehavior"/> 实现，然后在下面的表里加一行。
    /// </summary>
    public static class WeaponBehaviors
    {
        private static readonly Dictionary<WeaponKind, IWeaponBehavior> Map = new Dictionary<WeaponKind, IWeaponBehavior>
        {
            { WeaponKind.Projectile, new ShooterBehavior() },
            { WeaponKind.Homing, new ShooterBehavior() },
            { WeaponKind.Aura, new AuraBehavior() },
            { WeaponKind.Cleave, new CleaveBehavior() },
            { WeaponKind.Orbit, new OrbitBehavior() },
            { WeaponKind.Chain, new ChainBehavior() },
            { WeaponKind.Bomb, new BombBehavior() },
        };

        /// <summary>取某个形态的行为；没注册就退化成投掷物并告警，避免新枚举值把整局打崩。</summary>
        public static IWeaponBehavior Get(WeaponKind kind)
        {
            if (Map.TryGetValue(kind, out var behavior))
            {
                return behavior;
            }

            Debug.LogWarning("[Weapon] no behavior registered for " + kind + ", falling back to Projectile.");
            return Map[WeaponKind.Projectile];
        }
    }
}
