using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 直线贴脸：朝玩家走，碰到就靠 <see cref="BattleRunner"/> 的接触伤害结算。
    /// Chaser / Tank 共用；Tank 的差别只是血量、体型与速度数值。
    /// </summary>
    public class ChaserBehavior : IEnemyBehavior
    {
        public virtual void Tick(EnemyUnit enemy, PlayerUnit player, float deltaTime)
        {
            enemy.MoveToward(player);
        }

        public virtual void OnDeath(EnemyUnit enemy)
        {
        }
    }
}
