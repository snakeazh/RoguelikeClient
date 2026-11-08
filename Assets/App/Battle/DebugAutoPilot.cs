using UnityEngine;

namespace App.Battle
{
#if UNITY_EDITOR || ENABLE_APP_LOG
    /// <summary>
    /// 配合 <see cref="DebugAutoplayHook"/> 的自动选择：升级三选一自动点第一张。
    /// 自动战斗机器人只负责移动/冲刺，升级面板必须由外部点掉，否则模拟会一直冻结在升级阶段。
    ///
    /// 只在编辑器 / 带 <c>ENABLE_APP_LOG</c> 的调试包里编译，发布包里这个类型根本不存在。
    /// </summary>
    public sealed class DebugAutoPilot : MonoBehaviour
    {
        private Framework.UI.IUIManager _ui;

        public void Init(Framework.UI.IUIManager ui)
        {
            _ui = ui;
        }

        private void Update()
        {
            if (_ui == null)
            {
                return;
            }

            var levelUp = _ui.FindOpen<App.UI.Battle.LevelUpViewModel>();
            if (levelUp != null)
            {
                levelUp.Choose1Command.Execute();
            }
        }
    }
#endif
}
