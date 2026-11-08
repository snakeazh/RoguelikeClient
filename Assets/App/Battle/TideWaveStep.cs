namespace App.Battle
{
    /// <summary>
    /// 怪物潮：交给 <see cref="IBattleContext.RunTide"/> 起协程，
    /// 因为它在若干秒里持续刷怪，需要逐帧等待（不能阻塞波次时间轴）。
    /// </summary>
    public sealed class TideWaveStep : IWaveStep
    {
        public bool IsContinuous => false;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context)
        {
        }

        public void Fire(WaveState wave, float elapsed, IBattleContext context)
        {
            context.RunTide(wave.Def);
        }
    }
}
