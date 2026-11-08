namespace App.Battle
{
    /// <summary>
    /// 远程怪：保持在中距离（太远靠近、太近后退、距离合适就侧移），
    /// 攻击间隔到点朝玩家打一发弹体。炮手 / 深渊领主用它。
    /// </summary>
    public sealed class RangedBehavior : IEnemyBehavior
    {
        public void Tick(EnemyUnit enemy, PlayerUnit player, float deltaTime)
        {
            var dir = enemy.DirectionTo(player);
            var distance = enemy.DistanceTo(player);
            var speed = enemy.CurrentSpeed * deltaTime;
            var range = enemy.Def.AttackRange;

            if (distance > range * 0.85f)
            {
                enemy.MoveBy(dir, speed);
            }
            else if (distance < range * 0.5f)
            {
                enemy.MoveBy(-dir, speed);
            }
            else
            {
                enemy.MoveBy(new UnityEngine.Vector2(-dir.y, dir.x), speed * 0.4f);
            }

            enemy.AttackTimer -= deltaTime;
            if (enemy.AttackTimer > 0f)
            {
                return;
            }

            enemy.AttackTimer = UnityEngine.Mathf.Max(0.4f, enemy.Def.AttackInterval);
            var aim = enemy.DirectionTo(player);
            enemy.FireProjectile(aim);
        }

        public void OnDeath(EnemyUnit enemy)
        {
        }
    }
}
