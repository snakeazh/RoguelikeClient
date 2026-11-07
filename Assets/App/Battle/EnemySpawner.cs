using System.Collections.Generic;
using App.Config;
using Framework.Log;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 波次调度：读 Waves.json 的时间轴。
    /// - Spawn：在 [Time, Time+Duration) 区间内按 Interval 持续刷怪；
    /// - Elite / Ring / Tide / Boss / Rest：到点只触发一次。
    /// 刷怪数值按分钟成长：HP 每分钟 +40%，伤害每分钟 +20%。
    /// </summary>
    public sealed class EnemySpawner
    {
        private readonly List<WaveDef> _waves = new List<WaveDef>();
        private readonly Dictionary<int, float> _timers = new Dictionary<int, float>();
        private readonly HashSet<int> _fired = new HashSet<int>();

        public void Init()
        {
            _waves.Clear();
            _timers.Clear();
            _fired.Clear();

            foreach (var wave in WaveDef.All.Values)
            {
                _waves.Add(wave);
                _timers[wave.Id] = 0f;
            }

            _waves.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        public static float HpScale(float elapsed) => 1f + 0.4f * Mathf.Floor(elapsed / 60f);

        public static float DamageScale(float elapsed) => 1f + 0.2f * Mathf.Floor(elapsed / 60f);

        public void Tick(float deltaTime, float elapsed, BattleRunner runner)
        {
            for (var i = 0; i < _waves.Count; i++)
            {
                var wave = _waves[i];
                switch (wave.Kind)
                {
                    case WaveKind.Spawn:
                        TickSpawn(wave, deltaTime, elapsed, runner);
                        break;

                    default:
                        if (!_fired.Contains(wave.Id) && elapsed >= wave.Time)
                        {
                            _fired.Add(wave.Id);
                            FireOnce(wave, elapsed, runner);
                        }

                        break;
                }
            }
        }

        private void TickSpawn(WaveDef wave, float deltaTime, float elapsed, BattleRunner runner)
        {
            if (elapsed < wave.Time || elapsed >= wave.Time + wave.Duration)
            {
                return;
            }

            if (!_timers.TryGetValue(wave.Id, out var timer))
            {
                timer = 0f;
            }

            timer -= deltaTime;
            if (timer > 0f)
            {
                _timers[wave.Id] = timer;
                return;
            }

            _timers[wave.Id] = Mathf.Max(0.2f, wave.Interval);

            var count = Mathf.Max(1, wave.SpawnCount);
            for (var i = 0; i < count; i++)
            {
                var position = runner.RandomSpawnPosition();
                runner.SpawnEnemy(wave.EnemyId, position, wave.Affix, elapsed);
            }
        }

        private void FireOnce(WaveDef wave, float elapsed, BattleRunner runner)
        {
            AppLog.Info(LogChannel.Game, $"[Battle] wave {wave.Kind} at {wave.Time:0}s");

            switch (wave.Kind)
            {
                case WaveKind.Elite:
                {
                    var position = runner.RandomSpawnPosition();
                    runner.SpawnEnemy(wave.EnemyId, position, wave.Affix, elapsed);
                    runner.Toast("精英怪出现了！");
                    break;
                }

                case WaveKind.Ring:
                {
                    var player = runner.Player;
                    var center = player != null ? (Vector2)player.transform.position : Vector2.zero;
                    var count = Mathf.Max(1, wave.SpawnCount);
                    for (var i = 0; i < count; i++)
                    {
                        var angle = i * Mathf.PI * 2f / count;
                        var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * wave.Radius;
                        runner.SpawnEnemy(wave.EnemyId, position, EliteAffix.None, elapsed);
                    }

                    break;
                }

                case WaveKind.Tide:
                    runner.RunTide(wave);
                    break;

                case WaveKind.Boss:
                    runner.SpawnBoss(wave.EnemyId, elapsed);
                    runner.Toast("Boss 出现！");
                    break;

                case WaveKind.Rest:
                    runner.DropRestPickups();
                    runner.Toast("短暂喘息，捡点补给吧");
                    break;
            }
        }
    }
}
