using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 拾取物效果能用到的最小战斗能力集合。
    /// 效果类只拿到这些，不接触 <c>BattleRunner</c> 具体类型，也就不用跟着它一起改。
    /// </summary>
    public interface IPickupContext
    {
        BattleSession Session { get; }

        Vector3 PlayerPosition { get; }

        IReadOnlyList<PickupUnit> ActivePickups { get; }

        /// <summary>只伤敌人，不伤玩家。</summary>
        void ExplodeAt(Vector3 position, float radius, DamageInfo damage);

        void FreezeEnemies(float seconds);

        void GrantChest();

        void Toast(string message);
    }

    /// <summary>
    /// 效果原语。按字符串 key 注册，一个拾取物可以挂多条（复合），
    /// 加「回 50% 血的蛋糕」只需要改 json 参数，不用新增类型。
    /// </summary>
    public interface IPickupEffect
    {
        void Apply(IPickupContext context, PickupUnit pickup, App.Config.PickupArgs args);
    }
}
