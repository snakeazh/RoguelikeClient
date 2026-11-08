using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人行为注册表：`EnemyDef.Behavior` 是谁，就用谁去跑这一帧。
    /// 加一种新敌人行为 = 写一个 <see cref="IEnemyBehavior"/> + 在这里注册一行 + 枚举 + json，
    /// `EnemyUnit` 与 `BattleRunner` 都不用改。
    /// </summary>
    public static class EnemyBehaviors
    {
        private static readonly Dictionary<EnemyBehavior, IEnemyBehavior> Map =
            new Dictionary<EnemyBehavior, IEnemyBehavior>
            {
                // Chaser / Tank 行动规则一样（都是直线贴脸），差别只在数值与体型
                { EnemyBehavior.Chaser, new ChaserBehavior() },
                { EnemyBehavior.Tank, new ChaserBehavior() },
                { EnemyBehavior.Runner, new RunnerBehavior() },
                { EnemyBehavior.Shooter, new RangedBehavior() },
                { EnemyBehavior.Exploder, new ExploderBehavior() },
                { EnemyBehavior.Splitter, new SplitterBehavior() }
            };

        private static readonly IEnemyBehavior Fallback = new ChaserBehavior();

        public static IEnemyBehavior Get(EnemyBehavior behavior)
        {
            if (Map.TryGetValue(behavior, out var strategy))
            {
                return strategy;
            }

            Debug.LogWarning("[Enemy] no behavior registered for " + behavior + ", falling back to Chaser.");
            return Fallback;
        }
    }
}
