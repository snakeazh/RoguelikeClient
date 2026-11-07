using System.Text;
using System.Threading.Tasks;
using App.Battle;
using Framework.UI.Core;
using Framework.UI.View;
using UnityEngine;

namespace App.UI.Battle
{
    /// <summary>
    /// 战斗 HUD：顶部计时/等级/经验、左侧血条、武器与被动摘要、右下冲刺按钮。
    /// 数据来源是 BattleRunner.Session，通过 BattleRunner.StateChanged 每 0.1 秒刷新一次。
    /// </summary>
    public sealed class BattleHudViewModel : ViewModelBase
    {
        private readonly BattleRunner _runner;

        public BattleHudViewModel(BattleRunner runner)
        {
            _runner = runner;
            DashCommand = new RelayCommand(() => BattleInput.RequestDash());
        }

        public ObservableProperty<string> Timer { get; } = new ObservableProperty<string>("05:00");

        public ObservableProperty<string> LevelText { get; } = new ObservableProperty<string>("Lv.1");

        public ObservableProperty<string> KillText { get; } = new ObservableProperty<string>("击杀 0");

        public ObservableProperty<string> WeaponText { get; } =
            new ObservableProperty<string>("武器：无");

        public ObservableProperty<string> PassiveText { get; } =
            new ObservableProperty<string>("被动：无");

        public ObservableProperty<string> BossHint { get; } = new ObservableProperty<string>(string.Empty);

        public ObservableProperty<float> HpFill { get; } = new ObservableProperty<float>(1f);

        public ObservableProperty<float> ExpFill { get; } = new ObservableProperty<float>(0f);

        public ObservableProperty<string> HpText { get; } = new ObservableProperty<string>("100 / 100");

        public IRelayCommand DashCommand { get; }

        protected override Task OnOpen(object args)
        {
            if (_runner != null)
            {
                _runner.StateChanged += Refresh;
            }

            Refresh();
            return base.OnOpen(args);
        }

        protected override Task OnClose()
        {
            if (_runner != null)
            {
                _runner.StateChanged -= Refresh;
            }

            return base.OnClose();
        }

        private void Refresh()
        {
            var session = _runner?.Session;
            if (session == null)
            {
                return;
            }

            var left = session.TimeLeft;
            Timer.Value = $"{Mathf.FloorToInt(left / 60f):00}:{Mathf.FloorToInt(left % 60f):00}";
            LevelText.Value = $"Lv.{session.Level}";
            KillText.Value = $"击杀 {session.Kills}";
            HpFill.Value = session.HpRatio;
            HpText.Value = $"{Mathf.CeilToInt(session.Hp)} / {Mathf.CeilToInt(session.MaxHp)}";
            ExpFill.Value = session.ExpRatio;
            BossHint.Value = session.BossPhase ? "Boss 阶段" : string.Empty;

            var weapons = new StringBuilder("武器：");
            if (session.Weapons.Count == 0)
            {
                weapons.Append("无");
            }
            else
            {
                for (var i = 0; i < session.Weapons.Count; i++)
                {
                    var weapon = session.Weapons[i];
                    if (i > 0)
                    {
                        weapons.Append("  ");
                    }

                    weapons.Append($"{weapon.Def.Name} Lv{weapon.Level}");
                }
            }

            WeaponText.Value = weapons.ToString();

            var passives = new StringBuilder("被动：");
            if (session.Passives.Count == 0)
            {
                passives.Append("无");
            }
            else
            {
                for (var i = 0; i < session.Passives.Count; i++)
                {
                    var passive = session.Passives[i];
                    if (i > 0)
                    {
                        passives.Append("  ");
                    }

                    passives.Append($"{passive.Def.Name} Lv{passive.Level}");
                }
            }

            PassiveText.Value = passives.ToString();
        }
    }
}
