using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 魔法炮 / 湮灭炮的随身炮台：
    /// 贴在玩家侧面（垂直于开火方向，所以永远「在身边」而不是挡在身前），
    /// 炮管自动转向最近的敌人；冷却期间炮口聚能发光，蓄满即开火，开火有后座和炮口闪光。
    /// 由 <see cref="WeaponRuntime"/> 在拥有 <c>Mount = 1</c> 的武器时创建。
    /// </summary>
    public sealed class CannonRig : MonoBehaviour
    {
        private const float BarrelLength = 0.3f;
        private const float MuzzleOffset = 0.6f;
        private const float BarrelDiameter = 0.95f;
        private const int BaseOrder = BattleSort.CannonBase;
        private const int BarrelOrder = BattleSort.CannonBarrel;

        private Transform _barrelPivot;
        private SpriteRenderer _barrel;
        private SpriteRenderer _charge;
        private SpriteRenderer _chargeRing;
        private SpriteRenderer _flash;

        private float _angle = 90f;
        private float _chargeValue;
        private float _recoil;
        private float _flashTimer;

        /// <summary>当前蓄能进度（0..1），调试用。</summary>
        public float Charge => _chargeValue;

        /// <summary>
        /// 摆到逻辑层算好的位姿上。这里刻意不做任何平滑：
        /// 炮口位置必须与规则层完全一致，否则弹体出生点会跟着帧率漂。
        /// </summary>
        public void SetPose(Vector2 worldPosition, Vector2 direction)
        {
            transform.position = worldPosition;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            if (_barrelPivot != null)
            {
                _barrelPivot.rotation = Quaternion.Euler(0f, 0f, _angle);
            }
        }

        public static CannonRig Create(Transform player)
        {
            var go = new GameObject("CannonRig");
            go.transform.SetParent(player, false);

            var rig = go.AddComponent<CannonRig>();
            rig.Build();
            return rig;
        }

        private void Build()
        {
            var baseSprite = BattleVisuals.GetSprite(BattleVisuals.CannonBase);
            PlaceholderArt.CreateRenderer(
                "CannonBase",
                transform,
                baseSprite != null ? baseSprite : PlaceholderArt.Ring,
                new Color(0.62f, 0.7f, 0.85f, 0.75f),
                0.52f,
                BaseOrder);

            var pivot = new GameObject("BarrelPivot");
            pivot.transform.SetParent(transform, false);
            _barrelPivot = pivot.transform;

            var barrelSprite = BattleVisuals.GetSprite(BattleVisuals.Cannon);
            // 贴图里炮身约占高度的中间六成，抬高半个炮身，旋转时就绕炮尾转
            _barrel = PlaceholderArt.CreateRenderer(
                "Barrel",
                _barrelPivot,
                barrelSprite != null ? barrelSprite : PlaceholderArt.Square,
                Color.white,
                BarrelDiameter,
                BarrelOrder,
                new Vector3(0f, BarrelLength, 0f));

            // 蓄能光球：贴在炮口，随蓄能进度变大变亮
            _charge = PlaceholderArt.CreateRenderer(
                "Charge",
                _barrelPivot,
                TransientFx.SoftCircle(),
                new Color(0.4f, 0.75f, 1f, 0f),
                0.3f,
                BattleSort.CannonCharge,
                new Vector3(0f, MuzzleOffset, 0f));
            _charge.enabled = false;

            // 蓄能环：从外圈往里收，收得越紧表示快开炮了
            _chargeRing = PlaceholderArt.CreateRenderer(
                "ChargeRing",
                _barrelPivot,
                PlaceholderArt.Ring,
                new Color(0.55f, 0.85f, 1f, 0f),
                1f,
                BattleSort.CannonCharge,
                new Vector3(0f, MuzzleOffset, 0f));
            _chargeRing.enabled = false;

            // 炮口闪光：开火瞬间的一帧亮斑
            _flash = PlaceholderArt.CreateRenderer(
                "MuzzleFlash",
                _barrelPivot,
                TransientFx.SoftCircle(),
                new Color(1f, 1f, 1f, 0f),
                0.75f,
                BarrelOrder + 2,
                new Vector3(0f, MuzzleOffset, 0f));
            _flash.enabled = false;
        }

        /// <summary>0..1 的蓄能进度：炮口光球由小到大、由暗到亮，蓄满前会越来越亮地闪。</summary>
        public void SetCharge(float value)
        {
            _chargeValue = Mathf.Clamp01(value);
        }

        /// <summary>开火：后座 + 炮口闪光。</summary>
        public void Kick()
        {
            _recoil = 1f;
            _flashTimer = 0.1f;
        }

        private void Update()
        {
            var dt = Time.deltaTime;

            // 后座：炮管沿自身轴向往回退一点，再弹回来
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 7f);
            if (_barrel != null)
            {
                var home = new Vector3(0f, BarrelLength, 0f);
                _barrel.transform.localPosition = home - new Vector3(0f, _recoil * 0.18f, 0f);
            }

            // 蓄能表现：炮口光球由小到大、由暗到亮，外圈能量环向炮口收缩
            if (_charge != null)
            {
                var v = _chargeValue;
                var active = v > 0.03f;
                _charge.enabled = active;
                if (_chargeRing != null)
                {
                    _chargeRing.enabled = active;
                }

                if (active)
                {
                    var flicker = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 26f);
                    var size = Mathf.Lerp(0.22f, 0.82f, v) * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 18f));
                    _charge.transform.localScale = Vector3.one * size;

                    var color = Color.Lerp(new Color(0.45f, 0.8f, 1f, 0.55f), new Color(1f, 1f, 1f, 1f), v * v);
                    color.a *= flicker;
                    _charge.color = color;

                    // 快蓄满时炮口轻微抖动，给"压不住要炸了"的感觉
                    if (v > 0.7f)
                    {
                        var tremor = (v - 0.7f) / 0.3f * 0.035f;
                        _charge.transform.localPosition = new Vector3(
                            Random.Range(-tremor, tremor),
                            MuzzleOffset + Random.Range(-tremor, tremor),
                            0f);
                    }
                    else
                    {
                        _charge.transform.localPosition = new Vector3(0f, MuzzleOffset, 0f);
                    }

                    if (_chargeRing != null)
                    {
                        // 1 → 0 蓄满期间从 1.15 收到 0.3
                        var ringScale = Mathf.Lerp(1.15f, 0.3f, v);
                        _chargeRing.transform.localScale = Vector3.one * ringScale;
                        var ringColor = new Color(0.55f, 0.85f, 1f, 0.18f + 0.55f * v * v);
                        _chargeRing.color = ringColor;
                    }

                    if (_barrel != null)
                    {
                        _barrel.color = Color.Lerp(Color.white, new Color(0.72f, 0.95f, 1f), v * 0.85f);
                    }
                }
            }

            // 炮口闪光
            if (_flash != null)
            {
                _flashTimer -= dt;
                var on = _flashTimer > 0f;
                _flash.enabled = on;
                if (on)
                {
                    var t = Mathf.Clamp01(_flashTimer / 0.1f);
                    _flash.color = new Color(1f, 1f, 1f, t * 0.9f);
                    _flash.transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.6f, t);
                }
            }
        }
    }
}
