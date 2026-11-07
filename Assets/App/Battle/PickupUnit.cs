using UnityEngine;

namespace App.Battle
{
    public enum PickupKind
    {
        ExpSmall = 1,
        ExpMedium = 5,
        ExpLarge = 20,
        Cake,
        Gold,
        Chest,
        Magnet,
        Bomb,
        Hourglass
    }

    /// <summary>
    /// 拾取物：经验糖 / 小蛋糕 / 金币 / 宝箱 / 吸糖 / 炸弹 / 沙漏。
    /// 进入磁力半径后被吸向玩家，贴身后由 BattleRunner 结算效果。
    /// </summary>
    public sealed class PickupUnit : MonoBehaviour
    {
        private float _life = 90f;

        public PickupKind Kind { get; private set; }
        public int Value { get; private set; }
        public bool Attracted { get; private set; }

        public void Init(PickupKind kind, int value, Vector2 position)
        {
            Kind = kind;
            Value = value;
            transform.position = position;
            name = "Pickup_" + kind;

            var (spriteKey, fallback, size) = Appearance(kind);
            BattleVisuals.Body(transform, spriteKey, Color.white, size, BattleSort.Pickup, fallback);
        }

        private static (string spriteKey, Sprite fallback, float size) Appearance(PickupKind kind)
        {
            switch (kind)
            {
                case PickupKind.ExpMedium:
                    return ("Battle/Sprites/pickup_exp", PlaceholderArt.Circle, 0.32f);
                case PickupKind.ExpLarge:
                    return ("Battle/Sprites/pickup_exp_big", PlaceholderArt.Circle, 0.46f);
                case PickupKind.Cake:
                    return ("Battle/Sprites/pickup_cake", PlaceholderArt.Square, 0.44f);
                case PickupKind.Gold:
                    return ("Battle/Sprites/pickup_gold", PlaceholderArt.Circle, 0.30f);
                case PickupKind.Chest:
                    return ("Battle/Sprites/pickup_chest", PlaceholderArt.Square, 0.62f);
                case PickupKind.Magnet:
                    return ("Battle/Sprites/pickup_magnet", PlaceholderArt.Triangle, 0.40f);
                case PickupKind.Bomb:
                    return ("Battle/Sprites/pickup_bomb", PlaceholderArt.Circle, 0.42f);
                case PickupKind.Hourglass:
                    return ("Battle/Sprites/pickup_hourglass", PlaceholderArt.Square, 0.38f);
                default:
                    return ("Battle/Sprites/pickup_exp", PlaceholderArt.Circle, 0.24f);
            }
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
