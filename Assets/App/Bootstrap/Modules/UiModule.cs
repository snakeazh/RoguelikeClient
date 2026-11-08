using System.Threading.Tasks;
using App.Resources;
using App.UI;
using Framework.Assets;
using Framework.UI;
using Framework.UI.DI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Bootstrap.Modules
{
    /// <summary>
    /// UI：先把 UIRoot 读进资源缓存，再交给 <see cref="UIFramework.Create"/>。
    /// UIFramework.Create 不做同步阻塞加载，"UI/UIRoot" 不在缓存里会直接抛异常。
    /// </summary>
    public sealed class UiModule : IGameModule
    {
        public string Name => "UI";

        public UIFrameworkContext Context { get; private set; }

        public async Task InitAsync(ServiceContainer services)
        {
            var resources = services.Resolve<IResourceService>();
            await resources.LoadAsync<GameObject>(ResResourcePaths.UIRoot);

            Context = UIFramework.Create(services);
            services.RegisterInstance(new ToastService(services.Resolve<IUINavigator>()));
        }

        public void Dispose()
        {
            Context = null;
        }
    }
}
