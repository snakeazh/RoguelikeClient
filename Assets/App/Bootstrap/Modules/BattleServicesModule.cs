using System.Threading.Tasks;
using App.Battle;
using App.UI.Home;
using Framework.Assets;
using Framework.Save;
using Framework.UI;
using Framework.UI.DI;

namespace App.Bootstrap.Modules
{
    /// <summary>
    /// 战斗相关的全局服务：角色选择（存 `ISaveService`）、局外记录、战斗入口。
    /// 必须在 UI 模块之后——<see cref="BattleLauncher"/> 依赖 <see cref="IUIManager"/>。
    /// </summary>
    public sealed class BattleServicesModule : IGameModule
    {
        public string Name => "BattleServices";

        public CharacterSelection Characters { get; private set; }

        public Task InitAsync(ServiceContainer services)
        {
            var save = services.Resolve<ISaveService>();

            var catalog = BattleCatalog.FromConfig();
            services.RegisterInstance(catalog);

            Characters = new CharacterSelection(save, catalog);
            services.RegisterInstance(Characters);

            var records = new BattleRecords(save);
            services.RegisterInstance(records);

            // HomeViewModel 由 UIFramework 注册为瞬时实例，退出战斗时由 launcher 现取一个新的。
            services.RegisterInstance<System.Func<HomeViewModel>>(() => services.Resolve<HomeViewModel>());

            services.RegisterInstance(new BattleLauncher(
                services.Resolve<IUIManager>(),
                Characters,
                catalog,
                services.Resolve<IResourceService>(),
                records,
                services.Resolve<System.Func<HomeViewModel>>()));

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Characters = null;
        }
    }
}
