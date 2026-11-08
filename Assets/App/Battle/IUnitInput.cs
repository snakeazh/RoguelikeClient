using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一个单位每帧的「意图」：往哪个方向走、要不要冲刺。
    ///
    /// 谁产生这个意图由单位自己持有：
    /// - 玩家 → <see cref="PlayerInput"/>（摇杆 / 键盘）
    /// - 冒烟机器人 → <see cref="AutoPlayerInput"/>（按战场状态算路）
    /// - 怪物 → <see cref="EnemyInput"/>（由 <see cref="IEnemyBehavior"/> 每帧写入方向）
    ///
    /// 单位只消费 <see cref="Move"/>，不再各自维护「谁在控制我」的布尔标记。
    /// </summary>
    public interface IUnitInput
    {
        /// <summary>本帧的移动方向，长度 0..1（1 = 全速）。</summary>
        Vector2 Move { get; }

        /// <summary>本帧是否请求冲刺（目前只有玩家用到）。</summary>
        bool DashRequested { get; }

        /// <summary>每帧更新一次意图：读设备 / 做一次决策 / 什么都不做。</summary>
        void Update(float deltaTime);
    }
}
