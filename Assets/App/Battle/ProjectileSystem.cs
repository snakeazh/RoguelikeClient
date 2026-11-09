using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体规则（P3-1）：推进、追踪、命中判定、穿透、溅射。
    /// 只改 <see cref="ProjectileState"/>，通过 <see cref="IBattleContext"/> 查询敌人/玩家并结算伤害。
    /// 表现（精灵、旋转）由 <see cref="ProjectileUnit"/> 从 State 同步。
    /// </summary>
    public sealed class ProjectileSystem
    {
        // 命中会击杀敌人（进而增删 runner 的敌人列表），所以先拷一份再遍历。
        private static readonly List<EnemyState> s_targets = new List<EnemyState>(512);

        private readonly List<ProjectileState> _states = new List<ProjectileState>(256);

        public IReadOnlyList<ProjectileState> States => _states;

        public ProjectileState SpawnPlayer(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            float spin = 0f,
            EnemyState homingTarget = null)
        {
            var state = new ProjectileState
            {
                Position = position,
                Velocity = direction.normalized * speed,
                Damage = damage,
                Radius = Mathf.Max(0.05f, radius),
                Pierce = pierce,
                Splash = splash,
                Life = life,
                Spin = spin,
                FromPlayer = true,
                HomingTarget = homingTarget
            };

            _states.Add(state);
            return state;
        }

        public ProjectileState SpawnEnemy(Vector2 position, Vector2 direction, float speed, float damage, float radius)
        {
            var state = new ProjectileState
            {
                Position = position,
                Velocity = direction.normalized * speed,
                Damage = DamageInfo.Plain(damage),
                Radius = Mathf.Max(0.05f, radius),
                Pierce = 0,
                Splash = 0f,
                Life = 6f,
                Spin = 0f,
                FromPlayer = false
            };

            _states.Add(state);
            return state;
        }

        public void Tick(float deltaTime, IBattleContext context)
        {
            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                if (state == null || state.Dead)
                {
                    _states.RemoveAt(i);
                    continue;
                }

                TickOne(state, deltaTime, context);

                if (state.Dead)
                {
                    _states.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            _states.Clear();
        }

        private static void TickOne(ProjectileState state, float deltaTime, IBattleContext context)
        {
            state.Life -= deltaTime;
            if (state.Life <= 0f)
            {
                state.Dead = true;
                return;
            }

            // 追踪弹：朝目标缓慢转向
            var target = state.HomingTarget;
            if (target != null && !target.IsDead)
            {
                var desired = (target.Position - state.Position).normalized * state.Velocity.magnitude;
                state.Velocity = Vector2.Lerp(state.Velocity, desired, Mathf.Clamp01(deltaTime * 6f));
            }

            state.Position += state.Velocity * deltaTime;

            if (state.Spin != 0f)
            {
                state.SpinAngle += state.Spin * deltaTime;
            }

            if (!context.IsInsideArena(state.Position, 3f))
            {
                state.Dead = true;
                return;
            }

            if (state.FromPlayer)
            {
                HitEnemies(state, context);
            }
            else
            {
                HitPlayer(state, context);
            }
        }

        private static void HitEnemies(ProjectileState state, IBattleContext context)
        {
            s_targets.Clear();
            s_targets.AddRange(context.Enemies);

            foreach (var enemy in s_targets)
            {
                if (enemy == null || enemy.IsDead || state.HitEnemies.Contains(enemy))
                {
                    continue;
                }

                if (Vector2.Distance(state.Position, enemy.Position) > state.Radius + enemy.Radius)
                {
                    continue;
                }

                state.HitEnemies.Add(enemy);
                context.DamageEnemy(enemy, state.Damage);

                if (state.Splash > 0f)
                {
                    context.ExplodeAt(state.Position, state.Splash, state.Damage.Scaled(0.5f), false);
                }

                if (state.Pierce <= 0)
                {
                    state.Dead = true;
                    return;
                }

                state.Pierce--;
            }
        }

        private static void HitPlayer(ProjectileState state, IBattleContext context)
        {
            var player = context.Player;
            if (player == null)
            {
                state.Dead = true;
                return;
            }

            if (Vector2.Distance(state.Position, player.Position) > state.Radius + PlayerUnit.Radius)
            {
                return;
            }

            if (!player.IsInvulnerable)
            {
                context.DamagePlayer(state.Damage.Amount);
            }

            state.Dead = true;
        }

    }
}
