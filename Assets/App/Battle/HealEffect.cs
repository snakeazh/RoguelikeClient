using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>按最大生命百分比回血。`Args.Percent` 缺省时回 30%。</summary>
    public sealed class HealEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupUnit pickup, PickupArgs args)
        {
            var percent = args.Percent > 0f ? args.Percent : 0.3f;
            context.Session.HealPercent(percent);
        }
    }
}
