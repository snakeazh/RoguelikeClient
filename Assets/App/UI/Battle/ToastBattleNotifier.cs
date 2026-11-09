using App.Battle;

namespace App.UI.Battle
{
    /// <summary>战斗消息 → 屏幕 Toast。只有 UI 层知道「一句话该怎么显示」。</summary>
    public sealed class ToastBattleNotifier : IBattleNotifier
    {
        public void Notify(string message)
        {
            Toast.Show(message, 1.2f);
        }
    }
}
