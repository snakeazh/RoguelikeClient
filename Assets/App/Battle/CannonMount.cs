using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 随身炮台（魔法炮 / 湮灭炮，<c>WeaponDef.Mount == 1</c>）的瞄准逻辑。
    ///
    /// 炮台是"一把武器一件"的东西，但场上只会有一台，所以单独抽出来由 <see cref="WeaponRuntime"/> 持有：
    /// 每帧转向最近的目标、同步蓄能进度；开火逻辑仍留在 <see cref="ShooterBehavior"/> 里。
    ///
    /// 关键约定：**炮位与炮口都是纯逻辑算出来的**（玩家位置 + 侧向偏移 + 炮管长度），
    /// 视图只是照着这个位姿画。否则弹体出生点会依赖视图的帧率相关插值，
    /// 同种子两局就会打出不同的弹道（这是之前确定性崩掉的真正原因）。
    /// </summary>
    public sealed class CannonMount
    {
        /// <summary>炮口与目标夹角小于这个度数才开火（用真实的炮管角度，不是目标角度），避免"甩炮"。</summary>
        public const float AlignmentDegrees = 14f;

        /// <summary>炮口对不准时最多等这么久（秒），超时就先打出去。</summary>
        public const float FireGraceSeconds = 0.6f;

        /// <summary>炮台本体离玩家的侧向距离（垂直于开火方向，所以永远不挡弹道）。</summary>
        private const float SideDistance = 1.02f;

        /// <summary>炮管长度：炮口的逻辑位置 = 炮台中心 + 朝向 * 该值。</summary>
        private const float MuzzleOffset = 0.6f;

        // 转向手感：临界阻尼追角度。固定步长下这两组常数是完全确定的。
        private const float AngleSmoothTime = 0.22f;
        private const float MaxTurnSpeed = 200f;
        private const float MoveSmoothTime = 0.26f;
        private const float MaxMoveSpeed = 14f;

        private readonly WeaponRuntime _host;
        private ICannonView _view;
        private EnemyState _target;

        private Vector2 _direction = Vector2.up;
        private float _angleVelocity;
        private Vector2 _bodyOffset;
        private Vector2 _bodyVelocity;

        public CannonMount(WeaponRuntime host)
        {
            _host = host;
        }

        public bool Exists => _view != null;

        /// <summary>炮台本体的逻辑位置（玩家侧面）。</summary>
        public Vector2 BodyPosition => _host.PlayerPosition + _bodyOffset;

        public Vector2 MuzzlePosition => BodyPosition + _direction * MuzzleOffset;

        public Vector2 BarrelDirection => _direction;

        /// <summary>炮口是否已经对准某个点。没有炮台时永远算对准（普通武器不受影响）。</summary>
        public bool IsAlignedWith(Vector2 point)
        {
            if (_view == null)
            {
                return true;
            }

            return Vector2.Angle(_direction, point - MuzzlePosition) <= AlignmentDegrees;
        }

        public void Kick()
        {
            _view?.Kick();
        }

        /// <summary>
        /// 每帧维护：有 Mount 武器就造一台并转向最近的敌人、同步蓄能；没有就拆掉。
        /// 目标带一点"粘性"，否则两只距离相近的敌人会让炮管来回摆动。
        /// </summary>
        public void Tick(float deltaTime, WeaponInstance cannonWeapon)
        {
            if (cannonWeapon == null)
            {
                Dispose();
                return;
            }

            var origin = _host.PlayerPosition;
            if (!IsTargetValid(_target, cannonWeapon.Def.Range * 1.25f))
            {
                _target = _host.FindNearestEnemy(cannonWeapon.Def.Range);
            }

            var desired = _target != null ? (_target.Position - origin).normalized : _direction;
            UpdatePose(desired, deltaTime);

            if (_view == null)
            {
                _view = _host.Views.CreateCannon();
            }

            _view.SetPose(BodyPosition, _direction);

            if (_target == null)
            {
                // 没有目标就别亮着炮口，避免"空蓄能"的假象
                _view.SetCharge(0f);
                return;
            }

            var cooldown = _host.CooldownOf(cannonWeapon);
            var charge = cooldown <= 0.0001f
                ? 0f
                : 1f - Mathf.Clamp01(cannonWeapon.CooldownTimer / cooldown);
            _view.SetCharge(charge);
        }

        /// <summary>
        /// 逻辑侧位姿推进：朝向用临界阻尼角速度，炮台本体用 SmoothDamp 跟到玩家侧后方。
        /// 两者都吃固定步长的 deltaTime，所以同一颗种子下轨迹可复现。
        /// </summary>
        private void UpdatePose(Vector2 desiredDirection, float deltaTime)
        {
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                var targetAngle = Mathf.Atan2(desiredDirection.y, desiredDirection.x) * Mathf.Rad2Deg;
                var angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;

                if (deltaTime <= 0f)
                {
                    angle = targetAngle;
                    _angleVelocity = 0f;
                }
                else
                {
                    angle = Mathf.SmoothDampAngle(
                        angle, targetAngle, ref _angleVelocity, AngleSmoothTime, MaxTurnSpeed, deltaTime);
                }

                var radians = angle * Mathf.Deg2Rad;
                _direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }

            // 站在开火线的侧面，永远不挡在弹道上。
            var side = new Vector2(-_direction.y, _direction.x) * SideDistance + new Vector2(0f, 0.08f);
            if (deltaTime <= 0f)
            {
                _bodyOffset = side;
                _bodyVelocity = Vector2.zero;
            }
            else
            {
                _bodyOffset = Vector2.SmoothDamp(
                    _bodyOffset, side, ref _bodyVelocity, MoveSmoothTime, MaxMoveSpeed, deltaTime);
            }
        }

        public void Dispose()
        {
            if (_view == null)
            {
                return;
            }

            _view.Dispose();
            _view = null;
            _target = null;
        }

        private bool IsTargetValid(EnemyState target, float range)
        {
            if (target == null)
            {
                return false;
            }

            return Vector2.Distance(target.Position, _host.PlayerPosition) <= range;
        }
    }
}
