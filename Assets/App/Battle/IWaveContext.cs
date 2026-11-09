using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 波次规则（<see cref="IWaveStep"/> / <see cref="EnemySpawner"/>）能用到的世界能力。
    ///
    /// 只有「在哪儿刷、刷什么、什么时候收尾、要不要提示」——伤害、弹体、拾取结算一概看不见，
    /// 这样加波次时不可能顺手改到战斗数值，headless 也只需要实现这 7 个成员。
    /// </summary>
    public interface IWaveContext
    {
        /// <summary>玩家当前位置（环形的波次以它为中心）。</summary>
        Vector2 PlayerPosition { get; }

        /// <summary>在镜头边缘外取一个可走、且不在当前视野里的刷怪点。</summary>
        Vector2 RandomSpawnPosition();

        void SpawnEnemy(int enemyId, Vector2 position, EliteAffix affix, float elapsed);

        void SpawnBoss(int enemyId, float elapsed);

        /// <summary>怪物潮：按节奏连续刷新一段时间（实现方起协程）。</summary>
        void RunTide(WaveDef wave);

        /// <summary>收尾：把场上剩余的资源堆到玩家脚下。</summary>
        void DropRestPickups();

        void Toast(string message);
    }
}
