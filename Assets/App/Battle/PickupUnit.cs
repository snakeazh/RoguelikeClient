using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取物的表现层（P3-1）：把 <see cref="PickupState"/> 同步成 Transform 与精灵。
    /// 吸附、拾取判定、效果结算都在 <see cref="PickupSystem"/>，这里不写任何规则。
    /// </summary>
    public sealed class PickupUnit : MonoBehaviour
    {
        /// <summary>逻辑状态：只有 <see cref="PickupSystem"/> 写它，这里只读。</summary>
        public PickupState State { get; private set; }

        public PickupDef Def => State != null ? State.Def : null;

        /// <summary>本次掉落的数量（经验/金币）。效果里 `Args.Amount` 为空时用它。</summary>
        public int Amount => State != null ? State.Amount : 0;

        public bool Attracted => State != null && State.Attracted;

        public void Init(PickupState state)
        {
            State = state;

            name = "Pickup_" + (Def != null ? Def.Id.ToString() : "?");

            var size = Def != null && Def.Size > 0f ? Def.Size : 0.3f;
            var spriteKey = Def != null && !string.IsNullOrEmpty(Def.Sprite)
                ? "Battle/Sprites/" + Def.Sprite
                : BattleVisuals.PickupFallback;

            BattleVisuals.Body(transform, spriteKey, Color.white, size, BattleSort.Pickup, PlaceholderArt.Circle);
            SyncFromState();
        }

        /// <summary>逻辑帧结束后调用：只同步位置。</summary>
        public void SyncFromState()
        {
            if (State != null)
            {
                transform.position = State.Position;
            }
        }
    }
}
