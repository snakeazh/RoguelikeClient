namespace App.Battle
{
    /// <summary>喘息：把场上残留的经验/金币补给成堆丢给玩家，并提示。</summary>
    public sealed class RestWaveStep : IWaveStep
    {
        public bool IsContinuous => false;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context)
        {
        }

        public void Fire(WaveState wave, float elapsed, IBattleContext context)
        {
            context.DropRestPickups();
            context.Toast("短暂喘息，捡点补给吧");
        }
    }
}
