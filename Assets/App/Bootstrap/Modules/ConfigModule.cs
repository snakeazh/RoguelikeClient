using System.Threading.Tasks;
using App.Config;
using Framework.Assets;
using Framework.UI.DI;

namespace App.Bootstrap.Modules
{
    /// <summary>
    /// 配置表：ViewModel 的构造函数经常直接读表，所以必须早于 UI 模块。
    /// </summary>
    public sealed class ConfigModule : IGameModule
    {
        public string Name => "Config";

        public async Task InitAsync(ServiceContainer services)
        {
            await ConfigTables.LoadAsync(services.Resolve<IResourceService>());
        }

        public void Dispose()
        {
            ConfigTables.Clear();
        }
    }
}
