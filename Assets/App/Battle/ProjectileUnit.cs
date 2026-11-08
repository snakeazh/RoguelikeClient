using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体的表现层（P3-1）：把 <see cref="ProjectileState"/> 同步成 Transform 与精灵朝向。
    /// 推进、追踪、命中的规则在 <see cref="ProjectileSystem"/>，这里不写任何战斗逻辑。
    /// </summary>
    public sealed class ProjectileUnit : MonoBehaviour
    {
        private SpriteRenderer _renderer;

        /// <summary>逻辑状态：只有 <see cref="ProjectileSystem"/> 写它，这里只读。</summary>
        public ProjectileState State { get; private set; }

        public bool FromPlayer => State.FromPlayer;

        /// <summary>当前飞行速度（调试 / 朝向校验用）。</summary>
        public Vector2 Velocity => State.Velocity;

        public void Init(ProjectileState state, string spriteKey)
        {
            State = state;

            var key = string.IsNullOrEmpty(spriteKey) ? BattleVisuals.ProjectileFallback : spriteKey;
            _renderer = BattleVisuals.Body(
                transform, key, Color.white, state.Radius * 2f, BattleSort.Projectile, PlaceholderArt.Circle);

            name = state.FromPlayer ? "PlayerProjectile" : "EnemyProjectile";
            SyncFromState();
        }

        /// <summary>逻辑帧结束后调用：位置 + 朝向。</summary>
        public void SyncFromState()
        {
            if (State == null)
            {
                return;
            }

            transform.position = State.Position;

            // 所有弹体贴图都是「朝上（+Y）」画的，所以角度要减 90°；SpinAngle 是额外自转
            if (State.Velocity.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var angle = Mathf.Atan2(State.Velocity.y, State.Velocity.x) * Mathf.Rad2Deg - 90f + State.SpinAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
