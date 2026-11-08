using UnityEngine;

namespace App.Battle
{
    /// <summary>兔子那种会左右摆动的冲锋：总方向朝玩家，但横向叠一层正弦扰动。</summary>
    public sealed class RunnerBehavior : IEnemyBehavior
    {
        public void Tick(EnemyUnit enemy, PlayerUnit player, float deltaTime)
        {
            var dir = enemy.DirectionTo(player);

            // 摆动的相位存在单位上，避免给每只怪配一个策略实例
            enemy.WanderAngle += deltaTime * 6f;
            var weave = new Vector2(-dir.y, dir.x) * (Mathf.Sin(enemy.WanderAngle) * 0.55f);
            var moved = (dir + weave).normalized;

            enemy.MoveBy(moved, 1f);
        }

        public void OnDeath(EnemyUnit enemy)
        {
        }
    }
}
