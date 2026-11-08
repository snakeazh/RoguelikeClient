using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取物：只负责「飞向玩家」与「外观」，具体效果由 `PickupDef.Effects` 里的原语逐条执行。
    /// 这样加一种拾取物通常只是往 `Pickups.json` 加一行，不用新增枚举或改这里。
    /// </summary>
    public sealed class PickupUnit : MonoBehaviour
    {
        private float _life = 90f;

        public PickupDef Def { get; private set; }

        /// <summary>本次掉落的数量（经验/金币）。效果里 `Args.Amount` 为空时用它。</summary>
        public int Amount { get; private set; }

        public bool Attracted { get; private set; }

        public void Init(PickupDef def, int amount, Vector2 position)
        {
            Def = def;
            Amount = amount;
            _life = def != null && def.Lifetime > 0f ? def.Lifetime : 90f;

            transform.position = position;
            name = "Pickup_" + (def != null ? def.Id.ToString() : "?");

            var size = def != null && def.Size > 0f ? def.Size : 0.3f;
            var spriteKey = def != null && !string.IsNullOrEmpty(def.Sprite)
                ? "Battle/Sprites/" + def.Sprite
                : BattleVisuals.PickupFallback;

            BattleVisuals.Body(transform, spriteKey, Color.white, size, BattleSort.Pickup, PlaceholderArt.Circle);
        }

        /// <summary>被磁力道具强制吸取。</summary>
        public void ForceAttract() => Attracted = true;

        /// <summary>返回 true 表示已被拾取（调用方负责结算与销毁）。</summary>
        public bool Tick(float deltaTime, PlayerUnit player, float magnetRadius)
        {
            _life -= deltaTime;
            if (_life <= 0f || player == null)
            {
                return _life <= 0f;
            }

            var position = (Vector2)transform.position;
            var playerPosition = (Vector2)player.transform.position;
            var distance = Vector2.Distance(position, playerPosition);

            if (distance <= magnetRadius)
            {
                Attracted = true;
            }

            if (Attracted)
            {
                var dir = (playerPosition - position).normalized;
                var speed = 10f + Mathf.Max(0f, 4f - distance) * 4f;
                transform.position += (Vector3)(dir * (speed * deltaTime));
                distance = Vector2.Distance(transform.position, player.transform.position);
            }

            return distance <= 0.5f;
        }
    }
}
