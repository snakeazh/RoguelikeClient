using System.Threading.Tasks;
using App.Battle;
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

            Characters = new CharacterSelection(save);
            services.RegisterInstance(Characters);

            services.RegisterInstance(new BattleRecords(save));
            services.RegisterInstance(new BattleLauncher(services.Resolve<IUIManager>(), Characters));

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Characters = null;
        }
    }
}
