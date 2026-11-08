using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 分裂怪：行动上和普通贴脸怪一样，差别在死亡时炸出两只小史莱姆。
    /// 精英词缀 `EliteAffix.Split` 的分裂仍在 <c>BattleRunner.KillEnemy</c> 里（那是词缀，不是行为）。
    /// </summary>
    public sealed class SplitterBehavior : ChaserBehavior
    {
        private const int ChildDefId = 8;
        private const int ChildCount = 2;

        public override void OnDeath(EnemyUnit enemy)
        {
            for (var i = 0; i < ChildCount; i++)
            {
                var offset = Random.insideUnitCircle * 0.6f;
                enemy.Context.SpawnEnemy(
                    ChildDefId, (Vector2)enemy.Position + offset, EliteAffix.None, enemy.Context.ElapsedSeconds);
            }
        }
    }
}
