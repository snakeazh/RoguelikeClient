using System.Threading.Tasks;
using Framework.Save;
using Framework.UI.DI;

namespace App.Bootstrap.Modules
{
    /// <summary>存档：Editor/PC 用 PlayerPrefs，微信小游戏编译开关下自动切 WeChatSave。</summary>
    public sealed class SaveModule : IGameModule
    {
        public string Name => "Save";

        public ISaveService Save { get; private set; }

        public Task InitAsync(ServiceContainer services)
        {
            Save = SaveFramework.Create();
            services.RegisterInstance<ISaveService>(Save);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Save?.Save();
            Save = null;
        }
    }
}
