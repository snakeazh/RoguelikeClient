using System.Collections.Generic;
using System.Linq;
using App.Config;
using Framework.Log;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 波次调度：只负责「时间轴」——按 `Waves.json` 的 `Time` 排序，
    /// 持续型条目每帧交给 <see cref="IWaveStep.Tick"/>，一次性条目到点交给 <see cref="IWaveStep.Fire"/>。
    /// 具体刷什么、刷几只、要不要提示，都在各 <see cref="IWaveStep"/> 里；
    /// 用哪套策略由构造时注入的 <see cref="IWaveStepRegistry"/> 决定。
    /// 刷怪数值按分钟成长：HP 每分钟 +40%，伤害每分钟 +20%。
    /// </summary>
    public sealed class EnemySpawner
    {
        private readonly List<WaveState> _waves;
        private readonly IWaveStepRegistry _steps;

        public EnemySpawner(IWaveStepRegistry steps, IEnumerable<WaveDef> defs)
        {
            _steps = steps;
            _waves = defs.OrderBy(def => def.Time).Select(def => new WaveState(def)).ToList();
        }


        public void Tick(float deltaTime, float elapsed, IBattleContext context)
        {
            for (var i = 0; i < _waves.Count; i++)
            {
                var wave = _waves[i];
                var step = _steps.Get(wave.Def.Kind);

                if (step.IsContinuous)
                {
                    step.Tick(wave, deltaTime, elapsed, context);
                    continue;
                }

                if (wave.Fired || elapsed < wave.Def.Time)
                {
                    continue;
                }

                wave.Fired = true;
                AppLog.Info(LogChannel.Game, $"[Battle] wave {wave.Def.Kind} at {wave.Def.Time:0}s");
                step.Fire(wave, elapsed, context);
            }
        }
    }
}
