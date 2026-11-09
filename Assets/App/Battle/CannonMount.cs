using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 随身炮台（魔法炮 / 湮灭炮，<c>WeaponDef.Mount == 1</c>）的瞄准逻辑。
    ///
    /// 炮台是"一把武器一件"的东西，但场上只会有一台，所以单独抽出来由 <see cref="WeaponRuntime"/> 持有：
    /// 每帧转向最近的目标、同步蓄能进度；开火逻辑仍留在 <see cref="ShooterBehavior"/> 里。
    /// 炮台长什么样、怎么转由 <see cref="ICannonView"/> 负责，这里只算角度。
    /// </summary>
    public sealed class CannonMount
    {
        /// <summary>炮口与目标夹角小于这个度数才开火（用真实的炮管角度，不是目标角度），避免"甩炮"。</summary>
        public const float AlignmentDegrees = 14f;

        /// <summary>炮口对不准时最多等这么久（秒），超时就先打出去。</summary>
        public const float FireGraceSeconds = 0.6f;

        private readonly WeaponRuntime _host;
        private ICannonView _view;
        private EnemyState _target;

        public CannonMount(WeaponRuntime host)
        {
            _host = host;
        }

        public bool Exists => _view != null;

        public Vector2 MuzzlePosition => _view != null ? _view.MuzzlePosition : _host.PlayerPosition;

        public Vector2 BarrelDirection => _view != null ? _view.BarrelDirection : Vector2.up;

        /// <summary>炮口是否已经对准某个点。没有炮台时永远算对准（普通武器不受影响）。</summary>
        public bool IsAlignedWith(Vector2 point)
        {
            if (_view == null)
            {
                return true;
            }

            return Vector2.Angle(_view.BarrelDirection, point - _view.MuzzlePosition) <= AlignmentDegrees;
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

            if (_view == null)
            {
                _view = _host.Views.CreateCannon();
            }

            var origin = _host.PlayerPosition;
            var range = cannonWeapon.Def.Range;
            if (!IsTargetValid(_target, range * 1.25f))
            {
                _target = _host.FindNearestEnemy(range);
            }

            var aim = _target != null
                ? (_target.Position - origin).normalized
                : _view.BarrelDirection;
            _view.AimAt(aim, deltaTime);

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
