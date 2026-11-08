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
        /// <summary>规则层推进吸附/拾取/效果，表现层再跟着状态走。</summary>
        private void TickPickups(float deltaTime)
        {
            var magnetRadius = 1.8f * Session.MagnetMultiplier;
            _pickupSystem.Tick(deltaTime, PlayerPosition, Player != null, magnetRadius, this);
            SyncPickupViews();
        }

        private void SyncPickupViews()
        {
            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup == null || pickup.State == null || pickup.State.Done)
                {
                    _pickups.RemoveAt(i);
                    if (pickup != null)
                    {
                        Destroy(pickup.gameObject);
                    }

                    continue;
                }

                pickup.SyncFromState();
            }
        }

        // ---------- IPickupContext ----------
        public Vector3 PlayerPosition => Player != null ? Player.transform.position : Vector3.zero;

        public IReadOnlyList<PickupState> ActivePickups => _pickupSystem.States;

        void IPickupContext.ExplodeAt(Vector3 position, float radius, DamageInfo damage)
        {
            ExplodeAt(position, radius, damage, false);
        }

        public void FreezeEnemies(float seconds)
        {
            _freezeUntil = Session.Elapsed + Mathf.Max(0f, seconds);
        }

        // ---------- IEnemyContext ----------
        public void ApplySlowToPlayer(float duration)
        {
            Player?.ApplySlow(duration);
        }
        public void GrantChest()
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

            var candidates = Session.AllWeapons.Where(w => !w.IsMaxLevel).ToList();
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
                Session.TryUpgradeWeapon(weapon);
                Toast($"宝箱：{weapon.Def.Name} 升到 Lv{weapon.Level}");
                return;
            }

            Session.AddGold(50);
            Toast("宝箱：金币 +50");
        }

    }
}
