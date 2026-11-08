using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>经验：`Args.Amount` 为空时用拾取物实例上的 Amount（掉落时按怪物经验算好）。</summary>
    public sealed class ExpEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupUnit pickup, PickupArgs args)
        {
            var amount = args.Amount > 0f ? Mathf.RoundToInt(args.Amount) : pickup.Amount;
            if (amount > 0)
            {
                context.Session.AddExp(amount);
            }
        }
    }
}
