using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>冻结所有敌人。`Args.Seconds` 缺省 3 秒。</summary>
    public sealed class FreezeEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupUnit pickup, PickupArgs args)
        {
            context.FreezeEnemies(args.Seconds > 0f ? args.Seconds : 3f);
        }
    }
}
