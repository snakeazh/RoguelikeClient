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
        private async Task OpenLevelUpAsync()
        {
            if (_levelUpOpen)
            {
                return;
            }

            _levelUpOpen = true;
            Session.IsPaused = true;

            try
            {
                var options = ChoiceService.Build(Session);
                var vm = new LevelUpViewModel();
                vm.Setup(Session.Level + 1, options);
                await _ui.Open(vm);
                var index = await vm.WaitChoiceAsync();
                await _ui.Close(vm);

                Session.IsPaused = false;

                if (index >= 0 && index < options.Count)
                {
                    ChoiceService.Apply(Session, options[index]);
                }

                Session.TryLevelUp();
                Session.HealPercent(0.15f);
                StateChanged?.Invoke();
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
                Session.IsPaused = false;
            }
            finally
            {
                _levelUpOpen = false;
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
            if (_ending)
            {
                return;
            }

            _ending = true;
            _running = false;
            Session.Finish(victory);
            Session.IsPaused = true;
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

            Session.IsPaused = false;
            _onExit?.Invoke();
        }
    }
}
