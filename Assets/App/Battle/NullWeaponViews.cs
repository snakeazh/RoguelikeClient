using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 什么都不画的武器视图：headless 回归与单元测试用。
    ///
    /// 规则层的行为代码与表现层完全一样地跑（冷却、索敌、伤害、引信计时都不受影响），
    /// 只是没有任何 GameObject 被创建 —— 这正是「规则不依赖渲染」的验证方式。
    /// </summary>
    public sealed class NullWeaponViews : IWeaponViews
    {
        public static readonly NullWeaponViews Instance = new NullWeaponViews();

        private NullWeaponViews()
        {
        }

        public IBombView CreateBomb(Vector2 position, float scale) => new Bomb(position);

        public IPoisonView CreatePoison(Vector2 position, float radius) => new Poison();

        public IBladeView CreateBlade(float size) => new Blade();

        public ICannonView CreateCannon() => new Cannon();

        public void Flash(Vector2 position, float diameter, Color color, float life)
        {
        }

        public void Fan(Vector2 origin, float angleDegrees, float radius, float spreadDegrees, Color color, float life)
        {
        }

        public void Bolt(Vector2 from, Vector2 to, Color color, float life)
        {
        }

        public void ShowAura(bool visible, float radius)
        {
        }

        private sealed class Bomb : IBombView
        {
            public Bomb(Vector2 position)
            {
                Position = position;
            }

            public Vector2 Position { get; }

            public void Pulse(float elapsedSeconds)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class Poison : IPoisonView
        {
            public void Tick(float elapsedSeconds, float remainRatio, float deltaTime)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class Blade : IBladeView
        {
            public void SetPose(Vector2 position, float degrees)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class Cannon : ICannonView
        {
            public Vector2 MuzzlePosition => Vector2.zero;

            public Vector2 BarrelDirection => Vector2.up;

            public void AimAt(Vector2 direction, float deltaTime)
            {
            }

            public void SetCharge(float value)
            {
            }

            public void Kick()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
