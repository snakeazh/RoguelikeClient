using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人规则（P3-1b 第 4 步）：行为调度 → 位移 → Boss 二阶段。
    ///
    /// 只读 <see cref="EnemyUnit.State"/> 与行为注册表，不碰渲染；<see cref="EnemyUnit"/> 退化成表现层。
    /// 接触伤害在 <see cref="ContactSystem"/>，拆开是因为它关心的是「和玩家的距离」，和移动是两件事。
    /// </summary>
    public sealed class EnemySystem
    {
        /// <summary>Boss 进入二阶段后的加速倍率。</summary>
        public const float BossEnrageSpeedScale = 1.35f;

        /// <summary>Boss 二阶段弹幕的间隔与数量。</summary>
        public const float BossBurstInterval = 3f;

        public const int BossBurstCount = 12;

        /// <summary>冻结（沙漏）时整段跳过 —— 冻结是「敌人全部不动」，包括行为和 Boss 弹幕。</summary>
        public void Tick(float deltaTime, IEnemyContext context, System.Collections.Generic.IReadOnlyList<EnemyUnit> enemies, bool frozen)
        {
            if (frozen || context == null)
            {
                return;
            }

            var player = context.Player;
            if (player == null)
            {
                return;
            }

            // 先拷一份：行为里可能连锁击杀/分裂/爆炸，从而在遍历中增删 _enemies。
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

                TickOne(enemy, player, deltaTime, context);
            }
        }

        private readonly System.Collections.Generic.List<EnemyUnit> _buffer = new System.Collections.Generic.List<EnemyUnit>(256);

        private static void TickOne(EnemyUnit enemy, PlayerUnit player, float deltaTime, IEnemyContext context)
        {
            var state = enemy.State;

            // 顺序与拆分前一致：先吃掉上一帧的击退位移，再让行为读新位置，最后统一应用移动。
            state.TickKnockback(deltaTime);

            EnemyBehaviors.Get(state.Def.Behavior).Tick(enemy, player, deltaTime);

            var move = enemy.TakeMoveIntent();
            if (move.sqrMagnitude > 0.0001f)
            {
                var next = state.Position + move * (state.SpeedAt(context.ElapsedSeconds) * deltaTime);
                state.Position = BattleMap.ClampToWalkable(next, state.Radius);
            }

            if (state.IsBoss)
            {
                TickBoss(state, deltaTime, context);
            }
        }

        /// <summary>Boss 二阶段：50% 血量后加速 + 每 3 秒一圈弹幕，提示只播一次。</summary>
        private static void TickBoss(EnemyState state, float deltaTime, IEnemyContext context)
        {
            if (state.Hp > state.MaxHp * 0.5f)
            {
                return;
            }

            state.Speed = state.Def.Speed * BossEnrageSpeedScale;
            state.AttackTimer -= deltaTime;
            if (state.AttackTimer > 0f)
            {
                return;
            }

            state.AttackTimer = BossBurstInterval;
            for (var i = 0; i < BossBurstCount; i++)
            {
                var angle = i * Mathf.PI * 2f / BossBurstCount + context.ElapsedSeconds;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                context.SpawnEnemyProjectile(state.Position, direction, state.Def.ProjectileSpeed, state.Damage * 0.6f);
            }

            if (state.PhaseTwoAnnounced)
            {
                return;
            }

            context.Toast("Boss 进入第二阶段！");
            state.PhaseTwoAnnounced = true;
        }
    }
}
