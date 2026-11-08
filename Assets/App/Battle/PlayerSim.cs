using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 玩家移动规则（P3-1）。只改 <see cref="PlayerState"/>：计时递减、冲刺/普通位移、地图夹取、朝向角。
    /// 不读输入设备（<see cref="IUnitInput"/> 由外部传入）、不碰精灵和 Transform。
    /// </summary>
    public sealed class PlayerSim
    {
        public const float Radius = 0.4f;

        /// <summary>冲刺位移距离（约 2.5 身位）。</summary>
        public const float DashDistance = 2.5f;

        public const float DashDuration = 0.2f;
        public const float DashInvulnExtra = 0.1f;
        public const float DashCooldown = 3f;

        public void Tick(PlayerState state, BattleSession session, IUnitInput input, float deltaTime, Rect walkable)
        {
            input?.Update(deltaTime);
            var moveDir = input != null ? input.Move : Vector2.zero;
            var dashRequested = input != null && input.DashRequested;

            if (state.DashCooldownLeft > 0f)
            {
                state.DashCooldownLeft -= deltaTime;
            }

            if (state.SlowLeft > 0f)
            {
                state.SlowLeft -= deltaTime;
            }

            if (state.InvulnerableLeft > 0f)
            {
                state.InvulnerableLeft -= deltaTime;
            }

            if (state.IsDashing)
            {
                state.Position += state.DashDirection * (DashDistance / DashDuration) * deltaTime;
                state.DashTimeLeft -= deltaTime;
            }
            else
            {
                var speed = session.MoveSpeed * state.SlowMultiplier;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    state.Position += moveDir.normalized * (speed * deltaTime);
                }

                if (dashRequested && state.DashCooldownLeft <= 0f)
                {
                    StartDash(state, moveDir);
                }
            }

            state.Position = ClampToWalkable(state.Position, walkable, Radius);
            UpdateFacing(state, deltaTime, moveDir);
        }

        private static void StartDash(PlayerState state, Vector2 moveDir)
        {
            state.DashDirection = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Vector2.up;
            state.DashTimeLeft = DashDuration;
            state.InvulnerableLeft = DashDuration + DashInvulnExtra;
            state.DashCooldownLeft = DashCooldown;
        }

        private static void UpdateFacing(PlayerState state, float deltaTime, Vector2 moveDir)
        {
            var direction = state.IsDashing ? state.DashDirection : moveDir;
            if (direction.sqrMagnitude > 0.0001f)
            {
                state.FacingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            }
        }

        private static Vector2 ClampToWalkable(Vector2 position, Rect walkable, float radius)
        {
            position.x = Mathf.Clamp(position.x, walkable.xMin + radius, walkable.xMax - radius);
            position.y = Mathf.Clamp(position.y, walkable.yMin + radius, walkable.yMax - radius);
            return position;
        }
    }
}
