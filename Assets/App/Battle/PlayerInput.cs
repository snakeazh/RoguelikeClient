using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 玩家输入：左半屏动态摇杆 + 右下冲刺按钮，键盘作为 PC 调试兜底。
    ///
    /// 摇杆区域按「游戏画面」算而不是整个窗口：宽屏时相机会被裁成 9:16 的中间一列，
    /// 窗口左右是黑边；如果拿 <c>Screen.width / 2</c> 当分界线，画面中间按下去会落到
    /// 「右半屏」，表现就是宽屏按了没反应。
    /// </summary>
    public sealed class PlayerInput : IUnitInput
    {
        private bool _joystickActive;
        private Vector2 _joystickOrigin;
        private bool _dashPending;

        public Vector2 Move { get; private set; }

        public bool DashRequested { get; private set; }

        /// <summary>HUD 的冲刺按钮调这个；请求会保留到下一帧被 <see cref="Update"/> 消费。</summary>
        public void RequestDash()
        {
            _dashPending = true;
        }

        public void Update(float deltaTime)
        {
            var camera = Camera.main;
            var view = camera != null && camera.pixelWidth > 0 && camera.pixelHeight > 0
                ? camera.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            var pointer = ReadPointer(out var down, out var held);
            var insideView = view.width >= 1f && view.height >= 1f && view.Contains(pointer);

            if (down && insideView && pointer.x < view.xMin + view.width * 0.5f)
            {
                _joystickOrigin = pointer;
                _joystickActive = true;
            }

            var move = Vector2.zero;
            if (held && _joystickActive)
            {
                if (insideView)
                {
                    var radius = Mathf.Max(60f, view.height * 0.11f);
                    move = Vector2.ClampMagnitude((pointer - _joystickOrigin) / radius, 1f);
                }
            }
            else
            {
                _joystickActive = false;
            }

            // 键盘兜底（PC 调试）
            if (move == Vector2.zero)
            {
                move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }

            Move = move;
            DashRequested = _dashPending;
            _dashPending = false;
        }

        private static Vector2 ReadPointer(out bool down, out bool held)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                down = touch.phase == TouchPhase.Began;
                held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
                return touch.position;
            }

            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            return Input.mousePosition;
        }
    }
}
