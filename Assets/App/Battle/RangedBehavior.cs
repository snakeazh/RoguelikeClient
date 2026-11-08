using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 远程怪：保持在中距离（太远靠近、太近后退、距离合适就侧移），
    /// 攻击间隔到点朝玩家打一发弹体。炮手 / 深渊领主用它。
    /// 移动意图的「倍率」用向量长度表达：全速 1、侧移 0.4。
    /// </summary>
    public sealed class RangedBehavior : IEnemyBehavior
    {
        public Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime)
        {
            var offset = context.PlayerPosition - state.Position;
            var distance = offset.magnitude;
            var dir = distance <= 0.01f ? Vector2.zero : offset / distance;
            var range = state.Def.AttackRange;

            // 与拆分前一致：先结算开火计时，再决定这一帧怎么走
            TickAttack(state, context, dir, deltaTime);

            if (distance > range * 0.85f)
            {
                return dir;
            }

            if (distance < range * 0.5f)
            {
                return -dir;
            }

            return new Vector2(-dir.y, dir.x) * 0.4f;
        }

        public void OnDeath(EnemyState state, IEnemyContext context)
        {
        }

        private static void TickAttack(EnemyState state, IEnemyContext context, Vector2 aim, float deltaTime)
        {
            state.AttackTimer -= deltaTime;
            if (state.AttackTimer > 0f)
            {
                return;
            }

            state.AttackTimer = Mathf.Max(0.4f, state.Def.AttackInterval);
            context.SpawnEnemyProjectile(
                state.Position, aim, state.Def.ProjectileSpeed, state.Damage);
        }
    }
}
