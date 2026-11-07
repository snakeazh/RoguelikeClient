namespace App.Battle
{
    /// <summary>
    /// 一局战斗所处的阶段。以前是 <c>_running</c> / <c>_ending</c> / <c>_levelUpOpen</c> /
    /// <c>Session.IsPaused</c> 四个布尔互相约束，读代码得先推演出「现在到底能不能推进模拟」；
    /// 现在只有一个阶段值，冲突组合根本不存在。
    /// </summary>
    public enum BattlePhase
    {
        /// <summary>还没开始（Begin 之前），或对象已销毁。</summary>
        Idle = 0,

        /// <summary>正常战斗，模拟推进。</summary>
        Running = 1,

        /// <summary>升级三选一面板开着；可以连续多次，模拟冻结。</summary>
        LevelUp = 2,

        /// <summary>玩家/系统主动暂停（切后台、以后接暂停菜单），模拟冻结。</summary>
        Paused = 3,

        /// <summary>已分出胜负：清场、写记录、等结算面板关闭。</summary>
        Ending = 4,

        /// <summary>结算面板已关，准备退回主界面（终态）。</summary>
        Finished = 5,
    }

    /// <summary>阶段之间的合法转移。集中在这里，写错方向只会被拒绝并打日志，不会把流程拧坏。</summary>
    public static class BattlePhaseRules
    {
        /// <summary>只有 Running 推进模拟；其余阶段都在等别的东西（面板、玩家、结算）。</summary>
        public static bool BlocksSimulation(this BattlePhase phase)
        {
            return phase != BattlePhase.Running;
        }

        public static bool IsTerminal(this BattlePhase phase)
        {
            return phase == BattlePhase.Finished;
        }

        /// <summary>
        /// 允许的转移：
        /// Idle→Running、Running↔LevelUp、LevelUp→LevelUp（连升）、Running↔Paused、
        /// Paused→Ending（暂停菜单里「退出战斗」）、Running→Ending、Ending→Finished、任意→Idle（销毁）。
        ///
        /// 刻意不允许 LevelUp→Ending：升级面板期间模拟是冻结的，正常路径不可能分出胜负；
        /// 真出现了说明有外部代码在乱改状态，让它被拒绝并留下告警，比悄悄结算更好排查。
        /// </summary>
        public static bool CanTransition(BattlePhase from, BattlePhase to)
        {
            if (to == BattlePhase.Idle)
            {
                return true;
            }

            switch (from)
            {
                case BattlePhase.Idle:
                    return to == BattlePhase.Running;

                case BattlePhase.Running:
                    return to == BattlePhase.LevelUp
                        || to == BattlePhase.Paused
                        || to == BattlePhase.Ending;

                case BattlePhase.LevelUp:
                    return to == BattlePhase.LevelUp || to == BattlePhase.Running;

                case BattlePhase.Paused:
                    return to == BattlePhase.Running || to == BattlePhase.Ending;

                case BattlePhase.Ending:
                    return to == BattlePhase.Finished;

                default:
                    return false;
            }
        }
    }
}
