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
        // ---------- 敌人 ----------
        private void TickEnemies(float deltaTime)
        {
            var frozen = Session.Elapsed < _freezeUntil;

            // 规则层：行为调度 + 位移 + 击退 + Boss 阶段，然后单独跑接触伤害。
            _enemySystem.Tick(deltaTime, this, _enemies, frozen);
            _contactSystem.Tick(deltaTime, this, _enemies);

            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                var enemy = _enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    _enemies.RemoveAt(i);
                }
            }

            // 表现层：只把状态同步到 Transform，不参与任何判定。
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy != null)
                {
                    enemy.SyncFromState(deltaTime, Player);
                }
            }
        }

        // ---------- 弹体 ----------
        /// <summary>先让规则层推进所有弹体，再让表现层跟着状态走（表现不参与命中判定）。</summary>
        private void TickProjectiles(float deltaTime)
        {
            _projectileSystem.Tick(deltaTime, this);
            SyncProjectileViews();
        }

        private void SyncProjectileViews()
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                if (projectile == null || projectile.State == null || projectile.State.Dead)
                {
                    _projectiles.RemoveAt(i);
                    if (projectile != null)
                    {
                        Destroy(projectile.gameObject);
                    }

                    continue;
                }

                projectile.SyncFromState();
            }
        }
    }
}
