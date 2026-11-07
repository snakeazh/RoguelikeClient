using UnityEngine;

namespace App.Battle
{
#if UNITY_EDITOR || ENABLE_APP_LOG
    /// <summary>
    /// 只给排查用的小开关：`persistentDataPath` 里放一个 <c>autocam.flag</c>，启动后自动进战斗并接管操作
    /// 并自动点掉升级面板（和 BattleSmokeTest 用的是同一套自动战斗逻辑）。
    ///
    /// 只在编辑器 / 带 <c>ENABLE_APP_LOG</c> 的调试包里编译（`Build/Android/Build APK (Test Logs)` 会加这个 define）；
    /// 正式发布包里整个实现都被裁掉，`Enabled` 恒为 false、`StartAutoplay` 是空方法，调用方不需要写 `#if`。
    /// </summary>
    public static class DebugAutoplayHook
    {
        public const string FlagName = "autocam.flag";

        private static bool _checked;
        private static bool _enabled;

        public static string FlagPath => System.IO.Path.Combine(Application.persistentDataPath, FlagName);

        public static bool Enabled
        {
            get
            {
                if (!_checked)
                {
                    _checked = true;
                    _enabled = System.IO.File.Exists(FlagPath);
                }

                return _enabled;
            }
        }

        /// <summary>挂上自动选择组件并立刻开战。参数在发布包里不会被用到。</summary>
        public static void StartAutoplay(MonoBehaviour host, System.Action startBattle)
        {
            if (host != null)
            {
                host.gameObject.AddComponent<DebugAutoPilot>();
            }

            startBattle?.Invoke();
        }
    }
#else
    /// <summary>发布包里的空实现：排查开关不存在，调用方照旧写 <c>if (DebugAutoplayHook.Enabled)</c> 即可。</summary>
    public static class DebugAutoplayHook
    {
        public static bool Enabled => false;

        public static void StartAutoplay(MonoBehaviour host, System.Action startBattle)
        {
        }
    }
#endif
}
