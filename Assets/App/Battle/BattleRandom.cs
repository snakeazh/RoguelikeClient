using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗逻辑专用的随机数工具。
    ///
    /// 逻辑**不能**用 <c>UnityEngine.Random</c>：它和表现层共用同一条全局随机流，
    /// 特效（闪电抖动、飘字横移、镜头震屏、炮口抖动）一多一少就会把暴击、掉落、刷怪点全部带偏，
    /// 同种子两局跑不出同一个结果。所以逻辑统一走 <see cref="BattleSession.Rng"/>。
    /// </summary>
    public static class BattleRandom
    {
        public static float NextFloat(this System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        public static Vector2 NextInsideUnitCircle(this System.Random rng)
        {
            // 面积均匀：半径要开方，否则点会挤在圆心附近。
            var angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            var radius = Mathf.Sqrt((float)rng.NextDouble());
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        public static int NextIndex(this System.Random rng, int count)
        {
            return count <= 0 ? 0 : rng.Next(count);
        }
    }
}
