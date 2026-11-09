using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 表现层的武器视图工厂：全项目里唯一「为武器创建 GameObject / 挂炮台 / 播特效」的地方。
    ///
    /// 由 <see cref="BattleRunner"/> 构造并注入 <see cref="WeaponRuntime"/>；规则层的四个行为只认
    /// <see cref="IWeaponViews"/>，所以把它们换掉不会影响任何判定与随机流。
    /// </summary>
    public sealed class UnityWeaponViews : IWeaponViews
    {
        private const float PoisonAlpha = 0.42f;
        private const float PoisonSpinDegreesPerSecond = 18f;

        private readonly Transform _root;
        private readonly PlayerUnit _player;

        public UnityWeaponViews(Transform root, PlayerUnit player)
        {
            _root = root;
            _player = player;
        }

        public IBombView CreateBomb(Vector2 position, float scale)
        {
            var go = new GameObject("Bomb");
            go.transform.SetParent(_root, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            BattleVisuals.Body(
                go.transform,
                "Battle/Sprites/proj_bomb",
                Color.white,
                scale,
                BattleSort.Bomb,
                PlaceholderArt.Square);
            return new BombView(go.transform);
        }

        public IPoisonView CreatePoison(Vector2 position, float radius)
        {
            var root = new GameObject("PoisonCloud");
            root.transform.SetParent(_root, false);
            root.transform.position = new Vector3(position.x, position.y, 0f);

            // 两层：实心圆做「雾团」，外圈用光环精灵做边界，半透明叠起来像一团绿雾。
            var body = CreateLayer(root.transform, "Body", PlaceholderArt.Circle, BattleSort.Fx + 1);
            var ring = CreateLayer(
                root.transform,
                "Ring",
                BattleVisuals.GetSprite(BattleVisuals.Aura) ?? PlaceholderArt.Ring,
                BattleSort.Fx + 2);

            var scale = radius * 2f;
            root.transform.localScale = new Vector3(scale, scale, 1f);
            return new PoisonView(root.transform, body, ring);
        }

        public IBladeView CreateBlade(float size)
        {
            var go = new GameObject("Blade");
            go.transform.SetParent(_root, false);
            BattleVisuals.Body(
                go.transform,
                BattleVisuals.Blade,
                Color.white,
                size * 3.0f,
                BattleSort.Blade,
                PlaceholderArt.Square);
            return new BladeView(go.transform);
        }

        public ICannonView CreateCannon()
        {
            return _player != null ? new CannonView(CannonRig.Create(_player.transform)) : new CannonView(null);
        }

        public void Flash(Vector2 position, float diameter, Color color, float life)
        {
            TransientFx.Flash(position, diameter, color, life);
        }

        public void Fan(Vector2 origin, float angleDegrees, float radius, float spreadDegrees, Color color, float life)
        {
            TransientFx.Fan(origin, angleDegrees, radius, spreadDegrees, color, life);
        }

        public void Bolt(Vector2 from, Vector2 to, Color color, float life)
        {
            LightningFx.Bolt(from, to, color, life);
        }

        public void ShowAura(bool visible, float radius)
        {
            if (_player == null)
            {
                return;
            }

            _player.SetAuraVisible(visible);
            _player.SetAuraRadius(radius);
        }

        private static SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = new Color(0.4f, 0.95f, 0.35f, 0f);
            return renderer;
        }

        private sealed class BombView : IBombView
        {
            private readonly Transform _transform;

            public BombView(Transform transform)
            {
                _transform = transform;
            }

            public Vector2 Position => _transform != null ? (Vector2)_transform.position : Vector2.zero;

            public void Pulse(float elapsedSeconds)
            {
                if (_transform == null)
                {
                    return;
                }

                var pulse = 1f + Mathf.Sin(elapsedSeconds * 26f) * 0.16f;
                _transform.localScale = Vector3.one * pulse;
            }

            public void Dispose()
            {
                if (_transform != null)
                {
                    Object.Destroy(_transform.gameObject);
                }
            }
        }

        private sealed class PoisonView : IPoisonView
        {
            private readonly Transform _root;
            private readonly SpriteRenderer _body;
            private readonly SpriteRenderer _ring;

            public PoisonView(Transform root, SpriteRenderer body, SpriteRenderer ring)
            {
                _root = root;
                _body = body;
                _ring = ring;
            }

            public void Tick(float elapsedSeconds, float remainRatio, float deltaTime)
            {
                if (_root == null)
                {
                    return;
                }

                // 越接近消散越淡，并轻微呼吸，读起来像「雾」。
                var breathe = 0.9f + 0.1f * Mathf.Sin(elapsedSeconds * 6f);
                var fade = Mathf.Min(1f, remainRatio * 2f) * breathe;
                Tint(_body, new Color(0.34f, 0.92f, 0.30f, PoisonAlpha * 0.62f * fade));
                Tint(_ring, new Color(0.45f, 1f, 0.40f, PoisonAlpha * fade));
                _root.Rotate(0f, 0f, deltaTime * PoisonSpinDegreesPerSecond);
            }

            public void Dispose()
            {
                if (_root != null)
                {
                    Object.Destroy(_root.gameObject);
                }
            }

            private static void Tint(SpriteRenderer renderer, Color color)
            {
                if (renderer != null)
                {
                    renderer.color = color;
                }
            }
        }

        private sealed class BladeView : IBladeView
        {
            private readonly Transform _transform;

            public BladeView(Transform transform)
            {
                _transform = transform;
            }

            public void SetPose(Vector2 position, float degrees)
            {
                if (_transform == null)
                {
                    return;
                }

                _transform.position = new Vector3(position.x, position.y, 0f);
                _transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            }

            public void Dispose()
            {
                if (_transform != null)
                {
                    Object.Destroy(_transform.gameObject);
                }
            }
        }

        private sealed class CannonView : ICannonView
        {
            private readonly CannonRig _rig;

            public CannonView(CannonRig rig)
            {
                _rig = rig;
            }

            public Vector2 MuzzlePosition => _rig != null ? _rig.MuzzlePosition : Vector2.zero;

            public Vector2 BarrelDirection => _rig != null ? _rig.BarrelDirection : Vector2.up;

            public void AimAt(Vector2 direction, float deltaTime)
            {
                if (_rig != null)
                {
                    _rig.AimAt(direction, deltaTime);
                }
            }

            public void SetCharge(float value)
            {
                if (_rig != null)
                {
                    _rig.SetCharge(value);
                }
            }

            public void Kick()
            {
                if (_rig != null)
                {
                    _rig.Kick();
                }
            }

            public void Dispose()
            {
                if (_rig != null)
                {
                    Object.Destroy(_rig.gameObject);
                }
            }
        }
    }
}
