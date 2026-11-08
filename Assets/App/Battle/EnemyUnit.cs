using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人单位。7 种行为 + 精英词缀 + Boss 都用这一个类，差异由 EnemyDef.Behavior 与词缀驱动。
    /// 没有物理组件：所有碰撞都是圆形距离判定，由 BattleRunner 每帧统一调度。
    /// </summary>
    public sealed class EnemyUnit : MonoBehaviour
    {
        private IBattleContext _runner;
        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        private readonly ScriptedInput _input = new ScriptedInput();

        // 受击反馈：闪白计时（1 → 0）、贴图原始缩放与颜色
        private float _flash;
        private Vector3 _bodyScale = Vector3.one;
        private Color _bodyColor = Color.white;

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

        /// <summary>出场时按分钟做的数值放大（HP 每分钟 +40%，伤害 +20%）。</summary>
        public void Init(IBattleContext runner, EnemyDef def, Vector3 position, EliteAffix affix, float minuteScaleHp, float minuteScaleDamage)
        {
            _runner = runner;
            State = new EnemyState
            {
                Def = def,
                Affix = affix,
                IsElite = affix != EliteAffix.None,
                IsBoss = def.Id >= BattleSession.BossEnemyId,
                MaxHp = def.Hp * minuteScaleHp,
                Speed = def.Speed,
                Damage = def.Damage * minuteScaleDamage,
                Radius = def.Radius,
                AttackTimer = def.AttackInterval,
                WanderAngle = Random.value * Mathf.PI * 2f
            };
            State.Hp = State.MaxHp;
            State.ApplyAffix();
            name = def.Name;

            transform.position = position;
            State.Position = transform.position;

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
                transform, spriteKey, color, State.Radius * 2f, BattleSort.Character(position.y, GetInstanceID()), fallback);

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
            if (_body != null)
            {
                _body.sortingOrder = order;
            }

            if (_shadow != null)
            {
                _shadow.sortingOrder = BattleSort.Shadow(order);
            }
        }

        private void ApplyAffix()
        {
            State.ApplyAffix();
        }

        public void ApplySlow(float duration)
        {
            State.SlowUntil = Mathf.Max(State.SlowUntil, Context.ElapsedSeconds + duration);
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
        /// 表现层每帧同步：位置跟随状态，然后推进受击表现、朝向与排序。
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

            if (player != null)
            {
                FaceDirection(deltaTime, DirectionTo(player));
            }

            ApplySortOrder();
        }

        // ---------- 策略需要的单位数据（策略本身无状态，见 IEnemyBehavior 注释） ----------

        internal IBattleContext Context => _runner;

        /// <summary>法术/远程攻击计时器。</summary>
        internal float AttackTimer
        {
            get => State.AttackTimer;
            set => State.AttackTimer = value;
        }

        /// <summary>自爆引信是否已点燃。</summary>
        internal bool FuseStarted => State.FuseStarted;

        /// <summary>引信剩余时间。</summary>
        internal float FuseLeft
        {
            get => State.FuseLeft;
            set => State.FuseLeft = value;
        }

        /// <summary>兔子摆动的相位。</summary>
        internal float WanderAngle
        {
            get => State.WanderAngle;
            set => State.WanderAngle = value;
        }

        internal Vector2 DirectionTo(PlayerUnit player)
        {
            var offset = (Vector2)player.transform.position - State.Position;
            var distance = offset.magnitude;
            return distance > 0.001f ? offset / distance : Vector2.zero;
        }

        internal float DistanceTo(PlayerUnit player)
        {
            return Vector2.Distance(State.Position, player.transform.position);
        }

        /// <summary>本帧往哪走（长度 0..1）；系统取走它并清空，位移由 <see cref="EnemySystem"/> 统一换算。</summary>
        internal ScriptedInput Input => _input;

        /// <summary>取走本帧移动意图（行为写入的通道），取走即清零。</summary>
        internal Vector2 TakeMoveIntent()
        {
            var move = _input.Move;
            _input.Set(Vector2.zero);
            return move;
        }

        /// <summary>朝玩家走一步。</summary>
        internal void MoveToward(PlayerUnit player)
        {
            _input.Set(DirectionTo(player));
        }

        /// <summary>按方向与相对倍率移动（1 = 全速）；行为用它表达「侧移 0.4 倍」这类动作。</summary>
        internal void MoveBy(Vector2 direction, float scale)
        {
            _input.Set(Vector2.ClampMagnitude(direction, 1f) * Mathf.Clamp01(scale));
        }

        /// <summary>点燃自爆引信：变红 + 开始倒计时。</summary>
        internal void StartFuse()
        {
            State.FuseStarted = true;
            State.FuseLeft = Mathf.Max(0.2f, Def.FuseTime);
            if (_body != null)
            {
                SetBaseColor(new Color(1f, 0.45f, 0.45f));
            }
        }

        internal void FireProjectile(Vector2 direction)
        {
            _runner.SpawnEnemyProjectile(State.Position, direction, Def.ProjectileSpeed, State.Damage);
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

            _body.color = Color.Lerp(_bodyColor, Color.white, _flash * 0.9f);

            // 受击瞬间鼓一下再弹回，比单纯变色更容易被看见
            _body.transform.localScale = _bodyScale * (1f + _flash * 0.14f);
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
