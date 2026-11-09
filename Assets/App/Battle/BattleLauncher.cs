using System;
using System.Threading.Tasks;
using App.Bootstrap;
using App.UI;
using App.UI.Battle;
using App.UI.Home;
using Framework.Assets;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗入口服务：进战斗时关首页、建 BattleRunner、开 HUD；结算后清场并回首页。
    /// 依赖全部由构造函数注入（看着乐观点，但这样这个类可以在没有 Game 容器时单独测）。
    /// </summary>
    public sealed class BattleLauncher
    {
        private readonly IUIManager _ui;
        private readonly CharacterSelection _characters;
        private readonly IResourceService _resources;
        private readonly BattleRecords _records;
        private readonly Func<HomeViewModel> _homeFactory;
        private bool _starting;

        public BattleLauncher(
            IUIManager ui,
            CharacterSelection characters,
            IResourceService resources,
            BattleRecords records,
            Func<HomeViewModel> homeFactory)
        {
            _ui = ui;
            _characters = characters;
            _resources = resources;
            _records = records;
            _homeFactory = homeFactory;
        }

        /// <summary>当前这一局的 runner（没有战斗时为 null）。由 launcher 持有，外部不再需要找场景。</summary>
        public BattleRunner Runner { get; private set; }

        /// <summary>
        /// 下一局的随机种子（0 = 每局随机）。冒烟 / headless 回归设成固定值即可逐位复现。
        /// </summary>
        public int NextSeed { get; set; }

        /// <summary>
        /// 下一局的固定步长（0 = 真实帧时间）。必须在 <see cref="BattleRunner.Begin"/> 之前设好：
        /// 开局头几帧如果用的是真实 deltaTime，同种子两局也会分叉。
        /// </summary>
        public float NextFixedStep { get; set; }

        /// <summary>下一局的模拟倍速（0 表示 1 倍）。同样要在 <see cref="BattleRunner.Begin"/> 之前设好。</summary>
        public float NextSimulationSpeed { get; set; }

        public bool IsRunning => Runner != null;

        public async Task StartAsync()
        {
            if (_starting || IsRunning)
            {
                return;
            }

            _starting = true;
            try
            {
                // 先把 2D 精灵/预制体读进内存，再建战斗（读不到时内部退化成占位图形）。
                if (_resources != null)
                {
                    await BattleVisuals.LoadAsync(_resources);
                }

                await _ui.Close(UILayer.Page);

                var go = new GameObject("Battle");
                var runner = go.AddComponent<BattleRunner>();
                Runner = runner;
                runner.FixedStepSeconds = NextFixedStep;
                if (NextSimulationSpeed > 0f)
                {
                    runner.SimulationSpeed = NextSimulationSpeed;
                }

                // 规则层只发消息，怎么显示由 UI 层决定；headless 时用 NullBattleNotifier。
                runner.Notifier = new ToastBattleNotifier();
                runner.Begin(_ui, OnBattleExit, NextSeed, _characters.SelectedId, _records);

                await _ui.Open(new BattleHudViewModel(runner));
                AppLog.Info(LogChannel.Game, "[Battle] launcher started");
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
                _starting = false;
            }
        }

        private async void OnBattleExit()
        {
            try
            {
                var runner = Runner;
                await _ui.Close(UILayer.Hud);

                if (runner != null)
                {
                    UnityEngine.Object.Destroy(runner.gameObject);
                }

                Runner = null;

                await _ui.Open(_homeFactory());
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
            }
            finally
            {
                _starting = false;
            }
        }
    }
}
