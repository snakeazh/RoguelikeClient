using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>金币：`Args.Amount` 为空时用拾取物实例上的 Amount（精英掉 25、普通掉 10）。</summary>
    public sealed class GoldEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupState pickup, PickupArgs args)
        {
            var amount = args.Amount > 0f ? Mathf.RoundToInt(args.Amount) : pickup.Amount;
            if (amount > 0)
            {
                context.Session.AddGold(amount);
            }
        }
    }
}
