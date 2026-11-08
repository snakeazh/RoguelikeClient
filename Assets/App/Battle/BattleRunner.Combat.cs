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
        /// <summary>表现层：规则已由 <see cref="BattleSimulation"/> 算完，这里只移除死亡并同步 Transform。</summary>
        private void SyncEnemyViews(float deltaTime)
        {
            if (_simulation != null)
            {
                var removed = _simulation.RemovedEnemies;
                for (var i = 0; i < removed.Count; i++)
                {
                    var enemy = removed[i];
                    if (enemy != null)
                    {
                        Destroy(enemy.gameObject);
                    }
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
        /// <summary>表现层：规则层已经推进过弹体，这里只按状态销毁视图。</summary>
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
