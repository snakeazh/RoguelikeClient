using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Framework.Log;
using Framework.UI.DI;

namespace App.Bootstrap
{
    /// <summary>
    /// 模块宿主：按 <see cref="_modules"/> 的注册顺序初始化，按逆序释放。
    ///
    /// 启动顺序以前写在 <see cref="AppBootstrap"/> 的一长串编号注释里，现在顺序就是列表顺序，
    /// 加一个系统只需要 Add 一个模块，不用再去改 BootAsync 的编号。
    /// </summary>
    public sealed class ModuleHost
    {
        private readonly List<IGameModule> _modules = new List<IGameModule>(8);
        private readonly ServiceContainer _services;

        public ModuleHost(ServiceContainer services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IReadOnlyList<IGameModule> Modules => _modules;

        public ModuleHost Add(IGameModule module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            _modules.Add(module);
            return this;
        }

        public async Task InitAllAsync()
        {
            foreach (var module in _modules)
            {
                var watch = Stopwatch.StartNew();
                await module.InitAsync(_services);
                AppLog.Info(
                    LogChannel.Bootstrap,
                    $"[Boot] module {module.Name} ready in {watch.ElapsedMilliseconds} ms");
            }
        }

        public void DisposeAll()
        {
            for (var i = _modules.Count - 1; i >= 0; i--)
            {
                try
                {
                    _modules[i].Dispose();
                }
                catch (Exception e)
                {
                    AppLog.Exception(LogChannel.Bootstrap, e);
                }
            }

            _modules.Clear();
        }
    }
}
