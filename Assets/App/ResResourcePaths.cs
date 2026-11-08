namespace App.Resources
{
    /// <summary>
    /// Assets/Res 下的资源 Key（无扩展名、相对 Assets/Res）。
    /// Bundle 名 = key 第一段小写，例如 "UI/Home" → bundle "ui" + asset "Home"。
    /// 新增界面时在这里补常量，不要散落字符串。
    /// </summary>
    public static class ResResourcePaths
    {
        // ---------- UI 框架 ----------
        /// <summary>分层 Canvas 根预制体，UIFramework.Create 之前必须先 preload 到缓存。</summary>
        public const string UIRoot = "UI/UIRoot";

        /// <summary>内置确认框。</summary>
        public const string ConfirmDialog = "UI/ConfirmDialog";

        /// <summary>Toast 面板（Toast 层常驻）。</summary>
        public const string ToastPanel = "UI/Popup/ToastPanel";

        // ---------- 业务界面（示例）----------
        /// <summary>Demo 首页。</summary>
        public const string Home = "UI/Home";

        // ---------- 战斗界面 ----------
        /// <summary>战斗 HUD（Hud 层）。</summary>
        public const string BattleHud = "UI/Battle/BattleHud";

        /// <summary>升级三选一（Popup 层）。</summary>
        public const string LevelUp = "UI/Battle/LevelUp";

        /// <summary>战斗结算（Popup 层）。</summary>
        public const string BattleResult = "UI/Battle/BattleResult";

        // ---------- 配置表（json，TextAsset）----------
        /// <summary>示例配置表 Assets/Res/Config/DemoConfig.json。</summary>
        public const string DemoConfig = "Config/DemoConfig";

        /// <summary>战斗：怪物表。</summary>
        public const string BattleEnemies = "Config/Battle/Enemies";

        /// <summary>战斗：武器表。</summary>
        public const string BattleWeapons = "Config/Battle/Weapons";

        /// <summary>战斗：被动表。</summary>
        public const string BattlePassives = "Config/Battle/Passives";

        /// <summary>战斗：波次表。</summary>
        public const string BattleWaves = "Config/Battle/Waves";

        /// <summary>战斗：升级经验表。</summary>
        public const string BattleLevels = "Config/Battle/Levels";

        /// <summary>战斗：可选角色表（初始血量、移速、本命武器）。</summary>
        public const string BattleCharacters = "Config/Battle/Characters";
        public const string BattlePickups = "Config/Battle/Pickups";

        // ---------- 音频 ----------
        public const string SfxUiClick = "Audio/Effect/ui_click_03";

        // ---------- 其它资源 ----------
        /// <summary>默认 TMP 字体（TextMesh Pro 内置资源在 Assets/TextMesh Pro 下，不走 Res）。</summary>
        public const string DefaultFont = "Fonts/GameFont SDF";
    }
}
