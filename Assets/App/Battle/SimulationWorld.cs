using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一次模拟 tick 需要的外部视图。
    ///
    /// 之所以做成值类型集中传参：模拟层（<see cref="BattleSimulation"/>）不该知道
    /// <see cref="BattleRunner"/> 的存在，只该知道「这一帧敌人列表长什么样、玩家输入是什么、
    /// 是否被沙漏冻结」。headless 回归时换成纯数据的实现即可。
    /// </summary>
    public readonly struct SimulationWorld
    {
        public readonly IBattleContext Battle;

        public readonly IPickupContext Pickups;

        public readonly IEnemyContext Enemies;

        /// <summary>本帧参与模拟的敌人（含视图，用于表现同步；模拟本身只读它们的 <see cref="EnemyUnit.State"/>）。</summary>
        public readonly IReadOnlyList<EnemyUnit> EnemyUnits;

        public readonly IUnitInput Input;

        public readonly Rect Walkable;

        /// <summary>沙漏冻结期间为 true：行为、位移、Boss 弹幕全部跳过。</summary>
        public readonly bool Frozen;

        public readonly float MagnetRadius;

        public SimulationWorld(
            IBattleContext battle,
            IPickupContext pickups,
            IEnemyContext enemies,
            IReadOnlyList<EnemyUnit> enemyUnits,
            IUnitInput input,
            Rect walkable,
            bool frozen,
            float magnetRadius)
        {
            Battle = battle;
            Pickups = pickups;
            Enemies = enemies;
            EnemyUnits = enemyUnits;
            Input = input;
            Walkable = walkable;
            Frozen = frozen;
            MagnetRadius = magnetRadius;
        }
    }
}
