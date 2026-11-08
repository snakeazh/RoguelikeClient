using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取物的纯逻辑状态（P3-1）：种类、数量、位置、剩余寿命、是否被吸、是否已结算。
    /// 「它是什么」由 <see cref="PickupDef"/>（`Pickups.json` 一行）决定，代码里没有种类枚举。
    /// </summary>
    public sealed class PickupState
    {
        public PickupDef Def;

        /// <summary>本次掉落的数量（经验/金币）。效果里 `Args.Amount` 为空时用它。</summary>
        public int Amount;

        public Vector2 Position;
        public float Life;

        /// <summary>被磁力吸住（磁铁道具或被动范围）。</summary>
        public bool Attracted;

        /// <summary>已被玩家吃到，需要结算效果。</summary>
        public bool Collected;

        /// <summary>已经离开世界（吃到或到期），表现层据此销毁。</summary>
        public bool Done;
    }
}
