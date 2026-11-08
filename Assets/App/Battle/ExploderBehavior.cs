using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 自爆怪：先直线贴脸，进入 1.3 米后点引信（变红、脉冲放大由 <see cref="EnemyUnit.SyncFromState"/> 表现），
    /// 引信结束炸自己一圈伤害并自毁（不结算击杀奖励）。
    /// </summary>
    public sealed class ExploderBehavior : IEnemyBehavior
    {
        private const float TriggerDistance = 1.3f;

        public Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime)
        {
            var offset = context.PlayerPosition - state.Position;
            var distance = offset.magnitude;
            var dir = distance <= 0.01f ? Vector2.zero : offset / distance;

            if (!state.FuseStarted)
            {
                if (distance <= TriggerDistance)
                {
                    state.FuseStarted = true;
                    state.FuseLeft = Mathf.Max(0.2f, state.Def.FuseTime);
                }

                return dir;
            }

            state.FuseLeft -= deltaTime;
            if (state.FuseLeft > 0f)
            {
                return Vector2.zero;
            }

            context.ExplodeAt(
                state.Position, state.Def.ExplodeRadius, DamageInfo.Plain(state.Damage), true);
            context.DespawnEnemy(state);
            return Vector2.zero;
        }

        public void OnDeath(EnemyState state, IEnemyContext context)
        {
        }
    }
}
