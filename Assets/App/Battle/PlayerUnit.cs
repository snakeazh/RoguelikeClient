using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 玩家的表现层（P3-1 之后的职责）：把 <see cref="PlayerState"/> 同步到 Transform 与精灵。
    /// 移动/冲刺/无敌/减速的规则在 <see cref="PlayerSim"/>，这里不写任何战斗规则。
    /// 玩家不做任何攻击，伤害全部来自武器（WeaponSystem）。
    /// </summary>
    public sealed class PlayerUnit : MonoBehaviour
    {
        public const float Radius = PlayerSim.Radius;

        /// <summary>冲刺位移距离（约 2.5 身位）。</summary>
        public const float DashDistance = PlayerSim.DashDistance;

        public const float DashDuration = PlayerSim.DashDuration;
        public const float DashInvulnExtra = PlayerSim.DashInvulnExtra;
        public const float DashCooldown = PlayerSim.DashCooldown;

        private readonly PlayerSim _sim = new PlayerSim();

        private BattleSession _session;
        private SpriteRenderer _renderer;
        private SpriteRenderer _shadow;
        private SpriteRenderer _auraRenderer;
        private Transform _body;
        private Transform _auraVisual;
        private float _flash;

        /// <summary>逻辑状态：只有 <see cref="PlayerSim"/> 写它，表现层只读。</summary>
        public PlayerState State { get; private set; }

        public float DashCooldownLeft => State.DashCooldownLeft;
        public bool IsDashing => State.IsDashing;
        public bool IsInvulnerable => State.IsInvulnerable;

        /// <summary>
        /// 谁在控制这个角色：默认玩家摇杆，冒烟时换成机器人，将来有回放/引导也可以换。
        /// </summary>
        public IUnitInput Input { get; set; }

        /// <summary>当前移动倍率（被鼻涕虫命中后短暂减速 50%）。</summary>
        public float SlowMultiplier => State.SlowMultiplier;

        /// <summary>角色当前面向（单位向量，+Y 朝上）。停下来以后保持最后一次朝向。</summary>
        public Vector2 FacingDirection => State.FacingDirection;

        public void Init(BattleSession session, Vector3 position)
        {
            _session = session;
            State = new PlayerState { Position = position };
            transform.position = position;
            name = "Player";

            // 2D 精灵（Battle/Sprites/player）；没有精灵资源时自动退回圆形占位。
            _renderer = BattleVisuals.Body(
                transform, BattleVisuals.Player, Color.white, Radius * 2f,
                BattleSort.Character(position.y, GetInstanceID()), PlaceholderArt.Circle);
            _body = _renderer.transform;

            var shadow = transform.Find("Shadow");
            _shadow = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;

            // 朝向小三角只为占位图形准备；用精灵时精灵本身已经画了朝向标记，不再叠加
            var nose = transform.Find("Nose");
            if (nose == null && BattleVisuals.GetSprite(BattleVisuals.Player) == null)
            {
                PlaceholderArt.CreateRenderer(
                    "Nose", transform, PlaceholderArt.Triangle, new Color(1f, 1f, 1f, 0.9f), 0.22f, 11,
                    new Vector3(0f, 0.28f, 0f));
            }

            // 光环武器可视化（权能法杖）
            var aura = transform.Find("Aura");
            SpriteRenderer auraRenderer = aura != null ? aura.GetComponent<SpriteRenderer>() : null;
            if (auraRenderer == null)
            {
                auraRenderer = PlaceholderArt.CreateRenderer(
                    "Aura", transform, PlaceholderArt.Ring, new Color(1f, 0.85f, 0.4f, 0.25f), 2f, 1);
            }
            else
            {
                var auraSprite = BattleVisuals.GetSprite(BattleVisuals.Aura);
                if (auraSprite != null)
                {
                    auraRenderer.sprite = auraSprite;
                }

                auraRenderer.color = new Color(1f, 0.9f, 0.55f, 0.55f);
                auraRenderer.sortingOrder = 1;
                auraRenderer.transform.localScale = Vector3.one * 2f;
            }

            _auraVisual = auraRenderer.transform;
            _auraRenderer = auraRenderer;
            _auraVisual.localPosition = Vector3.zero;
            _auraVisual.gameObject.SetActive(false);
            ApplySortOrder();
        }

        public void SetAuraVisible(bool visible)
        {
            if (_auraVisual != null)
            {
                _auraVisual.gameObject.SetActive(visible);
            }
        }

        public void SetAuraRadius(float radius)
        {
            if (_auraVisual != null)
            {
                _auraVisual.localScale = Vector3.one * (radius * 2f);
            }
        }

        public void ApplySlow(float duration)
        {
            State.ApplySlow(duration);
        }

        /// <summary>受击闪红（战斗震动、屏幕泛红、飘字在 BattleRunner.DamagePlayer 里一起触发）。</summary>
        public void FlashHurt()
        {
            _flash = 1f;
        }

        /// <summary>
        /// 每帧只做两件事：让 <see cref="PlayerSim"/> 推进逻辑，再把逻辑状态画出来。
        /// 顺序很重要——先算完位置再同步 Transform 与排序，视觉才不会落后一帧。
        /// </summary>
        public void Tick(float deltaTime, Rect arena)
        {
            _sim.Tick(State, _session, Input, deltaTime, arena);

            transform.position = State.Position;
            ApplySortOrder();
            SyncVisual(deltaTime);
        }

        /// <summary>纯表现：闪红、朝向 Slerp、冲刺半透明。</summary>
        private void SyncVisual(float deltaTime)
        {
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - deltaTime / 0.22f);
                if (_renderer != null)
                {
                    _renderer.color = Color.Lerp(Color.white, new Color(1f, 0.42f, 0.42f), _flash * 0.72f);
                }
            }

            if (_body != null)
            {
                // 贴图默认朝上，所以角度加 90°；只转 Body，不转根节点（阴影/光环会歪）
                var target = Quaternion.Euler(0f, 0f, State.FacingAngle);
                _body.rotation = Quaternion.Slerp(_body.rotation, target, Mathf.Clamp01(deltaTime * 18f));
            }

            if (_renderer != null)
            {
                // 冲刺时半透明，提示无敌帧
                var color = _renderer.color;
                color.a = State.IsDashing ? 0.5f : 1f;
                _renderer.color = color;
            }
        }

        /// <summary>
        /// 按世界 y 更新排序号：越靠下越靠前。这样重叠的精灵不会因为相机移动
        /// 改变「到相机的距离」而交换绘制顺序（之前表现为高速移动时怪物闪烁）。
        /// </summary>
        private void ApplySortOrder()
        {
            var order = BattleSort.Character(transform.position.y, GetInstanceID());

            if (_renderer != null)
            {
                _renderer.sortingOrder = order;
            }

            if (_shadow != null)
            {
                _shadow.sortingOrder = BattleSort.Shadow(order);
            }

            if (_auraRenderer != null)
            {
                _auraRenderer.sortingOrder = BattleSort.Shadow(order) - 1;
            }
        }
    }
}
