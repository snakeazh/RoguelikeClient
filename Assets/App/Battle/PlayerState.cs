using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 玩家的纯逻辑状态（P3-1）。只回答「人在哪、还能不能冲、有没有无敌」，
    /// 不持有任何渲染对象，也不读输入设备 —— 输入由 <see cref="PlayerSim"/> 从 <see cref="IUnitInput"/> 取。
    /// </summary>
    public sealed class PlayerState
    {
        public Vector2 Position;

        /// <summary>朝向角（贴图默认朝上，所以实际角度 = FacingAngle + 90°）。</summary>
        public float FacingAngle;

        public float DashTimeLeft;
        public Vector2 DashDirection = Vector2.up;
        public float DashCooldownLeft;
        public float InvulnerableLeft;
        public float SlowLeft;

        public bool IsDashing => DashTimeLeft > 0f;

        public bool IsInvulnerable => InvulnerableLeft > 0f;

        /// <summary>被鼻涕虫命中后短暂减速 50%。</summary>
        public float SlowMultiplier => SlowLeft > 0f ? 0.5f : 1f;

        /// <summary>角色当前面向（单位向量，+Y 朝上）。停下来以后保持最后一次朝向。</summary>
        public Vector2 FacingDirection
        {
            get
            {
                var radians = (FacingAngle + 90f) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        public void ApplySlow(float duration)
        {
            SlowLeft = Mathf.Max(SlowLeft, duration);
        }
    }
}
