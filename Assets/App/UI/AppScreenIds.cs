namespace App.UI
{
    /// <summary>
    /// 界面逻辑 ID 注册表。新增界面时在这里加常量，View 上用
    /// [AutoScreen(AppScreenIds.Xxx, UILayer.Yyy, ResResourcePaths.Xxx)] 关联。
    /// </summary>
    public static class AppScreenIds
    {
        /// <summary>Demo 首页（Page 层）。</summary>
        public const string Home = "App.Home";

        /// <summary>内置确认框（Popup 层，由 IDialogService 打开）。</summary>
        public const string ConfirmDialog = "App.ConfirmDialog";

        /// <summary>Toast 面板（Toast 层，由 ToastService 懒打开后常驻）。</summary>
        public const string ToastPanel = "App.ToastPanel";

        /// <summary>战斗 HUD（Hud 层，进战斗时打开，结算后关闭）。</summary>
        public const string BattleHud = "App.BattleHud";

        /// <summary>升级三选一（Popup 层）。</summary>
        public const string LevelUp = "App.LevelUp";

        /// <summary>战斗结算（Popup 层）。</summary>
        public const string BattleResult = "App.BattleResult";
    }
}
