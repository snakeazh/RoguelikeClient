using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人规则能用到的最小世界视图（P3-1b）：和 <see cref="IPickupContext"/> 同一规格，
    /// 让 <see cref="EnemySystem"/> / <see cref="ContactSystem"/> 不依赖 <c>BattleRunner</c> 具体类型。
    /// </summary>
    public interface IEnemyContext
    {
        /// <summary>玩家本体（4a 仍由行为直接读位置；4b 会换成只暴露坐标）。</summary>
        PlayerUnit Player { get; }

        float ElapsedSeconds { get; }

        void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage);

        void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer);

        void SpawnEnemy(int defId, Vector2 position, EliteAffix affix, float elapsed);

        void DamagePlayer(float amount);

        void ApplySlowToPlayer(float duration);

        void Toast(string message);
    }
}
