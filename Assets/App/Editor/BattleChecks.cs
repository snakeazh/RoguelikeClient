using System;
using System.Collections.Generic;
using System.IO;
using App.Battle;
using App.Config;
using UnityEditor;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// 纯逻辑自检入口：不跑场景、不开 Play 模式，只断言「配表、状态机、刷怪曲线、三选一、
    /// 随机流」这些不依赖 Unity 对象的规则。
    ///
    /// 为什么不写成 Unity Test Framework 的 EditMode 测试：全工程没有 asmdef，
    /// 测试程序集不能引用 Assembly-CSharp；先拆 asmdef 是一次高风险大改，等真要接 CI 时再做。
    /// 在那之前，这个入口可以用 `-executeMethod App.EditorTools.BattleChecks.Run` 挂进批处理。
    /// </summary>
    public static class BattleChecks
    {
        private const string ConfigRoot = "Assets/Res/Config";

        public static void Run()
        {
            try
            {
                var catalog = LoadCatalog();
                CheckCatalog(catalog);
                CheckPhaseRules();
                CheckSpawnScaling();
                CheckRandomStream();
                CheckChoiceOptions(catalog);

                Debug.Log("[BattleChecks] PASS: 6 groups checked (catalog / phase / scaling / random / choices).");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BattleChecks] FAIL: {e.Message}\n{e.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>直接从磁盘读 json 建目录：不依赖 AppServices 的启动顺序。</summary>
        private static BattleCatalog LoadCatalog()
        {
            EnemyDef.Load(ReadRows<EnemyDef>("Battle/Enemies"));
            WeaponDef.Load(ReadRows<WeaponDef>("Battle/Weapons"));
            PassiveDef.Load(ReadRows<PassiveDef>("Battle/Passives"));
            WaveDef.Load(ReadRows<WaveDef>("Battle/Waves"));
            LevelDef.Load(ReadRows<LevelDef>("Battle/Levels"));
            CharacterDef.Load(ReadRows<CharacterDef>("Battle/Characters"));
            PickupDef.Load(ReadRows<PickupDef>("Battle/Pickups"));
            return BattleCatalog.FromConfig();
        }

        private static T[] ReadRows<T>(string relativePath)
        {
            var path = Path.Combine(ConfigRoot, relativePath + ".json");
            var json = File.ReadAllText(path);
            return ConfigJson.FromArrayJson<T>(json);
        }

        private static void CheckCatalog(BattleCatalog catalog)
        {
            Expect(catalog.Weapons.Count > 0, "weapon table is empty");
            Expect(catalog.Passives.Count > 0, "passive table is empty");
            Expect(catalog.Enemies.Count > 0, "enemy table is empty");
            Expect(catalog.Weapon(catalog.Weapons.Count > 0 ? FirstWeaponId(catalog) : 0) != null, "weapon lookup failed");
            Expect(catalog.Weapon(int.MaxValue) == null, "missing weapon should be null");

            var previous = int.MinValue;
            var waveCount = 0;
            foreach (var wave in catalog.Waves)
            {
                Expect(wave.Id > previous, $"waves are not sorted by id ({wave.Id} after {previous})");
                previous = wave.Id;
                waveCount++;
            }

            Expect(waveCount > 0, "wave table is empty");
        }

        private static int FirstWeaponId(BattleCatalog catalog)
        {
            foreach (var weapon in catalog.Weapons)
            {
                return weapon.Id;
            }

            return 0;
        }

        private static void CheckPhaseRules()
        {
            // 用户定的规则：升级面板期间不可能分出胜负，LevelUp→Ending 必须被拒绝。
            Expect(!BattlePhaseRules.CanTransition(BattlePhase.LevelUp, BattlePhase.Ending), "LevelUp→Ending must be rejected");
            Expect(BattlePhaseRules.CanTransition(BattlePhase.Running, BattlePhase.LevelUp), "Running→LevelUp must be allowed");
            Expect(BattlePhaseRules.CanTransition(BattlePhase.Running, BattlePhase.Paused), "Running→Paused must be allowed");
            Expect(BattlePhaseRules.CanTransition(BattlePhase.Paused, BattlePhase.Running), "Paused→Running must be allowed");
            Expect(!BattlePhaseRules.CanTransition(BattlePhase.Paused, BattlePhase.LevelUp), "Paused→LevelUp must be rejected");
            Expect(BattlePhaseRules.CanTransition(BattlePhase.Ending, BattlePhase.Finished), "Ending→Finished must be allowed");
            Expect(!BattlePhaseRules.CanTransition(BattlePhase.Finished, BattlePhase.Running), "Finished is terminal");
            Expect(BattlePhase.Finished.IsTerminal(), "Finished must report IsTerminal");

            // 只有 Running 推进模拟。
            Expect(!BattlePhase.Running.BlocksSimulation(), "Running must not block simulation");
            Expect(BattlePhase.Paused.BlocksSimulation(), "Paused must block simulation");
        }

        private static void CheckSpawnScaling()
        {
            Expect(Mathf.Approximately(SpawnSystem.HpScale(0f), 1f), "hp scale at 0s");
            Expect(Mathf.Approximately(SpawnSystem.HpScale(59.9f), 1f), "hp scale before 60s");
            Expect(Mathf.Approximately(SpawnSystem.HpScale(60f), 1.4f), "hp scale at 60s");
            Expect(Mathf.Approximately(SpawnSystem.HpScale(180f), 2.2f), "hp scale at 180s");
            Expect(Mathf.Approximately(SpawnSystem.DamageScale(120f), 1.4f), "damage scale at 120s");
        }

        private static void CheckRandomStream()
        {
            var a = new System.Random(987654);
            var b = new System.Random(987654);
            for (var i = 0; i < 64; i++)
            {
                Expect(Mathf.Approximately(a.NextFloat(0f, 10f), b.NextFloat(0f, 10f)), "NextFloat diverged");
                var pa = a.NextInsideUnitCircle();
                var pb = b.NextInsideUnitCircle();
                Expect(Vector2.Distance(pa, pb) < 1e-5f, "NextInsideUnitCircle diverged");
                Expect(a.NextIndex(9) == b.NextIndex(9), "NextIndex diverged");
                Expect(pa.sqrMagnitude <= 1.0001f, "NextInsideUnitCircle escaped the unit circle");
            }
        }

        private static void CheckChoiceOptions(BattleCatalog catalog)
        {
            var session = new BattleSession(catalog, 987654);
            var options = ChoiceService.Build(session);
            Expect(options.Count == 3, $"expected 3 options, got {options.Count}");

            var seen = new HashSet<string>();
            foreach (var option in options)
            {
                var key = $"{option.Kind}:{option.DefId}";
                Expect(seen.Add(key), $"duplicate option {key}");
                Expect(catalog.Weapon(option.DefId) != null || catalog.Passive(option.DefId) != null,
                    $"option {key} has no row");
            }

            // 本命武器开局就在，不该再作为「新武器」出现。
            if (session.InnateWeapon != null)
            {
                foreach (var option in options)
                {
                    Expect(!(option.Kind == ChoiceKind.WeaponNew && option.DefId == session.InnateWeapon.Def.Id),
                        "innate weapon offered as a new weapon");
                }
            }
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
