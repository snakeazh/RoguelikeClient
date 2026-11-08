using System;
using System.Diagnostics;
using System.Threading.Tasks;
using App.Audio;
using App.Battle;
using App.Config;
using App.Net;
using App.Resources;
using App.UI;
using App.UI.Home;
using Framework.Assets;
using Framework.Log;
using Framework.Save;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace App.Bootstrap
{
    /// <summary>
    /// 唯一启动入口：挂在启动场景的一个空物体上即可（Demo 场景 Scenes/Main）。
    ///
    /// 固定启动顺序，改动顺序前先想清楚依赖：
    ///   AppServices.Create → ResourceFramework → SaveFramework → AudioService
    ///   → HttpApiClient → ConfigTables → 预载 UIRoot → UIFramework.Create
    ///   → 注册 ToastService → 打开首屏。
    /// UIFramework.Create 要求 IResourceService 已 Initialize 且 "UI/UIRoot" 已在缓存里，
    /// 所以上面必须先 await LoadAsync。新增业务服务请注册在 ConfigTables 之后、UIFramework 之前。
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
        private ResourceFrameworkContext _resources;
        private UIFrameworkContext _ui;

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

            // 2. 资源框架：Editor/PC 直读 Assets/Res，打包后走 AssetBundle。
            _resources = ResourceFramework.Create();
            await _resources.InitializeAsync();
            _services.Register(_resources.Resources);

            // 3. 存档（Editor/PC 用 PlayerPrefs；微信小游戏编译开关下自动切 WeChatSave）。
            var save = SaveFramework.Create();
            _services.Register(save);

            // 4. 音频：AudioService 自己建 BGM/SFX 子物体并还原开关状态。
            var audioHost = new GameObject("Audio");
            audioHost.transform.SetParent(_services.transform, false);
            var audio = new AudioService(save, audioHost);
            audio.Load();
            _services.Register<IAudioService>(audio);
            await audio.PreloadUiClickAsync(_resources.Resources);

            // 5. 网络：只建通用 HTTP 客户端，业务端点各自封装在 App/Net 下。
            _services.Register(new HttpApiClient(save));

            // 6. 配置表（ViewModel 构造函数经常直接读表，必须早于 UI）。
            await ConfigTables.LoadAsync(_resources.Resources);

            // 7. UIRoot 必须先进入资源缓存，UIFramework.Create 不做同步阻塞加载。
            await _resources.Resources.LoadAsync<GameObject>(ResResourcePaths.UIRoot);

            // 8. UI：注册 UIRoot / Navigator / DialogService / 自动注册所有 [AutoScreen]。
            _ui = UIFramework.Create(_services.Container);
            _services.Register(new ToastService(_services.Resolve<IUINavigator>()));

            // 角色选择（存 ISaveService，首页卡片与战斗入口共用）。
            var characters = new CharacterSelection(save);
            _services.Register(characters);

            // 战斗入口（依赖 IUIManager，必须在 UIFramework.Create 之后注册）。
            _services.Register(new BattleLauncher(_ui.UI, characters));

            // 局外记录（金币 / 次数 / 最高纪录），依赖 ISaveService。
            _services.Register(new BattleRecords(save));

            // 9. 首屏。
            var home = _services.Container.Resolve<HomeViewModel>();
            await _ui.UI.Open(home);

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
            _resources?.ReleaseAll();
            _resources = null;
            _ui = null;
        }
    }
}
