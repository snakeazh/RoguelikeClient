namespace App.Battle
{
    /// <summary>
    /// 波次条目「到点做什么」的策略。
    ///
    /// 两类：<see cref="IsContinuous"/> 为 true 的按时间窗反复触发（Spawn），
    /// 为 false 的到点只触发一次（Elite / Ring / Tide / Boss / Rest）。
    /// 状态（倒计时、是否触发过）在 <see cref="WaveState"/> 上，<see cref="EnemySpawner"/> 只负责时间轴。
    /// </summary>
    public interface IWaveStep
    {
        bool IsContinuous { get; }

        /// <summary>持续型波次每帧推进。</summary>
        void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context);

        /// <summary>一次性波次到点触发。</summary>
        void Fire(WaveState wave, float elapsed, IBattleContext context);
    }
}
