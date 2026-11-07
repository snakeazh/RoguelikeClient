using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Bootstrap;
using App.Config;
using App.UI;
using App.UI.Battle;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    public sealed partial class BattleRunner : MonoBehaviour
    {
        // ---------- 升级 ----------
        /// <summary>
        /// 进一次升级就吃掉所有待升级的等级：连升时在同一个循环里连续开面板，
        /// 而不是关掉面板后靠下一帧的 Update 重新进来（那样中间会漏跑一帧推进）。
        /// </summary>
        private async Task RunLevelUpAsync()
        {
            if (!SetPhase(BattlePhase.LevelUp))
            {
                return;
            }

            try
            {
                while (Session.CanLevelUp && Phase == BattlePhase.LevelUp)
                {
                    var options = ChoiceService.Build(Session);
                    var vm = new LevelUpViewModel();
                    vm.Setup(Session.Level + 1, options);
                    await _ui.Open(vm);
                    var index = await vm.WaitChoiceAsync();
                    await _ui.Close(vm);

                    if (index >= 0 && index < options.Count)
                    {
                        ChoiceService.Apply(Session, options[index]);
                    }

                    Session.TryLevelUp();
                    Session.HealPercent(0.15f);
                    StateChanged?.Invoke();
                }
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
            }
            finally
            {
                // 只有还停在 LevelUp 才恢复；期间被销毁或已进入结算都不要覆盖状态。
                if (Phase == BattlePhase.LevelUp)
                {
                    SetPhase(BattlePhase.Running);
                }
            }
        }

        // ---------- 胜负 ----------
        private void CheckRoundEnd()
        {
            if (Session.IsOver)
            {
                return;
            }

            if (Session.Elapsed >= BattleSession.RoundDuration)
            {
                // 时间到：Boss 已死才算胜利，否则算超时失败（不然 Boss 没打死会一直打下去）。
                var bossAlive = _enemies.Any(e => e != null && e.IsBoss && !e.IsDead);
                Session.Finish(!bossAlive);
            }
        }

        private async Task EndAsync(bool victory)
        {
            // Phase 就是锁：重复调用会被 BattlePhaseRules 拒绝，不需要额外的 _ending。
            if (!SetPhase(BattlePhase.Ending))
            {
                return;
            }

            Session.Finish(victory);
            ClearBattlefield();
            StateChanged?.Invoke();

            // 局外结算：金币 / 次数 / 最高记录（服务未注册时静默跳过）。
            try
            {
                var records = AppServices.Resolve<BattleRecords>();
                records?.Commit(Session);
            }
            catch (Exception e)
            {
                AppLog.Warn(LogChannel.Game, "[Battle] records not committed: " + e.Message);
            }

            try
            {
                var vm = new BattleResultViewModel();
                vm.Setup(Session);
                await _ui.Open(vm);
                await vm.WaitCloseAsync();
                await _ui.Close(vm);
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
            }

            SetPhase(BattlePhase.Finished);
            _onExit?.Invoke();
        }
    }
}
