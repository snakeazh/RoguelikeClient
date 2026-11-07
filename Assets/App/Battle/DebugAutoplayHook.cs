using System.IO;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 只给排查用的小开关：播放器目录下的 persistentDataPath 里放一个 autocam.flag，
    /// 启动后会自动进战斗并打开自动战斗机器人（和 BattleSmokeTest 用的是同一套逻辑）。
    /// 发布包里没有这个文件就完全不影响正常流程。
    /// </summary>
    public static class DebugAutoplayHook
    {
        public const string FlagName = "autocam.flag";

        private static bool _checked;
        private static bool _enabled;

        public static string FlagPath => Path.Combine(Application.persistentDataPath, FlagName);

        public static bool Enabled
        {
            get
            {
                if (!_checked)
                {
                    _checked = true;
                    _enabled = File.Exists(FlagPath);
                }

                return _enabled;
            }
        }
    }
}
