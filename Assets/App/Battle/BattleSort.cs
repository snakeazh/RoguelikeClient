using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗场景的渲染排序，集中一处避免各文件写死数字。
    ///
    /// 角色（玩家/敌人）按世界 y 排序：越靠下越靠前，并给每个实体一个稳定的子槽位（取实例 id 低两位），
    /// 这样重叠的精灵不会再因为「到相机的距离」随相机移动而变化、逐帧交换绘制顺序
    /// —— 这正是高速移动时「怪物闪烁」的根因。
    /// </summary>
    public static class BattleSort
    {
        public const int Ground = -100;
        public const int Wall = -98;

        // 每个 y 档位下的子槽位数量：同一档位的多个怪靠实例 id 分开。
        public const int SubSlots = 4;
        private const int CharacterFront = 80;
        private const int CharacterBack = 10;

        public const int Pickup = 400;
        public const int CannonBase = 410;
        public const int CannonBarrel = 412;
        public const int CannonCharge = 413;
        public const int CannonMuzzle = 414;
        public const int Blade = 420;
        public const int Bomb = 422;
        public const int Projectile = 440;
        public const int Fx = 450;
        public const int Lightning = 456;
        public const int Popup = 470;

        /// <summary>角色排序号：y 越低越靠前；tieBreak 用来把重叠的实体错开。</summary>
        public static int Character(float y, int tieBreak = 0)
        {
            var min = -BattleMap.Height * 0.5f;
            var max = BattleMap.Height * 0.5f;
            var t = Mathf.InverseLerp(min, max, y);
            var band = Mathf.RoundToInt(Mathf.Lerp(CharacterFront, CharacterBack, t));
            return band * SubSlots + (Mathf.Abs(tieBreak) % SubSlots);
        }

        /// <summary>影子排在所属角色的所有子槽位之下。</summary>
        public static int Shadow(int characterOrder)
        {
            return characterOrder - characterOrder % SubSlots - 1;
        }

        /// <summary>特效排序号：基础层 + 偏移（保留不同特效之间的前后关系）。</summary>
        public static int FxLayer(int offset = 0)
        {
            return Fx + offset;
        }
    }
}
