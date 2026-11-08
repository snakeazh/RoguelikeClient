namespace App.Battle
{
    /// <summary>Boss：从玩家上方屏幕外走进来，并提示玩家。</summary>
    public sealed class BossWaveStep : IWaveStep
    {
        public bool IsContinuous => false;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context)
        {
        }

        public void Fire(WaveState wave, float elapsed, IBattleContext context)
        {
            context.SpawnBoss(wave.Def.EnemyId, elapsed);
            context.Toast("Boss 出现！");
        }
    }
}
