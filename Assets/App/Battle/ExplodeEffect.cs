using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>以玩家为中心范围伤害（只伤敌人）。`Args.Radius` 缺省 4，`Args.Damage` 缺省 60。</summary>
    public sealed class ExplodeEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupState pickup, PickupArgs args)
        {
            var radius = args.Radius > 0f ? args.Radius : 4f;
            var damage = args.Damage > 0f ? args.Damage : 60f;
            context.ExplodeAt(context.PlayerPosition, radius, DamageInfo.Plain(damage));
        }
    }
}
