using App.Config;

namespace App.Battle
{
    /// <summary>
    /// 一个波次条目的运行期状态。时间轴由 <see cref="EnemySpawner"/> 推进，
    /// 具体「到点做什么」交给 <see cref="IWaveStep"/>。
    /// </summary>
    public sealed class WaveState
    {
        public WaveState(WaveDef def)
        {
            Def = def;
        }

        public WaveDef Def { get; }

        /// <summary>持续刷怪的下一次触发倒计时。</summary>
        public float Timer;

        /// <summary>一次性波次是否已经触发过。</summary>
        public bool Fired;
    }
}
