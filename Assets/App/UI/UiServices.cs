using App.Audio;

namespace App.UI
{
    /// <summary>
    /// 预挂载在预制体上的 UI 组件（按钮音效、Toast 面板）没法走构造函数注入。
    /// 启动阶段由 <c>UiModule</c> 安装一次，运行期只读；不提供按类型查找的通用入口，
    /// 想加新服务就在这里显式加一个属性 —— 谁在用一眼能看出来。
    /// </summary>
    public static class UiServices
    {
        public static IAudioService Audio { get; private set; }

        public static ToastService Toast { get; private set; }

        public static void Install(IAudioService audio, ToastService toast)
        {
            Audio = audio;
            Toast = toast;
        }

        public static void Clear()
        {
            Audio = null;
            Toast = null;
        }
    }
}
