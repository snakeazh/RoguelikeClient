namespace App.Battle
{
    /// <summary>
    /// 一次伤害的完整描述。
    ///
    /// 以前暴击是「<c>RollDamage</c> 把结果记进字段、调用方再靠实参从左到右求值把标记取回来」的旁路写法，
    /// 读代码要先知道求值顺序才敢改。现在数值和暴击标记一起作为参数流动：
    /// 飘字颜色、火花尺寸、后续可能的吸血/减伤都只看这一个值。
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;

        public readonly bool Crit;

        public DamageInfo(float amount, bool crit = false)
        {
            Amount = amount;
            Crit = crit;
        }

        /// <summary>按比例缩放：溅射 0.5 倍、连锁每跳 0.85 倍都用它，暴击标记跟着走。</summary>
        public DamageInfo Scaled(float factor)
        {
            return new DamageInfo(Amount * factor, Crit);
        }

        /// <summary>不会暴击的伤害：敌人接触、自爆、场景炸弹。</summary>
        public static DamageInfo Plain(float amount)
        {
            return new DamageInfo(amount, false);
        }
    }
}
