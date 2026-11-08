using System;
using System.Diagnostics;
using System.Threading.Tasks;
using App.Bootstrap.Modules;
using App.Net;
using App.UI.Home;
using Framework.Log;
using Framework.UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace App.Bootstrap
{
    /// <summary>
    /// 唯一启动入口：挂在启动场景的一个空物体上即可（Demo 场景 Scenes/Main）。
    ///
    /// 启动顺序 = <see cref="_modules"/> 的注册顺序，改顺序就改 Add 的顺序：
    ///   Resource → Save → Audio → Net → Config → UI → BattleServices。
    /// 依赖关系（改之前先确认）：
    ///   · UI 需要 IResourceService 已 Initialize、且 "UI/UIRoot" 已进缓存；
    ///   · Config 要在 UI 之前（ViewModel 构造函数经常直接读表）；
    ///   · BattleLauncher 依赖 IUIManager，必须排在 UI 之后。
    /// </summary>
    public sealed class AppBootstrap : MonoBehaviour
    {
        [Header("网络（不联网的 Demo 保持关闭）")]
        [SerializeField] private bool enableServerConnection;

        [SerializeField] private GameApiServerEndpoint serverEndpoint = GameApiServerEndpoint.LocalHost;
        [SerializeField] private string customServerHost = "127.0.0.1";
        [SerializeField] private int customServerPort = GameApiSettings.DefaultPort;

        [Header("渲染")]
        [SerializeField] private int targetFrameRate = 60;

        private AppServicesHost _services;
        private ModuleHost _modules;

        private async void Start()
        {
            try
            {
                await BootAsync();
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Bootstrap, e);
                Debug.LogException(e);
            }
        }

        private async Task BootAsync()
        {
            var watch = Stopwatch.StartNew();
            AppLog.Info(LogChannel.Bootstrap, "[Boot] start");

            ApplyRenderSettings();
            ApplyNetworkSettings();
            StartCoroutine(ApplyDesktopWindowDelayed());

            // 1. 服务容器（DontDestroyOnLoad，跨场景常驻）。
            _services = AppServices.Create();

            // 2. 模块按顺序初始化（每个模块自己往容器里注册服务）。
            _modules = new ModuleHost(_services.Container)
                .Add(new ResourceModule())
                .Add(new SaveModule())
                .Add(new AudioModule(_services.transform))
                .Add(new NetModule())
                .Add(new ConfigModule())
                .Add(new UiModule())
                .Add(new BattleServicesModule());

            await _modules.InitAllAsync();

            // 3. 首屏。
            var ui = _services.Resolve<IUIManager>();
            var home = _services.Container.Resolve<HomeViewModel>();
            await ui.Open(home);

            // 排查用：autocam.flag 存在时自动进战斗（配合 BattleRunner 的自动战斗）。
            // 发布包里 DebugAutoplayHook.Enabled 恒为 false，StartAutoplay 是空实现，不需要 #if。
            if (App.Battle.DebugAutoplayHook.Enabled)
            {
                App.Battle.DebugAutoplayHook.StartAutoplay(this, () => home.StartBattleCommand.Execute());
            }

            AppLog.Info(
                LogChannel.Bootstrap,
                $"[Boot] ready in {watch.ElapsedMilliseconds} ms (endpoint={GameApiSettings.BaseUrl}, net={GameApiSettings.Enabled})");
        }

        private void ApplyRenderSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;
            ApplyDesktopWindow();
        }

        /// <summary>
        /// 桌面播放器固定按 540×960 竖屏窗口打开：这是竖屏游戏，
        /// 上次运行记住的窗口尺寸（比如 4K 全屏）会让可见宽度超过地图宽度，镜头就没法横向跟随。
        /// 手机/网页平台不做任何修改。
        /// </summary>
        private void ApplyDesktopWindow()
        {
#if UNITY_STANDALONE
            if (Screen.width > Screen.height)
            {
                Screen.SetResolution(540, 960, FullScreenMode.Windowed);
            }
#endif
        }

        /// <summary>播放器读注册表里的窗口尺寸发生在脚本之后，等几帧再改一次才生效。</summary>
        private System.Collections.IEnumerator ApplyDesktopWindowDelayed()
        {
            for (var i = 0; i < 5; i++)
            {
                yield return null;
            }

            ApplyDesktopWindow();
        }

        private void ApplyNetworkSettings()
        {
            GameApiSettings.Enabled = enableServerConnection;
            GameApiSettings.BaseUrl = GameApiSettings.ResolveBaseUrl(
                serverEndpoint,
                customServerHost,
                customServerPort);
        }

        private void OnDestroy()
        {
            _modules?.DisposeAll();
            _modules = null;
        }
    }
}
