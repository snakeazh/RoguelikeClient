using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 直线贴脸：朝玩家走，碰到就靠 <see cref="ContactSystem"/> 结算接触伤害。
    /// Chaser / Tank 共用；Tank 的差别只是血量、体型与速度数值。
    /// </summary>
    public class ChaserBehavior : IEnemyBehavior
    {
        public virtual Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime)
        {
            return TowardPlayer(state, context.PlayerPosition);
        }

        public virtual void OnDeath(EnemyState state, IEnemyContext context)
        {
        }

        /// <summary>朝玩家的单位方向；重合时返回零向量（本帧不动，避免抖动）。</summary>
        protected static Vector2 TowardPlayer(EnemyState state, Vector2 playerPosition)
        {
            var offset = playerPosition - state.Position;
            return offset.sqrMagnitude <= 0.0001f ? Vector2.zero : offset.normalized;
        }
    }
}
