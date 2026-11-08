using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人与玩家的接触伤害（P3-1b）：距离判定 + 0.5 秒冷却 + 施加减速。
    ///
    /// 单独成类而不是塞进 <see cref="EnemySystem"/>：这个系统只关心「贴着玩家的敌人怎么造成伤害」，
    /// 和移动/Boss 阶段无关，改接触手感（冷却、伤害系数、击退）时不用碰移动逻辑。
    /// </summary>
    public sealed class ContactSystem
    {
        /// <summary>同一只敌人两次接触伤害之间的间隔。</summary>
        public const float ContactInterval = 0.5f;

        private readonly System.Collections.Generic.List<EnemyUnit> _buffer = new System.Collections.Generic.List<EnemyUnit>(256);

        public void Tick(float deltaTime, IEnemyContext context, System.Collections.Generic.IReadOnlyList<EnemyUnit> enemies)
        {
            if (context == null)
            {
                return;
            }

            var player = context.Player;
            if (player == null)
            {
                return;
            }

            var playerPosition = (Vector2)player.transform.position;

            // 与移动一样先拷一份：接触伤害可能触发爆炸/分裂，遍历中会增删敌人。
            _buffer.Clear();
            for (var i = 0; i < enemies.Count; i++)
            {
                _buffer.Add(enemies[i]);
            }

            for (var i = 0; i < _buffer.Count; i++)
            {
                var enemy = _buffer[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var state = enemy.State;
                if (context.ElapsedSeconds < state.NextContactTime)
                {
                    continue;
                }

                if (Vector2.Distance(state.Position, playerPosition) > state.Radius + PlayerUnit.Radius)
                {
                    continue;
                }

                state.NextContactTime = context.ElapsedSeconds + ContactInterval;
                context.DamagePlayer(state.Damage);

                if (state.Def.SlowOnHit > 0f)
                {
                    context.ApplySlowToPlayer(state.Def.SlowOnHit);
                }
            }
        }
    }
}
