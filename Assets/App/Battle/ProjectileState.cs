using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体的纯逻辑状态（P3-1）。位置、速度、伤害、穿透、溅射、存活时间、追踪目标。
    /// 追踪目标是状态引用而不是 MonoBehaviour，headless 里也能直接跑。
    /// </summary>
    public sealed class ProjectileState
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public DamageInfo Damage;
        public float Radius;
        public int Pierce;
        public float Splash;
        public float Life;
        public float Spin;
        public float SpinAngle;
        public bool FromPlayer;

        /// <summary>追踪目标；null 表示直线飞行。</summary>
        public EnemyState HomingTarget;

        /// <summary>已经命中的敌人，避免同一发弹体反复打同一只。</summary>
        public readonly HashSet<EnemyState> HitEnemies = new HashSet<EnemyState>();

        /// <summary>本帧结算后是否要销毁。</summary>
        public bool Dead;
    }
}
