using UnityEngine;

namespace App.Battle
{
    /// <summary>兔子那种会左右摆动的冲锋：总方向朝玩家，但横向叠一层正弦扰动。</summary>
    public sealed class RunnerBehavior : IEnemyBehavior
    {
        public Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime)
        {
            var dir = context.PlayerPosition - state.Position;
            if (dir.sqrMagnitude <= 0.0001f)
            {
                return Vector2.zero;
            }

            dir = dir.normalized;

            // 摆动的相位存在状态里，避免给每只怪配一个策略实例
            state.WanderAngle += deltaTime * 6f;
            var weave = new Vector2(-dir.y, dir.x) * (Mathf.Sin(state.WanderAngle) * 0.55f);
            return (dir + weave).normalized;
        }

        public void OnDeath(EnemyState state, IEnemyContext context)
        {
        }
    }
}
