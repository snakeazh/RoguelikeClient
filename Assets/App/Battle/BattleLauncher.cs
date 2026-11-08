using System.Threading.Tasks;
using App.Bootstrap;
using App.UI;
using App.UI.Battle;
using App.UI.Home;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗入口服务：进战斗时关首页、建 BattleRunner、开 HUD；结算后清场并回首页。
    /// 由 AppBootstrap 注册进容器，HomeViewModel 通过构造函数注入使用。
    /// </summary>
    public sealed class BattleLauncher
    {
        private readonly IUIManager _ui;
        private readonly CharacterSelection _characters;
        private bool _starting;

        public BattleLauncher(IUIManager ui, CharacterSelection characters)
        {
            _ui = ui;
            _characters = characters;
        }

        /// <summary>当前这一局的 runner（没有战斗时为 null）。由 launcher 持有，外部不再需要找场景。</summary>
        public BattleRunner Runner { get; private set; }

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
                var resources = AppServices.Container.GetService(typeof(Framework.Assets.IResourceService))
                    as Framework.Assets.IResourceService;
                if (resources != null)
                {
                    await BattleVisuals.LoadAsync(resources);
                }

                await _ui.Close(UILayer.Page);

                var go = new GameObject("Battle");
                var runner = go.AddComponent<BattleRunner>();
                Runner = runner;
                runner.Begin(_ui, OnBattleExit, 0, _characters.SelectedId);

                await _ui.Open(new BattleHudViewModel(runner));
                AppLog.Info(LogChannel.Game, "[Battle] launcher started");
            }
            catch (System.Exception e)
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
                    Object.Destroy(runner.gameObject);
                }

                Runner = null;

                var home = AppServices.Container.Resolve<HomeViewModel>();
                await _ui.Open(home);
            }
            catch (System.Exception e)
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
