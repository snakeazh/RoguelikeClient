using System.Threading.Tasks;
using Framework.UI.DI;

namespace App.Bootstrap
{
    /// <summary>
    /// 一个全局系统（资源 / 存档 / 音频 / 网络 / 配置 / UI …）。
    ///
    /// 生命周期由 <see cref="ModuleHost"/> 按注册顺序驱动：
    /// <see cref="InitAsync"/> 里向容器注册自己的服务，<see cref="Dispose"/> 里只释放自己创建的东西。
    /// 模块之间不互相持有引用，需要别的模块时从 <see cref="ServiceContainer"/> 取接口。
    /// </summary>
    public interface IGameModule
    {
        /// <summary>日志里显示的名字（Resource / Audio …）。</summary>
        string Name { get; }

        /// <summary>初始化并向容器注册服务；抛异常由 <see cref="ModuleHost"/> 记日志并中断启动。</summary>
        Task InitAsync(ServiceContainer services);

        /// <summary>释放本模块申请的资源（按注册的逆序调用）。</summary>
        void Dispose();
    }
}
