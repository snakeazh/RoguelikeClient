using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体规则（<see cref="ProjectileSystem"/>）能用到的世界能力：
    /// 命中查询 + 伤害结算 + 出界判断。它不刷怪、不碰波次，也不需要玩家的朝向。
    /// </summary>
    public interface IProjectileContext
    {
        /// <summary>玩家状态：命中判定读位置与半径。</summary>
        PlayerState Player { get; }

        IReadOnlyList<EnemyState> Enemies { get; }

        /// <summary>点是否还在地图内（弹体飞出地图就回收）。</summary>
        bool IsInsideArena(Vector3 position, float margin);

        void DamageEnemy(EnemyState enemy, DamageInfo damage);

        void DamagePlayer(float amount);

        void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer);
    }
}
