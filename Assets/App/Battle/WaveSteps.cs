using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 波次策略注册表：`WaveDef.Kind` 是谁，就用谁去处理这条波次。
    /// 加一种新波次 = 写一个 <see cref="IWaveStep"/> + 在这里注册一行 + 枚举 + json。
    /// </summary>
    public static class WaveSteps
    {
        private static readonly Dictionary<WaveKind, IWaveStep> Map = new Dictionary<WaveKind, IWaveStep>
        {
            { WaveKind.Spawn, new SpawnWaveStep() },
            { WaveKind.Elite, new EliteWaveStep() },
            { WaveKind.Ring, new RingWaveStep() },
            { WaveKind.Tide, new TideWaveStep() },
            { WaveKind.Boss, new BossWaveStep() },
            { WaveKind.Rest, new RestWaveStep() }
        };

        private static readonly IWaveStep Fallback = new EliteWaveStep();

        public static IWaveStep Get(WaveKind kind)
        {
            if (Map.TryGetValue(kind, out var step))
            {
                return step;
            }

            Debug.LogWarning("[Wave] no step registered for " + kind + ", falling back to Elite.");
            return Fallback;
        }
    }
}
