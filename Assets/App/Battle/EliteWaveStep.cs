namespace App.Battle
{
    /// <summary>精英怪：在视野外刷一只带词缀的，并提示玩家。</summary>
    public sealed class EliteWaveStep : IWaveStep
    {
        public bool IsContinuous => false;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context)
        {
        }

        public void Fire(WaveState wave, float elapsed, IBattleContext context)
        {
            var def = wave.Def;
            context.SpawnEnemy(def.EnemyId, context.RandomSpawnPosition(), def.Affix, elapsed);
            context.Toast("精英怪出现了！");
        }
    }
}
