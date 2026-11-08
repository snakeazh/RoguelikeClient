using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人的「纯逻辑状态」（P3-1）：不含任何 Unity 对象，可以直接 new、可以在没有场景的情况下推进。
    ///
    /// 当前阶段（P3-1a）由 <see cref="EnemyUnit"/> 持有并代理，表现层仍然是 MonoBehaviour；
    /// 下一步（P3-1b）会把位置改为状态权威、单位只做同步，这样同一份状态就能在 headless 批量回归里跑。
    /// </summary>
    public sealed class EnemyState
    {
        public EnemyDef Def;
        public EliteAffix Affix;
        public bool IsElite;
        public bool IsBoss;

        public float MaxHp;
        public float Hp;
        public float Speed;
        public float Damage;
        public float Radius;

        /// <summary>世界坐标。P3-1a 期间由单位在移动后回写，P3-1b 起改为唯一真源。</summary>
        public Vector2 Position;

        public float SlowUntil;
        public bool FuseStarted;
        public float FuseLeft;
        public float AttackTimer;
        public float WanderAngle;

        /// <summary>接触伤害冷却（0.5 秒一跳）。</summary>
        public float NextContactTime;

        /// <summary>回旋刃的重复命中冷却。</summary>
        public float NextBladeHitTime;

        public bool PhaseTwoAnnounced;

        public bool IsDead => Hp <= 0f;

        /// <summary>
        /// 精英词缀：只改数值，不改行为。放大血量时同步把当前血量补满（生成时就是满血）。
        /// </summary>
        public void ApplyAffix()
        {
            switch (Affix)
            {
                case EliteAffix.Swift:
                    Speed *= 1.6f;
                    break;
                case EliteAffix.Giant:
                    Radius *= 1.6f;
                    MaxHp *= 2f;
                    Hp = MaxHp;
                    Damage *= 1.4f;
                    break;
                case EliteAffix.Shell:
                    MaxHp *= 1.8f;
                    Hp = MaxHp;
                    Speed *= 0.85f;
                    break;
                case EliteAffix.Frenzy:
                    Speed *= 1.25f;
                    Damage *= 1.25f;
                    break;
                case EliteAffix.Split:
                    MaxHp *= 0.9f;
                    Hp = MaxHp;
                    break;
            }
        }

        /// <summary>扣血，返回是否本次死亡。</summary>
        public bool TakeDamage(float amount)
        {
            if (IsDead)
            {
                return false;
            }

            Hp -= amount;
            if (Hp <= 0f)
            {
                Hp = 0f;
                return true;
            }

            return false;
        }
    }
}
