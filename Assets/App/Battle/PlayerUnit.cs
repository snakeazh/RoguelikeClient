using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 玩家单位：左半屏摇杆移动 + 右下冲刺（位移 + 无敌帧）。
    /// 玩家不做任何攻击，伤害全部来自武器（WeaponRuntime）。
    /// </summary>
    public sealed class PlayerUnit : MonoBehaviour
    {
        public const float Radius = 0.4f;

        /// <summary>冲刺位移距离（约 2.5 身位）。</summary>
        public const float DashDistance = 2.5f;

        public const float DashDuration = 0.2f;
        public const float DashInvulnExtra = 0.1f;
        public const float DashCooldown = 3f;

        private BattleSession _session;
        private SpriteRenderer _renderer;
        private SpriteRenderer _shadow;
        private SpriteRenderer _auraRenderer;
        private Transform _body;
        private Transform _auraVisual;
        private float _facingAngle;
        private float _dashTimeLeft;
        private Vector2 _dashDir;
        private float _dashCooldownLeft;
        private float _invulnLeft;
        private float _slowLeft;
        private float _flash;

        public float DashCooldownLeft => _dashCooldownLeft;
        public bool IsDashing => _dashTimeLeft > 0f;
        public bool IsInvulnerable => _invulnLeft > 0f;

        /// <summary>当前移动倍率（被鼻涕虫命中后短暂减速 50%）。</summary>
        public float SlowMultiplier => _slowLeft > 0f ? 0.5f : 1f;

        /// <summary>角色当前面向（单位向量，+Y 朝上）。停下来以后保持最后一次朝向。</summary>
        public Vector2 FacingDirection
        {
            get
            {
                var radians = (_facingAngle + 90f) * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        public void Init(BattleSession session, Vector3 position)
        {
            _session = session;
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
            _slowLeft = Mathf.Max(_slowLeft, duration);
        }

        /// <summary>受击闪红（战斗震动、屏幕泛红、飘字在 BattleRunner.DamagePlayer 里一起触发）。</summary>
        public void FlashHurt()
        {
            _flash = 1f;
        }

        public void Tick(float deltaTime, Vector2 moveDir, bool dashRequested, Rect arena)
        {
            if (_dashCooldownLeft > 0f)
            {
                _dashCooldownLeft -= deltaTime;
            }

            if (_slowLeft > 0f)
            {
                _slowLeft -= deltaTime;
            }

            if (_invulnLeft > 0f)
            {
                _invulnLeft -= deltaTime;
            }

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - deltaTime / 0.22f);
                if (_renderer != null)
                {
                    _renderer.color = Color.Lerp(Color.white, new Color(1f, 0.42f, 0.42f), _flash * 0.72f);
                }
            }

            if (IsDashing)
            {
                transform.position += (Vector3)(_dashDir * (DashDistance / DashDuration) * deltaTime);
                _dashTimeLeft -= deltaTime;
            }
            else
            {
                var speed = _session.MoveSpeed * SlowMultiplier;
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    transform.position += (Vector3)(moveDir.normalized * (speed * deltaTime));
                }

                if (dashRequested && _dashCooldownLeft <= 0f)
                {
                    StartDash(moveDir);
                }
            }

            ClampToArena(arena);

            ApplySortOrder();
            UpdateFacing(deltaTime, moveDir);

            if (_renderer != null)
            {
                // 冲刺时半透明，提示无敌帧
                var color = _renderer.color;
                color.a = IsDashing ? 0.5f : 1f;
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

        /// <summary>
        /// 精灵朝向：贴着移动方向慢慢转（贴图默认朝上，所以角度减 90°）。
        /// 只转 Body 子节点，不转根节点 —— 否则脚下的阴影和光环会跟着歪。
        /// </summary>
        private void UpdateFacing(float deltaTime, Vector2 moveDir)
        {
            var direction = IsDashing ? _dashDir : moveDir;
            if (direction.sqrMagnitude > 0.0001f)
            {
                _facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            }

            if (_body == null)
            {
                return;
            }

            var target = Quaternion.Euler(0f, 0f, _facingAngle);
            _body.rotation = Quaternion.Slerp(_body.rotation, target, Mathf.Clamp01(deltaTime * 18f));
        }

        private void StartDash(Vector2 moveDir)
        {
            _dashDir = moveDir.sqrMagnitude > 0.01f ? moveDir.normalized : Vector2.up;
            _dashTimeLeft = DashDuration;
            _invulnLeft = DashDuration + DashInvulnExtra;
            _dashCooldownLeft = DashCooldown;
        }

        private void ClampToArena(Rect arena)
        {
            var pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, arena.xMin + Radius, arena.xMax - Radius);
            pos.y = Mathf.Clamp(pos.y, arena.yMin + Radius, arena.yMax - Radius);
            transform.position = pos;
        }
    }
}
