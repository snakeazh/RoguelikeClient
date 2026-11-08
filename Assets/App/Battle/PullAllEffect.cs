using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>把场上其余拾取物全部吸向玩家（磁铁）。</summary>
    public sealed class PullAllEffect : IPickupEffect
    {
        public void Apply(IPickupContext context, PickupUnit pickup, PickupArgs args)
        {
            var pickups = context.ActivePickups;
            for (var i = 0; i < pickups.Count; i++)
            {
                var other = pickups[i];
                if (other != null && other != pickup)
                {
                    other.ForceAttract();
                }
            }
        }
    }
}
