using System.Collections.Generic;
using System.Threading.Tasks;
using App.Battle;
using Framework.UI.Core;
using Framework.UI.View;

namespace App.UI.Battle
{
    /// <summary>
    /// 升级三选一：时停期间弹出，选中后把下标交回 BattleRunner。
    /// 三个选项槽位预留在预制体上（Option1/2/3），比动态列表更好做美术与动效。
    /// </summary>
    public sealed class LevelUpViewModel : ViewModelBase
    {
        private TaskCompletionSource<int> _tcs = new TaskCompletionSource<int>();
        private List<ChoiceOption> _options = new List<ChoiceOption>();

        public LevelUpViewModel()
        {
            Choose1Command = new RelayCommand(() => Choose(0));
            Choose2Command = new RelayCommand(() => Choose(1));
            Choose3Command = new RelayCommand(() => Choose(2));
        }

        public ObservableProperty<string> Header { get; } = new ObservableProperty<string>("升级！选择一项强化");

        public ObservableProperty<string> Option1Title { get; } = new ObservableProperty<string>(string.Empty);
        public ObservableProperty<string> Option1Desc { get; } = new ObservableProperty<string>(string.Empty);
        public ObservableProperty<string> Option2Title { get; } = new ObservableProperty<string>(string.Empty);
        public ObservableProperty<string> Option2Desc { get; } = new ObservableProperty<string>(string.Empty);
        public ObservableProperty<string> Option3Title { get; } = new ObservableProperty<string>(string.Empty);
        public ObservableProperty<string> Option3Desc { get; } = new ObservableProperty<string>(string.Empty);

        public IRelayCommand Choose1Command { get; }
        public IRelayCommand Choose2Command { get; }
        public IRelayCommand Choose3Command { get; }

        public void Setup(int newLevel, List<ChoiceOption> options)
        {
            _options = options ?? new List<ChoiceOption>();
            Header.Value = $"升到 Lv.{newLevel}，选择一项强化";

            SetSlot(0, Option1Title, Option1Desc);
            SetSlot(1, Option2Title, Option2Desc);
            SetSlot(2, Option3Title, Option3Desc);
        }

        public Task<int> WaitChoiceAsync() => _tcs.Task;

        private void SetSlot(int index, ObservableProperty<string> title, ObservableProperty<string> desc)
        {
            if (index < _options.Count)
            {
                title.Value = _options[index].Name;
                desc.Value = _options[index].Description;
            }
            else
            {
                title.Value = "—";
                desc.Value = string.Empty;
            }
        }

        private void Choose(int index)
        {
            if (_tcs.Task.IsCompleted || index >= _options.Count)
            {
                return;
            }

            _tcs.TrySetResult(index);
        }
    }
}
