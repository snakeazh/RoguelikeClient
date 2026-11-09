using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 武器形态（<see cref="IWeaponBehavior"/>）能用到的世界能力。
    ///
    /// 形态只需要「玩家在哪、附近有谁、结算一次伤害、发一发弹体」；
    /// 刷怪、潮水、地图边界这些和武器无关的成员不在这里，避免形态顺手改波次。
    /// </summary>
    public interface IWeaponContext
    {
        /// <summary>玩家状态（纯数据）：形态只读位置/朝向。</summary>
        PlayerState Player { get; }

        Vector2 PlayerPosition { get; }

        /// <summary>敌人状态列表（权威）：形态只读，改状态一律走 <see cref="DamageEnemy"/>。</summary>
        IReadOnlyList<EnemyState> Enemies { get; }

        /// <summary>本局已进行的秒数（形态做周期性表现时用）。</summary>
        float ElapsedSeconds { get; }

        void DamageEnemy(EnemyState enemy, DamageInfo damage);

        void SpawnPlayerProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            EnemyState homingTarget,
            string spriteKey);
    }
}
