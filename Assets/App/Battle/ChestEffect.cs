using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>宝箱：先尝试进化，再尝试升级/新武器，最后折成金币。</summary>
    public sealed class ChestEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupState pickup, PickupArgs args)
        {
            context.GrantChest();
        }
    }
}
