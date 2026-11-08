using System;
using System.Collections.Generic;

namespace App.Config
{
    /// <summary>
    /// 一个效果的扁平参数包。用「宽表」而不是每种效果一个类，
    /// 是因为 json 里写 `{ "Key": "heal", "Args": { "Percent": 0.3 } }` 比嵌套多态好写也好读；
    /// 各效果只读自己关心的字段，不用字段留默认值即可。
    /// </summary>
    [Serializable]
    public sealed class PickupArgs
    {
        /// <summary>数量（经验/金币）。小于等于 0 时用拾取物实例上的 Amount（掉落时按怪物经验算出来的）。</summary>
        public float Amount;

        /// <summary>百分比（回血）。</summary>
        public float Percent;

        /// <summary>秒数（冻结）。</summary>
        public float Seconds;

        /// <summary>半径（爆炸）。</summary>
        public float Radius;

        /// <summary>伤害（爆炸）。</summary>
        public float Damage;

        /// <summary>提示文案（toast）。</summary>
        public string Text = string.Empty;
    }

    [Serializable]
    public sealed class PickupEffectDef
    {
        /// <summary>效果原语 key，对应 <c>PickupEffects</c> 注册表里的名字。</summary>
        public string Key = string.Empty;

        public PickupArgs Args = new PickupArgs();
    }

    /// <summary>
    /// 拾取物定义：外观 + 效果列表。加一种拾取物通常只需要在这张表里加一行，
    /// 不需要新增枚举或改 <c>BattleRunner</c>。
    /// </summary>
    [Serializable]
    public sealed class PickupDef : ConfigRowBase<PickupDef>
    {
        /// <summary>对应 <c>Assets/Res/Battle/Sprites/&lt;Sprite&gt;.png</c>，留空用占位圆。</summary>
        public string Sprite = string.Empty;

        /// <summary>显示直径（世界单位）。</summary>
        public float Size = 0.3f;

        /// <summary>存活秒数，超时自动消失。</summary>
        public float Lifetime = 90f;

        /// <summary>掉落权重（当前掉落逻辑按规则挑 id，权重留给以后做加权随机）。</summary>
        public int Weight = 1;

        /// <summary>依次执行的效果。</summary>
        public List<PickupEffectDef> Effects = new List<PickupEffectDef>();
    }
}
