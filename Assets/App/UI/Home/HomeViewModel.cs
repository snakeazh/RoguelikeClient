using System;
using System.Text;
using System.Threading.Tasks;
using App.Audio;
using App.Battle;
using App.Config;
using Framework.Log;
using Framework.UI;
using Framework.UI.Core;
using Framework.UI.View;

namespace App.UI.Home
{
    /// <summary>
    /// Demo 首页 ViewModel：演示配置表读取、文本绑定、命令绑定、对话框与 Toast。
    /// 业务功能从这里开始长：一个界面 = 一个 ViewModel（状态 + 命令）+ 一个 View（只管绑定）。
    /// </summary>
    public sealed class HomeViewModel : ViewModelBase
    {
        private readonly IUIManager _ui;
        private readonly ToastService _toast;
        private readonly IAudioService _audio;
        private readonly BattleLauncher _battle;
        private readonly BattleRecords _records;
        private readonly CharacterSelection _characters;
        private readonly BattleCatalog _catalog;

        public HomeViewModel(
            IUIManager ui,
            ToastService toast,
            IAudioService audio,
            BattleLauncher battle,
            BattleRecords records,
            CharacterSelection characters,
            BattleCatalog catalog)
        {
            _ui = ui;
            _toast = toast;
            _audio = audio;
            _battle = battle;
            _records = records;
            _characters = characters;
            _catalog = catalog;

            OpenDialogCommand = new RelayCommand(() => Run(OpenDialogAsync));
            ShowToastCommand = new RelayCommand(ShowToast);
            PlayClickCommand = new RelayCommand(PlayClick);
            StartBattleCommand = new RelayCommand(StartBattle);
            SelectRangerCommand = new RelayCommand(() => SelectCharacter(1));
            SelectWarriorCommand = new RelayCommand(() => SelectCharacter(2));
        }

        public ObservableProperty<string> Title { get; } =
            new ObservableProperty<string>("Roguelike Client");

        public ObservableProperty<string> Subtitle { get; } =
            new ObservableProperty<string>("Framework 演示工程 · 不含业务逻辑");

        /// <summary>配置表摘要，验证 json → 表 → 界面 的完整链路。</summary>
        public ObservableProperty<string> Content { get; } =
            new ObservableProperty<string>(string.Empty);

        /// <summary>交互反馈（按钮点击 / 对话框结果）。</summary>
        public ObservableProperty<string> Status { get; } =
            new ObservableProperty<string>("点击按钮开始体验");

        /// <summary>当前角色摘要（血/速/本命武器），选人按钮点了会刷新。</summary>
        public ObservableProperty<string> CharacterInfo { get; } =
            new ObservableProperty<string>(string.Empty);

        public IRelayCommand OpenDialogCommand { get; }

        public IRelayCommand ShowToastCommand { get; }

        public IRelayCommand PlayClickCommand { get; }

        public IRelayCommand StartBattleCommand { get; }

        public IRelayCommand SelectRangerCommand { get; }

        public IRelayCommand SelectWarriorCommand { get; }

        protected override Task OnOpen(object args)
        {
            RefreshContent();
            RefreshCharacterInfo();
            return Task.CompletedTask;
        }

        private void SelectCharacter(int characterId)
        {
            _characters.Select(characterId);
            _audio.PlayUiClick();
            RefreshCharacterInfo();
            Status.Value = $"已选择 {_characters.Selected?.Name}，点「开始战斗」进局";
        }

        private void RefreshCharacterInfo()
        {
            var def = _characters.Selected;
            if (def == null)
            {
                CharacterInfo.Value = "角色表未加载";
                return;
            }

            var weapon = _catalog.Weapon(def.InnateWeaponId);
            var weaponName = weapon != null ? weapon.Name : "无";
            CharacterInfo.Value =
                $"当前角色：{def.Name} · {def.MaxHp:0} 血 · {def.MoveSpeed:0.0} 速 · ★{weaponName}";
        }

        private void RefreshContent()
        {
            var builder = new StringBuilder();
            builder.AppendLine(_records.Summary);
            builder.AppendLine();
            builder.AppendLine($"DemoItemConfig: {DemoItemConfig.Count} rows");

            var total = 0;
            foreach (var row in DemoItemConfig.All.Values)
            {
                builder.AppendLine($"  [{row.Id}] {row.Name}  +{row.Value}  {row.Desc}");
                total += row.Value;
            }

            builder.Append($"total value = {total}");
            Content.Value = builder.ToString();
        }

        private async Task OpenDialogAsync()
        {
            var result = await _ui.Dialogs.ConfirmAsync(
                "确认对话框",
                "这是框架自带的 IDialogService，返回 DialogResult。",
                Framework.UI.Dialog.DialogButtons.OkCancel,
                "确定",
                "取消");

            Status.Value = $"对话框结果：{result}";
            AppLog.Info(LogChannel.UI, $"Demo dialog result: {result}");
        }

        private void ShowToast()
        {
            _toast.ShowSuccess("这是一条 Toast 提示");
            Status.Value = "已弹出 Toast";
        }

        private void PlayClick()
        {
            _audio.PlayUiClick();
            Status.Value = "已播放 UI 点击音（无音频资源时静默）";
        }

        private void StartBattle()
        {
            Status.Value = "进入战斗…";
            Run(_battle.StartAsync);
        }

        /// <summary>
        /// 命令是同步签名，异步流程统一从这里发车并在这里兜住异常，
        /// 避免 async void 把异常丢进 Unity 的未捕获队列。
        /// </summary>
        private void Run(Func<Task> work)
        {
            _ = RunCore(work);
        }

        private async Task RunCore(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.UI, e);
                Status.Value = "出错：" + e.Message;
            }
        }
    }
}
