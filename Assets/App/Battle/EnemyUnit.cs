using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人单位的**表现层**（P3-1b 之后）。7 种行为 + 精英词缀 + Boss 都用这一个类，
    /// 但规则全在 <see cref="EnemySystem"/> / <see cref="ContactSystem"/> / <see cref="IEnemyBehavior"/>：
    /// 这里只把 <see cref="EnemyState"/> 同步到 Transform 与精灵。
    /// 没有物理组件：所有碰撞都是圆形距离判定。
    /// </summary>
    public sealed class EnemyUnit : MonoBehaviour
    {
        private IBattleContext _runner;
        private SpriteRenderer _body;
        private SpriteRenderer _shadow;

        // 受击反馈：闪白计时（1 → 0）、贴图原始缩放与颜色
        private float _flash;
        private Vector3 _bodyScale = Vector3.one;
        private Color _bodyColor = Color.white;
        private bool _fuseTinted;
        private Color _appliedColor;
        private bool _colorApplied;
        private Vector3 _appliedScale;
        private bool _scaleApplied;
        private int _appliedOrder = int.MinValue;

        /// <summary>纯逻辑状态（P3-1）：数值、计时器、位置都在这里，单位只负责表现与代理。</summary>
        public EnemyState State { get; private set; }

        public EnemyDef Def => State.Def;
        public float Hp => State.Hp;
        public float MaxHp => State.MaxHp;
        public float Radius => State.Radius;
        public float Damage => State.Damage;
        public bool IsBoss => State.IsBoss;
        public bool IsElite => State.IsElite;
        public EliteAffix Affix => State.Affix;
        public bool IsDead => State.IsDead;

        /// <summary>权威位置（<see cref="State.Position"/>）；表现层的 Transform 每帧跟随它。</summary>
        internal Vector2 Position => State.Position;

        /// <summary>接触伤害冷却（0.5 秒一跳）。</summary>
        public float NextContactTime
        {
            get => State.NextContactTime;
            set => State.NextContactTime = value;
        }

        /// <summary>回旋刃的重复命中冷却。</summary>
        public float NextBladeHitTime
        {
            get => State.NextBladeHitTime;
            set => State.NextBladeHitTime = value;
        }

        /// <summary>
        /// 绑定一份已经算好的状态（数值与出生点位由 <see cref="SpawnSystem"/> 决定），
        /// 这里只把状态画出来：精灵、阴影、精英染色、排序。
        /// </summary>
        public void Init(IBattleContext runner, EnemyState state)
        {
            _runner = runner;
            State = state;

            var def = state.Def;
            name = def.Name;

            transform.position = state.Position;

            var color = Color.white;
            if (IsElite)
            {
                // 精英只轻微染色，保留精灵本身的辨识度。
                var tint = PlaceholderArt.ParseColor(def.Color, Color.white);
                color = Color.Lerp(tint, new Color(1f, 0.78f, 0.45f), 0.55f);
            }

            var spriteKey = string.IsNullOrEmpty(def.Sprite) ? BattleVisuals.EnemyFallback : "Battle/Sprites/" + def.Sprite;
            var fallback = def.Behavior == EnemyBehavior.Runner ? PlaceholderArt.Triangle : PlaceholderArt.Circle;
            _body = BattleVisuals.Body(
                transform, spriteKey, color, State.Radius * 2f, BattleSort.Character(transform.position.y, GetInstanceID()), fallback);

            var shadow = transform.Find("Shadow");
            _shadow = shadow != null ? shadow.GetComponent<SpriteRenderer>() : null;

            _bodyColor = color;
            _bodyScale = _body.transform.localScale;

            if (IsBoss)
            {
                transform.localScale = Vector3.one;
            }

            ApplySortOrder();
        }

        /// <summary>按世界 y 更新排序号：越靠下越靠前，重叠时不会因相机移动交换绘制顺序。</summary>
        private void ApplySortOrder()
        {
            var order = BattleSort.Character(transform.position.y, GetInstanceID());
            if (order == _appliedOrder)
            {
                return;
            }

            _appliedOrder = order;

            if (_body != null)
            {
                _body.sortingOrder = order;
            }

            if (_shadow != null)
            {
                _shadow.sortingOrder = BattleSort.Shadow(order);
            }
        }

        public void ApplySlow(float duration, float elapsedSeconds)
        {
            State.SlowUntil = Mathf.Max(State.SlowUntil, elapsedSeconds + duration);
        }

        /// <summary>扣血，返回是否本次死亡。</summary>
        public bool TakeDamage(float amount)
        {
            var died = State.TakeDamage(amount);
            if (died)
            {
                return true;
            }

            PlayHitFeedback(amount);
            return false;
        }

        /// <summary>
        /// 受击表现：闪白 + 缩放回弹。击退是逻辑，交给 <see cref="EnemyState.ApplyKnockback"/>，
        /// 由 <see cref="EnemySystem"/> 每帧推进，表现层不再直接改 transform（否则 headless 就没有击退）。
        /// </summary>
        private void PlayHitFeedback(float amount)
        {
            _flash = 1f;

            var source = _runner != null && _runner.Player != null
                ? (Vector2)_runner.Player.transform.position
                : State.Position - Vector2.up;

            State.ApplyKnockback(source, amount);
        }

        /// <summary>改基础颜色（自爆怪引信变红等），闪白结束后会回到这个颜色。</summary>
        public void SetBaseColor(Color color)
        {
            _bodyColor = color;
        }

        /// <summary>
        /// 表现层每帧同步：位置跟随状态，然后推进受击表现、引信脉冲、朝向与排序。
        /// 规则（行为、位移、击退、Boss 阶段、接触伤害）全在 <see cref="EnemySystem"/> / <see cref="ContactSystem"/>。
        /// </summary>
        public void SyncFromState(float deltaTime, PlayerUnit player)
        {
            if (IsDead)
            {
                return;
            }

            transform.position = State.Position;

            TickHitFeedback(deltaTime);
            TickFuseVisual(deltaTime);

            if (player != null)
            {
                var offset = (Vector2)player.transform.position - State.Position;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    FaceDirection(deltaTime, offset.normalized);
                }
            }

            ApplySortOrder();
        }

        /// <summary>自爆引信的「变红 + 脉冲放大」：逻辑只在 State 里点火，视觉在这里。</summary>
        private void TickFuseVisual(float deltaTime)
        {
            if (!State.FuseStarted)
            {
                return;
            }

            if (!_fuseTinted)
            {
                SetBaseColor(new Color(1f, 0.45f, 0.45f));
                _fuseTinted = true;
            }

            var elapsed = _runner != null ? _runner.ElapsedSeconds : 0f;
            var pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.08f;
            transform.localScale = Vector3.one * pulse;
        }

        /// <summary>
        /// 敌人朝向：让贴图的「上方」对着玩家（贴图默认朝上，所以减 90°），
        /// 这样眼睛始终看着玩家，而不是所有怪都朝着同一个方向。
        /// </summary>
        private void FaceDirection(float deltaTime, Vector2 dir)
        {
            if (_body == null || dir.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            var target = Quaternion.Euler(0f, 0f, angle);
            _body.transform.rotation = Quaternion.Slerp(_body.transform.rotation, target, Mathf.Clamp01(deltaTime * 10f));
        }

        /// <summary>
        /// 受击表现的每帧推进：闪白衰减、按闪白程度压回基础色、缩放回弹。
        /// （击退位移在 <see cref="EnemyState.TickKnockback"/> 里，由系统推进。）
        /// </summary>
        private void TickHitFeedback(float deltaTime)
        {
            if (_body == null)
            {
                return;
            }

            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - deltaTime / 0.09f);
            }

            // 每帧写 SpriteRenderer.color / localScale 都会让渲染器重新上传参数；值没变就不写。
            var color = Color.Lerp(_bodyColor, Color.white, _flash * 0.9f);
            if (!_colorApplied || color != _appliedColor)
            {
                _appliedColor = color;
                _colorApplied = true;
                _body.color = color;
            }

            // 受击瞬间鼓一下再弹回，比单纯变色更容易被看见
            var scale = _bodyScale * (1f + _flash * 0.14f);
            if (scale != _appliedScale)
            {
                _appliedScale = scale;
                _body.transform.localScale = scale;
            }
        }

        /// <summary>移除自身（掉落与计数由 BattleRunner 结算）。</summary>
        public void KillSelf()
        {
            Destroy(gameObject);
        }

        public void SetDead()
        {
            State.Hp = 0f;
        }
    }
}
