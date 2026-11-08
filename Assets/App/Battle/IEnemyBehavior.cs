using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人行为的策略接口（P3-1b 之后只认 <see cref="EnemyState"/> + <see cref="IEnemyContext"/>）。
    ///
    /// 约定：
    /// • <b>策略本身无状态</b>——朝向、计时器、引信这些数据都在 <see cref="EnemyState"/> 上，
    ///   同屏上百只时给每个实例配策略对象与字典查找不划算，也更容易漏清理。
    /// • <b>只产出移动意图，不改位置</b>——位移由 <c>EnemySystem</c> 按移速与地图边界统一结算，
    ///   这样减速、卡墙、冻结只在一个地方生效。
    /// • <b>副作用走 context</b>——开火、爆炸、刷小怪都通过 <see cref="IEnemyContext"/>，
    ///   策略不持有任何 Unity 对象，headless 跑数值时可以直接替换 context。
    /// </summary>
    public interface IEnemyBehavior
    {
        /// <summary>
        /// 每帧推进，返回这一帧的移动意图（长度 0 表示不动，1 表示全速；大于 1 会被 <c>EnemySystem</c> 截断）。
        /// </summary>
        Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime);

        /// <summary>死亡时的额外结算（分裂怪在这里生小怪）。默认什么都不做。</summary>
        void OnDeath(EnemyState state, IEnemyContext context);
    }
}
