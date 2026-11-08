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
        private void SyncPickupViews()
        {
            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup == null || pickup.State == null || pickup.State.Done)
                {
                    _pickups.RemoveAt(i);
                    _pickupViews.Return(pickup);

                    continue;
                }

                pickup.SyncFromState();
            }
        }

        // ---------- IPickupContext ----------
        public Vector3 PlayerPosition => Player != null ? Player.transform.position : Vector3.zero;

        /// <summary>逻辑随机源：与表现层共用的 `UnityEngine.Random` 分开，保证同种子可复现。</summary>
        public System.Random Rng => Session != null ? Session.Rng : null;

        public IReadOnlyList<PickupState> ActivePickups => _simulation != null ? _simulation.ActivePickups : EmptyPickups;

        private static readonly PickupState[] EmptyPickups = new PickupState[0];

        void IPickupContext.ExplodeAt(Vector3 position, float radius, DamageInfo damage)
        {
            ExplodeAt(position, radius, damage, false);
        }

        public void FreezeEnemies(float seconds)
        {
            _freezeUntil = Session.Elapsed + Mathf.Max(0f, seconds);
        }

        // ---------- IEnemyContext ----------
        // 显式实现：IPickupContext 的同名属性是 Vector3（给爆炸特效用），
        // 敌人侧只需要平面位置，接口成员故意收窄成 Vector2。
        Vector2 IEnemyContext.PlayerPosition => Player != null ? Player.State.Position : Vector2.zero;

        public float PlayerRadius => PlayerUnit.Radius;

        public void ApplySlowToPlayer(float duration)
        {
            Player?.ApplySlow(duration);
        }

        /// <summary>把一只敌人从场上拿掉（自爆自毁）：不结算击杀奖励、不掉落。</summary>
        /// <summary>自爆怪炸完自己：只销毁视图，死亡与掉落由 <see cref="EnemySystem"/> 下一帧统一清理。</summary>
        public void DespawnEnemy(EnemyState state)
        {
            if (state == null)
            {
                return;
            }

            if (_enemyViews.TryGetValue(state, out var view))
            {
                _enemyViews.Remove(state);
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            state.Hp = 0f;
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
