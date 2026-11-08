namespace App.Battle
{
    /// <summary>
    /// 敌人行为的策略接口。
    ///
    /// 约定：<b>策略本身无状态</b>——朝向、计时器、引信这些数据都留在 <see cref="EnemyUnit"/> 上，
    /// 因为敌人同屏可能上百个，给每个实例配一份策略对象与字典查找不划算，也更容易漏清理。
    /// 策略只负责「这一帧按什么规则行动」。
    /// </summary>
    public interface IEnemyBehavior
    {
        /// <summary>每帧推进（受击反馈与朝向由 EnemyUnit 统一处理，不在这里做）。</summary>
        void Tick(EnemyUnit enemy, PlayerUnit player, float deltaTime);

        /// <summary>死亡时的额外结算（分裂怪在这里生小怪）。默认什么都不做。</summary>
        void OnDeath(EnemyUnit enemy);
    }
}
