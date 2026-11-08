using System.Threading.Tasks;
using App.Battle;
using Framework.UI.Core;
using Framework.UI.View;
using UnityEngine;

namespace App.UI.Battle
{
    /// <summary>战斗结算界面：胜负、存活时间、击杀、金币，点按钮回到首页。</summary>
    public sealed class BattleResultViewModel : ViewModelBase
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public BattleResultViewModel()
        {
            ReturnCommand = new RelayCommand(CloseResult);
        }

        public ObservableProperty<string> Title { get; } = new ObservableProperty<string>("战斗结束");

        public ObservableProperty<string> Subtitle { get; } = new ObservableProperty<string>(string.Empty);

        public ObservableProperty<string> Stats { get; } = new ObservableProperty<string>(string.Empty);

        public IRelayCommand ReturnCommand { get; }

        public void Setup(BattleSession session)
        {
            if (session == null)
            {
                return;
            }

            Title.Value = session.IsVictory ? "胜利！" : "失败";
            Subtitle.Value = session.IsVictory ? "你撑过了这一局" : "再来一次吧";

            var minutes = Mathf.FloorToInt(session.Elapsed / 60f);
            var seconds = Mathf.FloorToInt(session.Elapsed % 60f);

            var weaponLines = new System.Text.StringBuilder();
            var weaponCount = 0;
            foreach (var weapon in session.AllWeapons)
            {
                weaponLines.Append(weaponLines.Length == 0 ? string.Empty : "、")
                    .Append(weapon.IsInnate ? "★" : string.Empty)
                    .Append(weapon.Def.Name)
                    .Append("Lv").Append(weapon.Level);
                weaponCount++;
            }

            Stats.Value = $"存活 {minutes:00}:{seconds:00}  ·  等级 Lv.{session.Level}\n"
                          + $"击杀 {session.Kills}  ·  金币 {session.Gold}\n"
                          + $"武器 {weaponCount}  ·  被动 {session.Passives.Count}\n"
                          + (weaponLines.Length > 0 ? "\n" + weaponLines : string.Empty);
        }

        public Task WaitCloseAsync() => _tcs.Task;

        private void CloseResult()
        {
            if (!_tcs.Task.IsCompleted)
            {
                _tcs.TrySetResult(true);
            }
        }
    }
}
