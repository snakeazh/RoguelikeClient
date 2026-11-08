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

            // 敌人 Tick 里会连锁击杀（爆炸、分裂）进而增删 _enemies，所以先拷一份再遍历。
            s_enemyBuffer.Clear();
            s_enemyBuffer.AddRange(_enemies);
            for (var i = 0; i < s_enemyBuffer.Count; i++)
            {
                var enemy = s_enemyBuffer[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (!frozen)
                {
                    enemy.Tick(deltaTime, Player);
                    TickContact(enemy);
                }
            }

            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] == null || _enemies[i].IsDead)
                {
                    _enemies.RemoveAt(i);
                }
            }
        }

        private void TickContact(EnemyUnit enemy)
        {
            if (Player == null || enemy.IsDead)
            {
                return;
            }

            var distance = Vector2.Distance(enemy.transform.position, Player.transform.position);
            if (distance > enemy.Radius + PlayerUnit.Radius)
            {
                return;
            }

            if (Session.Elapsed < enemy.NextContactTime)
            {
                return;
            }

            enemy.NextContactTime = Session.Elapsed + 0.5f;
            DamagePlayer(enemy.Damage);

            if (enemy.Def.SlowOnHit > 0f)
            {
                Player.ApplySlow(enemy.Def.SlowOnHit);
            }
        }

        // ---------- 弹体 ----------
        private void TickProjectiles(float deltaTime)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                if (projectile == null)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                if (projectile.Tick(deltaTime, this))
                {
                    _projectiles.RemoveAt(i);
                    Destroy(projectile.gameObject);
                }
            }
        }
    }
}
