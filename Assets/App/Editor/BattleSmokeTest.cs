using System;
using System.Text;
using System.Threading.Tasks;
using App.Battle;
using App.Bootstrap;
using App.UI.Battle;
using Framework.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// 战斗冒烟测试：Play 模式下自动开一局战斗，验证刷怪、索敌武器、经验升级（自动选择）、击杀，
    /// 然后自己退出编辑器。菜单 Tools/Client/Battle Smoke Test，命令行：
    /// Unity.exe -batchmode -projectPath &lt;工程&gt; -executeMethod App.EditorTools.BattleSmokeTest.Run
    ///
    /// 为什么用 SessionState：进 Play 会触发 domain reload，静态字段与事件订阅都会丢，
    /// 只有 SessionState 能跨 reload 保留，再由 [InitializeOnLoadMethod] 重新挂上回调。
    /// </summary>
    [InitializeOnLoad]
    public static class BattleSmokeTest
    {
        private const string ActiveKey = "BattleSmoke.Active";
        private const string PhaseKey = "BattleSmoke.Phase";
        private const string DeadlineKey = "BattleSmoke.Deadline";
        private const string KillsKey = "BattleSmoke.Kills";
        private const string LevelKey = "BattleSmoke.Level";
        private const string PendingExitKey = "BattleSmoke.PendingExit";
        private const string TargetKey = "BattleSmoke.Target";
        private const string SpeedKey = "BattleSmoke.Speed";
        private const string ImmortalKey = "BattleSmoke.Immortal";
        private const string ResultSeenKey = "BattleSmoke.ResultSeen";
        private const string SummaryKey = "BattleSmoke.Summary";

        private const string CharacterKey = "BattleSmoke.Character";

        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const float DefaultTargetSeconds = 40f;
        private const float DefaultSpeed = 4f;

        static BattleSmokeTest()
        {
            HookAfterReload();
        }

        [InitializeOnLoadMethod]
        private static void HookAfterReload()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Client/Battle Smoke Test (Play Mode)")]
        public static void Run()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError("[BattleSmokeTest] Missing scene: " + ScenePath);
                EditorApplication.Exit(1);
                return;
            }

            var target = ReadArg("-battleSeconds", DefaultTargetSeconds);
            var speed = ReadArg("-battleSpeed", DefaultSpeed);
            var immortal = ReadArg("-battleImmortal", 0f) > 0f;
            var characterId = ReadArg("-battleCharacter", 0f);

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ImmortalKey, immortal);
            SessionState.SetBool(ResultSeenKey, false);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetFloat(TargetKey, target);
            SessionState.SetFloat(SpeedKey, speed);
            SessionState.SetInt(CharacterKey, (int)characterId);
            // 加速越大，真实等待越短；留出启动与升级弹窗的余量。
            var timeout = 60f + target / Mathf.Max(1f, speed) * 3f;
            SessionState.SetFloat(DeadlineKey, (float)EditorApplication.timeSinceStartup + timeout);
            SessionState.SetInt(KillsKey, 0);
            SessionState.SetInt(LevelKey, 1);
            SessionState.SetInt(PendingExitKey, -1);

            EditorSceneManager.OpenScene(ScenePath);
            HookAfterReload();
            EditorApplication.isPlaying = true;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                Fail("Exception: " + condition + "\n" + stackTrace);
            }
            else if (type == LogType.Error)
            {
                Fail("Error: " + condition);
            }
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            var pending = SessionState.GetInt(PendingExitKey, -1);
            if (pending >= 0)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(ActiveKey, false);
                    EditorApplication.Exit(pending);
                }

                return;
            }

            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(DeadlineKey, 0f))
            {
                Fail("Timeout");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            var phase = SessionState.GetInt(PhaseKey, 0);
            if (phase == 0)
            {
                if (AppServices.IsReady)
                {
                    SessionState.SetInt(PhaseKey, 1);
                    _ = StartBattleAsync();
                }

                return;
            }

            TrackBattle();
        }

        private static async Task StartBattleAsync()
        {
            try
            {
                // 走真实入口：首页按钮 → HomeViewModel.StartBattleCommand → BattleLauncher。
                // -battleCharacter N 只是把存档里的选择改掉，入口本身不变。
                var characterId = SessionState.GetInt(CharacterKey, 0);
                if (characterId > 0)
                {
                    AppServices.Resolve<App.Battle.CharacterSelection>().Select(characterId);
                }

                var home = AppServices.Resolve<App.UI.Home.HomeViewModel>();
                var launcher = AppServices.Resolve<App.Battle.BattleLauncher>();
                home.StartBattleCommand.Execute();

                for (var i = 0; i < 300 && launcher.Runner == null; i++)
                {
                    await Task.Delay(16);
                }

                var runner = launcher.Runner;
                if (runner == null)
                {
                    Fail("BattleLauncher.Runner is null after StartBattleCommand");
                    return;
                }

                runner.AutoPlay = true;
                runner.SimulationSpeed = SessionState.GetFloat(SpeedKey, DefaultSpeed);
                Debug.Log("[BattleSmokeTest] battle started via Home.StartBattleCommand");
            }
            catch (Exception e)
            {
                Fail("StartBattleAsync: " + e);
            }
        }

        private static void TrackBattle()
        {
            if (!AppServices.IsReady)
            {
                return;
            }

            var runner = AppServices.Resolve<App.Battle.BattleLauncher>().Runner;
            if (runner == null || runner.Session == null)
            {
                // 点了结算界面的「返回」之后 launcher 会销毁 runner：用点之前存好的快照收尾。
                if (SessionState.GetBool(ResultSeenKey, false) && SessionState.GetInt(PhaseKey, 0) >= 1)
                {
                    Pass(SessionState.GetString(SummaryKey, "[BattleSmokeTest] battle ended"));
                }

                return;
            }

            var session = runner.Session;

            // -battleImmortal 1：只用于跑完 5 分钟的全流程（Boss、潮水、环形），验证代码路径不报错。
            if (SessionState.GetBool(ImmortalKey, false))
            {
                session.HealPercent(1f);
            }

            var kills = session.Kills;
            var level = session.Level;
            SessionState.SetInt(KillsKey, kills);
            SessionState.SetInt(LevelKey, level);

            // 升级弹窗会暂停战斗，冒烟测试里轮流选三张卡（避免永远只点第一张、Build 长不全）。
            var ui = AppServices.Resolve<IUIManager>();
            var levelUp = ui.FindOpen<LevelUpViewModel>();
            if (levelUp != null)
            {
                switch (level % 3)
                {
                    case 0:
                        levelUp.Choose2Command.Execute();
                        break;
                    case 1:
                        levelUp.Choose3Command.Execute();
                        break;
                    default:
                        levelUp.Choose1Command.Execute();
                        break;
                }
            }

            if (session.IsOver)
            {
                // 结算界面：先把结果快照存进 SessionState（点「返回」之后 launcher 会销毁 runner，
                // 下一帧就再也读不到 session 了），再点「返回」走回首页的流程。
                var result = ui.FindOpen<BattleResultViewModel>();
                if (result != null)
                {
                    if (!SessionState.GetBool(ResultSeenKey, false))
                    {
                        SessionState.SetBool(ResultSeenKey, true);
                        SessionState.SetString(SummaryKey, BuildSummary(session));
                        Debug.Log("[BattleSmokeTest] result screen opened");
                        result.ReturnCommand.Execute();
                    }

                    return;
                }

                if (!SessionState.GetBool(ResultSeenKey, false))
                {
                    return; // 等结算界面出现
                }

                Pass(SessionState.GetString(SummaryKey, BuildSummary(session)));
                return;
            }

            // 打够目标时长（默认 40 秒，加速 4 倍 ≈ 10 秒真实时间），且有过击杀、升过级。
            var target = SessionState.GetFloat(TargetKey, DefaultTargetSeconds);
            if (session.Elapsed >= target && kills >= 10 && level >= 2)
            {
                Pass($"[BattleSmokeTest] PASS: kills={kills}, level={level}, elapsed={session.Elapsed:0.0}s");
            }
        }

        /// <summary>
        /// 结算摘要。在点「返回」之前调用并塞进 SessionState —— 之后 launcher 会销毁 runner，
        /// 再想读 Session 就晚了（以前能用静态 Instance，P2 去掉单例后不行）。
        /// </summary>
        private static string BuildSummary(BattleSession session)
        {
            var summary = new StringBuilder();
            summary.Append("[BattleSmokeTest] battle ended: ");
            summary.Append(session.IsVictory ? "victory" : "defeat");
            summary.Append($", kills={session.Kills}, level={session.Level}, time={session.Elapsed:0.0}s");

            summary.Append(" | weapons:");
            foreach (var weapon in session.AllWeapons)
            {
                summary.Append(' ').Append(weapon.Def.Name).Append("(Lv").Append(weapon.Level).Append(')');
            }

            summary.Append(" | passives:");
            foreach (var passive in session.Passives)
            {
                summary.Append(' ').Append(passive.Def.Name).Append("(Lv").Append(passive.Level).Append(')');
            }

            return summary.ToString();
        }

        /// <summary>读取命令行参数，例如 -battleSeconds 300 -battleSpeed 20。</summary>
        private static float ReadArg(string name, float fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(args[i + 1], out var value) && value > 0f)
                {
                    return value;
                }
            }

            return fallback;
        }

        private static void Pass(string message)
        {
            Debug.Log(message);
            Finish(0);
        }

        private static void Fail(string reason)
        {
            Debug.LogError("[BattleSmokeTest] FAIL: " + reason);
            Finish(1);
        }

        private static void Finish(int code)
        {
            if (SessionState.GetInt(PendingExitKey, -1) >= 0)
            {
                return;
            }

            SessionState.SetInt(PendingExitKey, code);
            EditorApplication.isPlaying = false;
        }
    }
}
