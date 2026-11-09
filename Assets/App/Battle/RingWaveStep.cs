using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>环形包围：以玩家为中心、半径 `Radius` 上均匀刷 `SpawnCount` 只。</summary>
    public sealed class RingWaveStep : IWaveStep
    {
        public bool IsContinuous => false;

        public void Tick(WaveState wave, float deltaTime, float elapsed, IWaveContext context)
        {
        }

        public void Fire(WaveState wave, float elapsed, IWaveContext context)
        {
            var def = wave.Def;
            var center = context.PlayerPosition;
            var count = Mathf.Max(1, def.SpawnCount);

            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * def.Radius;
                context.SpawnEnemy(def.EnemyId, position, EliteAffix.None, elapsed);
            }
        }
    }
}
