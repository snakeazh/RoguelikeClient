using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 被动输入通道：方向由外部每帧写入，自身不做任何决策。
    /// 冒烟机器人把算好的方向写进 <see cref="Set"/>；敌人行为用它表达「这帧往哪走」。
    /// </summary>
    public sealed class ScriptedInput : IUnitInput
    {
        public Vector2 Move { get; private set; }

        public bool DashRequested { get; private set; }

        public void Set(Vector2 direction, bool dash = false)
        {
            Move = Vector2.ClampMagnitude(direction, 1f);
            DashRequested = dash;
        }

        public void Update(float deltaTime)
        {
            // 被动通道：方向由调用方每帧写入，这里不需要做任何事。
        }
    }
}
