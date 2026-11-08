using System.Threading.Tasks;
using App.Net;
using Framework.Save;
using Framework.UI.DI;

namespace App.Bootstrap.Modules
{
    /// <summary>网络：只建通用 HTTP 客户端；业务端点各自封装在 App/Net 下。</summary>
    public sealed class NetModule : IGameModule
    {
        public string Name => "Net";

        public HttpApiClient Client { get; private set; }

        public Task InitAsync(ServiceContainer services)
        {
            Client = new HttpApiClient(services.Resolve<ISaveService>());
            services.RegisterInstance(Client);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Client = null;
        }
    }
}
