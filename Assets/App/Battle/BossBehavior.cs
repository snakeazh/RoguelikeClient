using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// Boss 二阶段（P2-4 收尾）：血量过半后加速、每 3 秒打一圈弹幕、提示只播一次。
    ///
    /// 做成装饰器而不是第七种行为：Boss 的走位本来就是 Tank/Shooter 那几套，
    /// 二阶段只是「在原有走位之上再叠一层」，这样 `Enemies.json` 不必为 Boss 单开新 `Behavior` 值，
    /// <see cref="EnemySystem"/> 里也不用再写 `if (state.IsBoss)`——判断谁是不是 Boss 只有注册表一处。
    /// </summary>
    public sealed class BossBehavior : IEnemyBehavior
    {
        /// <summary>二阶段加速倍率。</summary>
        public const float EnrageSpeedScale = 1.35f;

        /// <summary>二阶段弹幕的间隔（秒）与一圈的数量。</summary>
        public const float BurstInterval = 3f;

        public const int BurstCount = 12;

        private readonly IEnemyBehavior _movement;

        public BossBehavior(IEnemyBehavior movement)
        {
            _movement = movement;
        }

        public Vector2 Tick(EnemyState state, IEnemyContext context, float deltaTime)
        {
            // 先走位再结算二阶段：位移仍由 EnemySystem 统一应用，这里只负责弹幕与加速。
            var move = _movement.Tick(state, context, deltaTime);
            TickPhaseTwo(state, context, deltaTime);
            return move;
        }

        public void OnDeath(EnemyState state, IEnemyContext context)
        {
            _movement.OnDeath(state, context);
        }

        private static void TickPhaseTwo(EnemyState state, IEnemyContext context, float deltaTime)
        {
            if (state.Hp > state.MaxHp * 0.5f)
            {
                return;
            }

            state.Speed = state.Def.Speed * EnrageSpeedScale;
            state.AttackTimer -= deltaTime;
            if (state.AttackTimer > 0f)
            {
                return;
            }

            state.AttackTimer = BurstInterval;
            for (var i = 0; i < BurstCount; i++)
            {
                // 加一点相位偏移，每一圈的弹幕开口方向都在转，站着不动也会被削
                var angle = i * Mathf.PI * 2f / BurstCount + context.ElapsedSeconds;
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
