namespace App.Battle
{
    /// <summary>
    /// 规则层往界面发消息的出口（Boss 进入二阶段、潮水开始、宝箱升级……）。
    ///
    /// 规则层只知道「要说一句话」，不知道这句话是 Toast、飘字还是日志；
    /// 具体怎么显示由组合根（<see cref="BattleLauncher"/>）注入的实现决定。
    /// </summary>
    public interface IBattleNotifier
    {
        void Notify(string message);
    }

    /// <summary>什么都不做的实现：纯逻辑测试与 headless 用。</summary>
    public sealed class NullBattleNotifier : IBattleNotifier
    {
        public static readonly NullBattleNotifier Instance = new NullBattleNotifier();

        private NullBattleNotifier()
        {
        }

        public void Notify(string message)
        {
        }
    }
}
