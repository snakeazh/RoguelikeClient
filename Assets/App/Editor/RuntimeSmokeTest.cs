using System;
using System.Threading.Tasks;
using App.Bootstrap;
using App.UI;
using Framework.UI;
using Framework.UI.Dialog;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace App.EditorTools
{
    /// <summary>
    /// 冒烟测试：打开 Main 场景 → 进 Play → 等启动流程打印 "[Boot] ready" →
    /// 触发一次 Toast 与确认对话框 → 关闭编辑器并返回退出码。
    /// 菜单 Tools/Client/Smoke Test (Play Mode)，命令行：
    /// Unity.exe -batchmode -projectPath &lt;工程&gt; -executeMethod App.EditorTools.RuntimeSmokeTest.Run -logFile &lt;log&gt;
    /// 退出码 0 = 启动与 UI 都走通，1 = 超时或出现异常/错误日志。
    ///
    /// 进入和退出 Play 模式都会触发 domain reload，静态字段会被清空，
    /// 所以状态放在 SessionState 里，每次 reload 由 [InitializeOnLoadMethod] 重新挂回调。
    /// </summary>
    public static class RuntimeSmokeTest
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const double TimeoutSeconds = 30d;

        private const string ActiveKey = "SmokeTest.Active";
        private const string DeadlineKey = "SmokeTest.Deadline";
        private const string ErrorsKey = "SmokeTest.Errors";
        private const string PendingExitKey = "SmokeTest.PendingExit";
        private const string ReadyKey = "SmokeTest.Ready";
        private const string PhaseKey = "SmokeTest.Phase";
        private const string ReadyMark = "[Boot] ready";

        private const int PhaseWaitingBoot = 0;
        private const int PhaseCheckingUi = 1;

        [MenuItem("Tools/Client/Smoke Test (Play Mode)")]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
            {
                Debug.LogError("[RuntimeSmokeTest] FAIL: main scene missing: " + MainScenePath);
                EditorApplication.Exit(1);
                return;
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(ErrorsKey, string.Empty);
            SessionState.SetString(DeadlineKey, (EditorApplication.timeSinceStartup + TimeoutSeconds).ToString("R"));
            SessionState.EraseBool(ReadyKey);
            SessionState.EraseInt(PendingExitKey);
            SessionState.SetInt(PhaseKey, PhaseWaitingBoot);

            EditorSceneManager.OpenScene(MainScenePath);

            Hook();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void HookAfterReload()
        {
            if (SessionState.GetBool(ActiveKey, false) || SessionState.GetInt(PendingExitKey, -1) >= 0)
            {
                Hook();
            }
        }

        private static void Hook()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains(ReadyMark))
            {
                SessionState.SetBool(ReadyKey, true);
            }

            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
            {
                return;
            }

            var errors = SessionState.GetString(ErrorsKey, string.Empty);
            if (string.IsNullOrEmpty(errors))
            {
                errors = type + ": " + condition;
            }

            SessionState.SetString(ErrorsKey, errors);
        }

        private static void Tick()
        {
            var pendingExit = SessionState.GetInt(PendingExitKey, -1);
            if (pendingExit >= 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                EditorApplication.update -= Tick;
                EditorApplication.Exit(pendingExit);
                return;
            }

            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            var errors = SessionState.GetString(ErrorsKey, string.Empty);
            if (!string.IsNullOrEmpty(errors))
            {
                Finish(1, "[RuntimeSmokeTest] FAIL: runtime error: " + errors);
                return;
            }

            var phase = SessionState.GetInt(PhaseKey, PhaseWaitingBoot);
            if (phase == PhaseWaitingBoot && SessionState.GetBool(ReadyKey, false))
            {
                SessionState.SetInt(PhaseKey, PhaseCheckingUi);
                CheckUiAsync();
                return;
            }

            if (!double.TryParse(SessionState.GetString(DeadlineKey, "0"), out var deadline)
                || EditorApplication.timeSinceStartup > deadline)
            {
                Finish(1, "[RuntimeSmokeTest] FAIL: timed out in phase " + phase);
            }
        }

        /// <summary>启动完成后再摸一遍 Toast 与确认对话框，确认预制体可用。</summary>
        private static async void CheckUiAsync()
        {
            try
            {
                Toast.Success("冒烟测试 Toast", 1.5f);

                var ui = AppServices.Resolve<IUIManager>();
                var dialog = ui.Dialogs.ConfirmAsync("冒烟测试", "能看到这个对话框说明 UI 框架正常。");
                await Task.Delay(400);

                // CloseWithResult 只对 ShowCustomAsync 生效；确认框要点按钮命令才会回结果。
                var open = ui.FindOpen<ConfirmDialogViewModel>();
                if (open == null)
                {
                    throw new InvalidOperationException("ConfirmDialogViewModel is not open after ConfirmAsync.");
                }

                open.OkCommand.Execute();
                var result = await dialog;

                Debug.Log("[RuntimeSmokeTest] UI ok (dialog result = " + result + ").");
                Finish(0, "[RuntimeSmokeTest] PASS");
            }
            catch (Exception exception)
            {
                Finish(1, "[RuntimeSmokeTest] FAIL: ui check: " + exception);
            }
        }

        private static void Finish(int exitCode, string message)
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(PendingExitKey, exitCode);
            Debug.Log(message);

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
