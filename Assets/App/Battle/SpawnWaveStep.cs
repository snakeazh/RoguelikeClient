using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 持续刷怪：在 `[Time, Time + Duration)` 区间里每 `Interval` 秒刷 `SpawnCount` 只。
    /// 时间窗之外只是静默等待，不会重置已用的倒计时。
    /// </summary>
    public sealed class SpawnWaveStep : IWaveStep
    {
        public bool IsContinuous => true;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IBattleContext context)
        {
            var def = wave.Def;
            if (elapsed < def.Time || elapsed >= def.Time + def.Duration)
            {
                return;
            }

            wave.Timer -= deltaTime;
            if (wave.Timer > 0f)
            {
                return;
            }

            wave.Timer = Mathf.Max(0.2f, def.Interval);

            var count = Mathf.Max(1, def.SpawnCount);
            for (var i = 0; i < count; i++)
            {
                context.SpawnEnemy(def.EnemyId, context.RandomSpawnPosition(), def.Affix, elapsed);
            }
        }

        public void Fire(WaveState wave, float elapsed, IBattleContext context)
        {
        }
    }
}
