using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Bootstrap;
using App.Config;
using App.UI;
using App.UI.Battle;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    public sealed partial class BattleRunner : MonoBehaviour
    {
        // ---------- 拾取 ----------
        private void TickPickups(float deltaTime)
        {
            var magnetRadius = 1.8f * Session.MagnetMultiplier;
            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup == null)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                if (!pickup.Tick(deltaTime, Player, magnetRadius))
                {
                    continue;
                }

                ApplyPickup(pickup);
                _pickups.RemoveAt(i);
                Destroy(pickup.gameObject);
            }
        }

        private void ApplyPickup(PickupUnit pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.ExpSmall:
                case PickupKind.ExpMedium:
                case PickupKind.ExpLarge:
                    Session.AddExp(pickup.Value);
                    break;

                case PickupKind.Cake:
                    Session.HealPercent(0.3f);
                    Toast("恢复 30% 生命");
                    break;

                case PickupKind.Gold:
                    Session.AddGold(pickup.Value);
                    break;

                case PickupKind.Chest:
                    GrantChest();
                    break;

                case PickupKind.Magnet:
                    foreach (var other in _pickups)
                    {
                        if (other != null && other != pickup)
                        {
                            other.ForceAttract();
                        }
                    }

                    Toast("磁力吸取！");
                    break;

                case PickupKind.Bomb:
                    ExplodeAt(Player.transform.position, 4f, DamageInfo.Plain(60f), false);
                    Toast("糖果炸弹！");
                    break;

                case PickupKind.Hourglass:
                    _freezeUntil = Time.time + 3f;
                    Toast("时间定格 3 秒");
                    break;
            }
        }

        private void GrantChest()
        {
            // 1) 有满足条件的武器先进化（满级武器 + 配套被动满级）。
            var evolvable = Session.FindEvolvable();
            if (evolvable != null)
            {
                var evolved = Session.Evolve(evolvable);
                if (evolved != null)
                {
                    Toast($"宝箱：{evolved.Name} 进化完成！");
                    TransientFx.Flash(Player.transform.position, 3.2f, new Color(1f, 0.85f, 0.35f), 0.5f);
                    return;
                }
            }

            var candidates = Session.Weapons.Where(w => !w.IsMaxLevel).ToList();
            if (candidates.Count == 0 && Session.CanAddWeapon)
            {
                var missing = WeaponDef.All.Values.Where(d => d.Id < 100 && !Session.HasWeapon(d.Id)).ToList();
                if (missing.Count > 0)
                {
                    Session.AddWeapon(missing[Session.Rng.Next(missing.Count)]);
                    Toast("宝箱：获得新武器！");
                    return;
                }
            }

            if (candidates.Count > 0)
            {
                var weapon = candidates[Session.Rng.Next(candidates.Count)];
                weapon.TryUpgrade();
                Toast($"宝箱：{weapon.Def.Name} 升到 Lv{weapon.Level}");
                return;
            }

            Session.AddGold(50);
            Toast("宝箱：金币 +50");
        }

    }
}
