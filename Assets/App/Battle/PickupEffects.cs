using System;
using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取效果注册表：按 `PickupEffectDef.Key` 找到原语。
    /// 加一个新原语 = 写一个 <see cref="IPickupEffect"/> + 在 `CreateDefault` 里 Add 一行；
    /// 加一个拾取物变体 = 只改 `Pickups.json`。
    /// </summary>
    public sealed class PickupEffects
    {
        private readonly Dictionary<string, IPickupEffect> _map =
            new Dictionary<string, IPickupEffect>(StringComparer.OrdinalIgnoreCase);

        private readonly IPickupEffect _fallback = new ToastEffect("[拾取] 未知效果");

        public PickupEffects Add(string key, IPickupEffect effect)
        {
            _map[key] = effect;
            return this;
        }

        public static PickupEffects CreateDefault()
        {
            return new PickupEffects()
                .Add("exp", new ExpEffect())
                .Add("gold", new GoldEffect())
                .Add("heal", new HealEffect())
                .Add("freeze", new FreezeEffect())
                .Add("explode", new ExplodeEffect())
                .Add("pullAll", new PullAllEffect())
                .Add("chest", new ChestEffect())
                .Add("toast", new ToastEffect());
        }

        public IPickupEffect Get(string key)
        {
            if (!string.IsNullOrEmpty(key) && _map.TryGetValue(key, out var effect))
            {
                return effect;
            }

            Debug.LogWarning("[Pickup] unknown effect key '" + key + "', ignored.");
            return _fallback;
        }
    }
}
