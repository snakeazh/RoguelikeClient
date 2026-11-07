using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗输入：左半屏动态摇杆 + 右下冲刺按钮。
    /// 直接读旧版 Input Manager（ProjectSettings 里 activeInputHandler = 0）。
    ///
    /// 键鼠调试：按住鼠标左键在左半屏拖动 = 摇杆；左半屏起点不固定，摇杆原点就是按下点。
    /// 触屏：第一根手指。右下角的冲刺按钮由 HUD 上的 Button 触发 <see cref="RequestDash"/>。
    /// </summary>
    public static class BattleInput
    {
        /// <summary>自动战斗（冒烟测试 / 演示用）：跳过真实输入，使用 AutoMove / AutoDash。</summary>
        public static bool AutoPlay;

        public static Vector2 AutoMove;
        public static bool AutoDash;

        /// <summary>本帧请求冲刺（HUD 按钮或 AutoDash）。</summary>
        public static bool DashRequested { get; private set; }

        public static Vector2 PointerPosition { get; private set; }

        public static bool PointerDown { get; private set; }

        public static bool PointerHeld { get; private set; }

        public static void RequestDash() => DashRequested = true;

        public static void Reset()
        {
            AutoPlay = false;
            AutoMove = Vector2.zero;
            AutoDash = false;
            DashRequested = false;
            PointerDown = false;
            PointerHeld = false;
        }

        /// <summary>每帧开头由 BattleRunner 调用一次，刷新指针状态。</summary>
        public static void Poll()
        {
            PointerDown = false;
            PointerHeld = false;

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                PointerPosition = touch.position;
                PointerDown = touch.phase == TouchPhase.Began;
                PointerHeld = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return;
            }

            PointerPosition = Input.mousePosition;
            PointerDown = Input.GetMouseButtonDown(0);
            PointerHeld = Input.GetMouseButton(0);
        }

        /// <summary>本帧结束后清掉一次性标记（冲刺按下）。</summary>
        public static void EndFrame()
        {
            DashRequested = false;
        }

        public static bool ConsumeDash()
        {
            if (!DashRequested)
            {
                return false;
            }

            DashRequested = false;
            return true;
        }
    }
}
