using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人规则能用到的最小世界视图（P3-1b）：和 <see cref="IPickupContext"/> 同一规格，
    /// 让 <see cref="EnemySystem"/> / <see cref="ContactSystem"/> / 各行为不依赖 <c>BattleRunner</c> 具体类型。
    /// 只暴露坐标与「可以做哪些副作用」，不暴露玩家本体，避免行为顺手改玩家状态。
    /// </summary>
    public interface IEnemyContext
    {
        /// <summary>逻辑专用随机源（与表现层的 `UnityEngine.Random` 分开，保证同种子可复现）。</summary>
        System.Random Rng { get; }

        /// <summary>玩家当前坐标（权威位置来自 <c>PlayerState.Position</c>）。</summary>
        Vector2 PlayerPosition { get; }

        /// <summary>玩家碰撞半径（接触判定用）。</summary>
        float PlayerRadius { get; }

        float ElapsedSeconds { get; }

        void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage);

        void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer);

        void SpawnEnemy(int defId, Vector2 position, EliteAffix affix, float elapsed);

        /// <summary>把一个敌人从本局移除（自爆后自毁用），不结算击杀奖励。</summary>
        void DespawnEnemy(EnemyState state);

        void DamagePlayer(float amount);

        void ApplySlowToPlayer(float duration);

        void Toast(string message);
    }
}
