using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一次模拟 tick 需要的外部视图。
    ///
    /// 之所以做成值类型集中传参：模拟层（<see cref="BattleSimulation"/>）不该知道
    /// <see cref="BattleRunner"/> 的存在，只该知道「这一帧敌人状态列表长什么样、玩家输入是什么、
    /// 是否被沙漏冻结」。headless 回归时换成纯数据的实现即可。
    /// </summary>
    public readonly struct SimulationWorld
    {
        public readonly IBattleContext Battle;

        public readonly IPickupContext Pickups;

        public readonly IEnemyContext Enemies;

        /// <summary>本帧参与模拟的敌人状态（权威列表，模拟层可直接增删）。</summary>
        public readonly List<EnemyState> EnemyStates;

        public readonly IUnitInput Input;

        public readonly Rect Walkable;

        /// <summary>沙漏冻结期间为 true：行为、位移、Boss 弹幕全部跳过。</summary>
        public readonly bool Frozen;

        public readonly float MagnetRadius;

        public SimulationWorld(
            IBattleContext battle,
            IPickupContext pickups,
            IEnemyContext enemies,
            List<EnemyState> enemyStates,
            IUnitInput input,
            Rect walkable,
            bool frozen,
            float magnetRadius)
        {
            Battle = battle;
            Pickups = pickups;
            Enemies = enemies;
            EnemyStates = enemyStates;
            Input = input;
            Walkable = walkable;
            Frozen = frozen;
            MagnetRadius = magnetRadius;
        }
    }
}
