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
        // ---------- 敌人视图 ----------
        /// <summary>
        /// 表现层只做映射：状态是权威，视图按需创建、按需销毁。
        /// 逻辑层什么时候刷怪、什么时候死亡，视图层都不需要参与。
        /// </summary>
        private void SyncEnemyViews(float deltaTime)
        {
            if (_simulation == null)
            {
                return;
            }

            var removed = _simulation.RemovedEnemies;
            for (var i = 0; i < removed.Count; i++)
            {
                var state = removed[i];
                if (state == null || !_enemyViews.TryGetValue(state, out var view))
                {
                    continue;
                }

                _enemyViews.Remove(state);
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            var enemies = _simulation.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var state = enemies[i];
                if (state == null)
                {
                    continue;
                }

                if (!_enemyViews.TryGetValue(state, out var view) || view == null)
                {
                    view = _enemyFactory.Create(state);
                    _enemyViews[state] = view;
                }

                view.SyncFromState(deltaTime, Player);
            }
        }

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
