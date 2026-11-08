using System.Threading.Tasks;
using Framework.Assets;
using Framework.UI.DI;

namespace App.Bootstrap.Modules
{
    /// <summary>资源框架：Editor/PC 直读 Assets/Res，打包后走 AssetBundle。</summary>
    public sealed class ResourceModule : IGameModule
    {
        public string Name => "Resource";

        public ResourceFrameworkContext Context { get; private set; }

        public async Task InitAsync(ServiceContainer services)
        {
            Context = ResourceFramework.Create();
            await Context.InitializeAsync();
            services.RegisterInstance<IResourceService>(Context.Resources);
        }

        public void Dispose()
        {
            Context?.ReleaseAll();
            Context = null;
        }
    }
}
