using System.Threading.Tasks;
using App.Audio;
using Framework.Assets;
using Framework.Save;
using Framework.UI.DI;
using UnityEngine;

namespace App.Bootstrap.Modules
{
    /// <summary>
    /// 音频：AudioService 自己建 BGM/SFX 子物体并还原开关状态；
    /// 点击音是远程资源，加载失败只告警、不阻断启动。
    /// </summary>
    public sealed class AudioModule : IGameModule
    {
        private readonly Transform _hostRoot;
        private GameObject _host;

        public AudioModule(Transform hostRoot)
        {
            _hostRoot = hostRoot;
        }

        public string Name => "Audio";

        public IAudioService Audio { get; private set; }

        public async Task InitAsync(ServiceContainer services)
        {
            var save = services.Resolve<ISaveService>();
            var resources = services.Resolve<IResourceService>();

            _host = new GameObject("Audio");
            _host.transform.SetParent(_hostRoot, false);

            var audio = new AudioService(save, _host);
            audio.Load();
            Audio = audio;
            services.RegisterInstance<IAudioService>(audio);

            await audio.PreloadUiClickAsync(resources);
        }

        public void Dispose()
        {
            Audio = null;
            if (_host != null)
            {
                Object.Destroy(_host);
                _host = null;
            }
        }
    }
}
