using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 自爆怪：先直线贴脸，进入 1.3 米后点引信（变红、脉冲放大），引信结束炸自己一圈伤害。
    /// </summary>
    public sealed class ExploderBehavior : IEnemyBehavior
    {
        private const float TriggerDistance = 1.3f;

        public void Tick(EnemyUnit enemy, PlayerUnit player, float deltaTime)
        {
            if (!enemy.FuseStarted)
            {
                enemy.MoveToward(player, deltaTime);

                if (enemy.DistanceTo(player) <= TriggerDistance)
                {
                    enemy.StartFuse();
                }

                return;
            }

            enemy.FuseLeft -= deltaTime;
            var pulse = 1f + Mathf.Sin(Time.time * 20f) * 0.08f;
            enemy.transform.localScale = Vector3.one * pulse;

            if (enemy.FuseLeft > 0f)
            {
                return;
            }

            enemy.Context.ExplodeAt(
                enemy.transform.position, enemy.Def.ExplodeRadius, DamageInfo.Plain(enemy.Damage), true);
            enemy.KillSelf();
        }

        public void OnDeath(EnemyUnit enemy)
        {
        }
    }
}
