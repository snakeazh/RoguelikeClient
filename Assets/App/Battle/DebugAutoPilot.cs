using App.Bootstrap;
using App.UI.Battle;
using Framework.UI;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 配合 <see cref="DebugAutoplayHook"/> 的自动选择：升级三选一自动点第一张。
    /// 正常的自动战斗机器人只负责移动/冲刺，升级面板必须由外部点掉，否则游戏会停在暂停状态。
    /// </summary>
    public sealed class DebugAutoPilot : MonoBehaviour
    {
        private IUIManager _ui;

        private void Update()
        {
            if (_ui == null)
            {
                if (!AppServices.IsReady)
                {
                    return;
                }

                _ui = AppServices.Container.GetService(typeof(IUIManager)) as IUIManager;
                if (_ui == null)
                {
                    return;
                }
            }

            var levelUp = _ui.FindOpen<LevelUpViewModel>();
            if (levelUp != null)
            {
                levelUp.Choose1Command.Execute();
            }
        }
    }
}
