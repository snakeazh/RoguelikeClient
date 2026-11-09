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
        // ---------- 伤害 ----------
        public void DamageEnemy(EnemyState enemy, DamageInfo damage)
        {
            if (enemy == null || enemy.IsDead)
            {
                return;
            }

            var died = enemy.TakeDamage(damage.Amount);

            PlayEnemyHitFx(enemy, damage);

            if (!died)
            {
                return;
            }

            KillEnemy(enemy);
        }

        /// <summary>敌人受击表现：飘字 + 白色火花（闪白/挤压由视图根据状态画）。</summary>
        private void PlayEnemyHitFx(EnemyState enemy, DamageInfo damage)
        {
            var crit = damage.Crit;
            var color = crit ? new Color(1f, 0.85f, 0.3f) : Color.white;
            var position = (Vector3)enemy.Position;
            DamagePopup.Show(
                position + Vector3.up * (enemy.Radius + 0.25f), damage.Amount, crit, color);

            TransientFx.Flash(
                position,
                enemy.Radius * (crit ? 1.7f : 1.3f),
                new Color(1f, 1f, 1f, crit ? 0.9f : 0.7f),
                crit ? 0.14f : 0.1f,
                22);
        }

        private void KillEnemy(EnemyState enemy)
        {
            Session.AddKill();

            var def = enemy.Def;
            var position = enemy.Position;

            // 死亡爆散：让「打死了」和「打了一下」在画面上区分开
            TransientFx.Flash(
                position,
                enemy.Radius * (enemy.IsBoss ? 4.2f : 2.6f),
                new Color(1f, 1f, 1f, 0.7f),
                enemy.IsBoss ? 0.35f : 0.2f,
                18);

            // 经验糖：经验值随分钟成长，保证 5 分钟内能打到 20 级（满级需要约 3500 点）。
            if (def.Exp > 0)
            {
                var exp = Mathf.Max(1, Mathf.RoundToInt(def.Exp * ExpScale()));
                var defId = exp >= 20 ? 3 : exp >= 5 ? 2 : 1;
                SpawnPickup(defId, exp, position);
            }

            // 金币 / 小蛋糕掉落
            var luck = Session.Luck;
            if (Session.Rng.NextDouble() < 0.08f + luck * 0.002f)
            {
                SpawnPickup(5, 10, position + Session.Rng.NextInsideUnitCircle() * 0.2f);
            }

            if (Session.Rng.NextDouble() < 0.04f)
            {
                SpawnPickup(4, 0, position);
            }

            if (enemy.IsElite)
            {
                SpawnPickup(5, 25, position);
                if (Session.Rng.NextDouble() < 0.35f)
                {
                    SpawnPickup(6, 0, position + Session.Rng.NextInsideUnitCircle() * 0.3f);
                }
            }

            // 行为自带的分裂（分裂怪）：策略自己决定生几只、什么属性
            EnemyBehaviors.Get(enemy).OnDeath(enemy, this);

            // 精英词缀的分裂：任何怪带上 Split 词缀都会分裂，与行为无关
            if (enemy.Affix == EliteAffix.Split)
            {
                for (var i = 0; i < 2; i++)
                {
                    var offset = Session.Rng.NextInsideUnitCircle() * 0.6f;
                    SpawnEnemy(8, position + offset, EliteAffix.None, Session.Elapsed);
                }
            }



            if (enemy.IsBoss)
            {
                SpawnPickup(6, 0, position);
                Session.AddGold(30);

                if (def.Id == BattleSession.BossEnemyId)
                {
                    Session.Finish(true);
                    AppLog.Info(LogChannel.Game, "[Battle] final boss defeated");
                }
                else
                {
                    Toast($"{def.Name} 被击败！");
                    AppLog.Info(LogChannel.Game, $"[Battle] boss defeated id={def.Id}");
                }
            }
        }

        public void DamagePlayer(float amount)
        {
            if (Player == null || Player.IsInvulnerable || Session.IsOver)
            {
                return;
            }

            // 受击后短暂无敌：避免被人群连续判定瞬间打死（一次伤害结算一堆敌人）。
            if (Session.Elapsed < _playerInvulnerableUntil)
            {
                return;
            }

            _playerInvulnerableUntil = Session.Elapsed + PlayerHitInvulnerability;
            Session.ApplyDamage(amount);

            // 受击反馈：角色闪红 + 屏幕边缘泛红 + 镜头震动 + 红色飘字
            Player.FlashHurt();

            var severity = Mathf.Clamp01(amount / Mathf.Max(1f, Session.MaxHp) * 2.5f);
            if (_hurtOverlay != null)
            {
                _hurtOverlay.Pulse(0.2f + severity * 0.4f);
            }

            if (_cameraFollow != null)
            {
                _cameraFollow.Shake(0.04f + severity * 0.12f, 0.18f);
            }

            DamagePopup.Show(
                Player.transform.position + Vector3.up * (PlayerUnit.Radius + 0.3f),
                amount,
                false,
                new Color(1f, 0.35f, 0.35f));
        }

        public void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer)
        {
            var center = (Vector2)position;
            var snapshot = _simulation.Enemies;
            for (var i = snapshot.Count - 1; i >= 0; i--)
            {
                var enemy = snapshot[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (Vector2.Distance(center, enemy.Position) <= radius + enemy.Radius)
                {
                    DamageEnemy(enemy, damage);
                }
            }

            if (hitPlayer && Player != null)
            {
                if (Vector2.Distance(center, Player.transform.position) <= radius + PlayerUnit.Radius)
                {
                    DamagePlayer(damage.Amount);
                }
            }
        }

        public void SpawnPlayerProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            EnemyState homingTarget = null,
            string spriteKey = null)
        {
            var state = _simulation.SpawnPlayerProjectile(
                position, direction, speed, damage, radius, pierce, splash, life, homingTarget);

            var unit = _projectileViews.Rent();
            unit.Init(state, spriteKey);
            unit.name = "Projectile";
            _projectiles.Add(unit);
        }

        public void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage)
        {
            var state = _simulation.SpawnEnemyProjectile(position, direction, speed, damage, 0.16f);

            var enemyShot = _projectileViews.Rent();
            enemyShot.Init(state, "Battle/Sprites/proj_enemy");
            enemyShot.name = "EnemyProjectile";
            _projectiles.Add(enemyShot);
        }

        /// <summary>经验成长系数：0 分钟 1 倍，每分钟 +1.8 倍（5 分钟约 10 倍）。</summary>
        private float ExpScale()
        {
            return 1f + Session.Elapsed / 60f * 1.8f;
        }

        private void SpawnPickup(int defId, int amount, Vector2 position)
        {
            var def = PickupDef.Get(defId);
            if (def == null)
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] pickup id {defId} not found");
                return;
            }

            var state = _simulation.SpawnPickup(def, amount, position);
            var pickup = _pickupViews.Rent();
            pickup.Init(state);
            _pickups.Add(pickup);
        }

        public void DropRestPickups()
        {
            if (Player == null)
            {
                return;
            }

            var position = (Vector2)Player.transform.position;
            SpawnPickup(4, 0, position + new Vector2(-1.2f, 0.5f));
            SpawnPickup(7, 0, position + new Vector2(1.2f, 0.5f));
        }

    }
}
