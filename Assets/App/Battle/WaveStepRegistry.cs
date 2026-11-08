using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 波次策略注册表的接口。做成接口是为了让一局战斗可以换一套策略
    /// （例如以后「无尽模式」用不同的刷怪节奏），而不是被静态字典钉死。
    /// </summary>
    public interface IWaveStepRegistry
    {
        IWaveStep Get(WaveKind kind);
    }

    /// <summary>
    /// 默认注册表：`WaveDef.Kind` 是谁，就用谁去处理这条波次。
    /// 加一种新波次 = 写一个 <see cref="IWaveStep"/> + 在这里注册一行 + 枚举 + json。
    /// </summary>
    public sealed class WaveStepRegistry : IWaveStepRegistry
    {
        private readonly Dictionary<WaveKind, IWaveStep> _map;
        private readonly IWaveStep _fallback;

        public WaveStepRegistry(IEnumerable<KeyValuePair<WaveKind, IWaveStep>> steps, IWaveStep fallback)
        {
            _map = new Dictionary<WaveKind, IWaveStep>();
            foreach (var pair in steps)
            {
                _map[pair.Key] = pair.Value;
            }

            _fallback = fallback;
        }

        public static WaveStepRegistry CreateDefault()
        {
            return new WaveStepRegistry(
                new Dictionary<WaveKind, IWaveStep>
                {
                    { WaveKind.Spawn, new SpawnWaveStep() },
                    { WaveKind.Elite, new EliteWaveStep() },
                    { WaveKind.Ring, new RingWaveStep() },
                    { WaveKind.Tide, new TideWaveStep() },
                    { WaveKind.Boss, new BossWaveStep() },
                    { WaveKind.Rest, new RestWaveStep() }
                },
                new EliteWaveStep());
        }

        public IWaveStep Get(WaveKind kind)
        {
            if (_map.TryGetValue(kind, out var step))
            {
                return step;
            }

            Debug.LogWarning("[Wave] no step registered for " + kind + ", falling back to Elite.");
            return _fallback;
        }
    }
}
