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
        private float _speed;
        private float _damage;
        private float _radius;
        private float _angle;
        private float _slowUntil;
        private float _fuseLeft;
        private float _attackTimer;
        private bool _phaseTwoAnnounced;
        private bool _fuseStarted;

        // 受击反馈：闪白计时（1 → 0）、被击退的速度、贴图原始缩放与颜色
        private float _flash;
        private Vector2 _knockback;
        private Vector3 _bodyScale = Vector3.one;
        private Color _bodyColor = Color.white;

        public EnemyDef Def { get; private set; }
        public float Hp { get; private set; }
        public float MaxHp { get; private set; }
        public float Radius => _radius;
        public float Damage => _damage;
        public bool IsBoss { get; private set; }
        public bool IsElite { get; private set; }
        public EliteAffix Affix { get; private set; }
        public bool IsDead => Hp <= 0f;

        /// <summary>接触伤害冷却（0.5 秒一跳）。</summary>
        public float NextContactTime;

        /// <summary>回旋刃的重复命中冷却。</summary>
        public float NextBladeHitTime;

        /// <summary>出场时按分钟做的数值放大（HP 每分钟 +40%，伤害 +20%）。</summary>
        public void Init(IBattleContext runner, EnemyDef def, Vector3 position, EliteAffix affix, float minuteScaleHp, float minuteScaleDamage)
        {
            _runner = runner;
            Def = def;
            Affix = affix;
            IsElite = affix != EliteAffix.None;
            IsBoss = def.Id >= BattleSession.BossEnemyId;
            name = def.Name;

            MaxHp = def.Hp * minuteScaleHp;
            Hp = MaxHp;
            _speed = def.Speed;
            _damage = def.Damage * minuteScaleDamage;
            _radius = def.Radius;
            _attackTimer = def.AttackInterval;
            _angle = Random.value * Mathf.PI * 2f;

            ApplyAffix();

            transform.position = position;

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
                transform, spriteKey, color, _radius * 2f, BattleSort.Character(position.y, GetInstanceID()), fallback);

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
            switch (Affix)
            {
                case EliteAffix.Swift:
                    _speed *= 1.6f;
                    break;
                case EliteAffix.Giant:
                    _radius *= 1.6f;
                    MaxHp *= 2f;
                    Hp = MaxHp;
                    _damage *= 1.4f;
                    break;
                case EliteAffix.Shell:
                    MaxHp *= 1.8f;
                    Hp = MaxHp;
                    _speed *= 0.85f;
                    break;
                case EliteAffix.Frenzy:
                    _speed *= 1.25f;
                    _damage *= 1.25f;
                    break;
                case EliteAffix.Split:
                    MaxHp *= 0.9f;
                    Hp = MaxHp;
                    break;
            }
        }

        public void ApplySlow(float duration)
        {
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
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

            PlayHitFeedback(amount);
            return false;
        }

        /// <summary>
        /// 受击表现：闪白 + 被击退 + 缩放回弹。
        /// Boss 不击退（体型大、推不动），免得被玩家一直顶在原地抽搐。
        /// </summary>
        private void PlayHitFeedback(float amount)
        {
            _flash = 1f;

            if (IsBoss)
            {
                return;
            }

            var source = _runner != null && _runner.Player != null
                ? (Vector2)_runner.Player.transform.position
                : (Vector2)transform.position - Vector2.up;

            var away = (Vector2)transform.position - source;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = Random.insideUnitCircle.normalized;
            }

            // 伤害越重推得越远，但有上限，免得一拳飞出屏幕。
            var strength = Mathf.Clamp(amount / Mathf.Max(1f, MaxHp) * 2.4f, 0.06f, 0.32f);
            _knockback += away.normalized * strength;
        }

        /// <summary>改基础颜色（自爆怪引信变红等），闪白结束后会回到这个颜色。</summary>
        public void SetBaseColor(Color color)
        {
            _bodyColor = color;
        }

        public void Tick(float deltaTime, PlayerUnit player)
        {
            if (IsDead || player == null)
            {
                return;
            }

            TickHitFeedback(deltaTime);

            // 行动规则交给策略（P2-3），策略只写「这帧想往哪走」，位移由单位统一应用。
            EnemyBehaviors.Get(Def.Behavior).Tick(this, player, deltaTime);
            ApplyMovement(deltaTime);

            if (IsBoss)
            {
                TickBoss(deltaTime);
            }

            FaceDirection(deltaTime, DirectionTo(player));
            ApplySortOrder();
        }

        /// <summary>
        /// 把本帧输入方向换算成位移：所有敌人都走同一条路（移速、减速、地图夹取），
        /// 行为本身不再直接改 transform。
        /// </summary>
        private void ApplyMovement(float deltaTime)
        {
            var move = _input.Move;
            _input.Set(Vector2.zero);
            if (move.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var next = (Vector2)transform.position + move * (CurrentSpeed * deltaTime);
            transform.position = BattleMap.ClampToWalkable(next, _radius);
        }

        // ---------- 策略需要的单位数据（策略本身无状态，见 IEnemyBehavior 注释） ----------

        internal IBattleContext Context => _runner;

        /// <summary>当前移速（含减速效果）。</summary>
        internal float CurrentSpeed => _speed * (Time.time < _slowUntil ? 0.6f : 1f);

        /// <summary>法术/远程攻击计时器。</summary>
        internal float AttackTimer
        {
            get => _attackTimer;
            set => _attackTimer = value;
        }

        /// <summary>自爆引信是否已点燃。</summary>
        internal bool FuseStarted => _fuseStarted;

        /// <summary>引信剩余时间。</summary>
        internal float FuseLeft
        {
            get => _fuseLeft;
            set => _fuseLeft = value;
        }

        /// <summary>兔子摆动的相位。</summary>
        internal float WanderAngle
        {
            get => _angle;
            set => _angle = value;
        }

        internal Vector2 DirectionTo(PlayerUnit player)
        {
            var offset = (Vector2)(player.transform.position - transform.position);
            var distance = offset.magnitude;
            return distance > 0.001f ? offset / distance : Vector2.zero;
        }

        internal float DistanceTo(PlayerUnit player)
        {
            return Vector2.Distance(transform.position, player.transform.position);
        }

        /// <summary>本帧往哪走（长度 0..1，位移由 <see cref="ApplyMovement"/> 统一换算）。</summary>
        internal ScriptedInput Input => _input;

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
            _fuseStarted = true;
            _fuseLeft = Mathf.Max(0.2f, Def.FuseTime);
            if (_body != null)
            {
                SetBaseColor(new Color(1f, 0.45f, 0.45f));
            }
        }

        internal void FireProjectile(Vector2 direction)
        {
            _runner.SpawnEnemyProjectile(transform.position, direction, Def.ProjectileSpeed, _damage);
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
        /// 受击表现的每帧推进：闪白衰减、按闪白程度压回基础色、缩放回弹、击退位移衰减。
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

            if (_knockback.sqrMagnitude > 0.000001f)
            {
                transform.position += (Vector3)(_knockback * deltaTime);
                _knockback *= Mathf.Exp(-14f * deltaTime);
            }
        }

        private void TickBoss(float deltaTime)
        {
            // 阶段二：50% 血量后狂暴（移速 + 弹幕）
            if (Hp > MaxHp * 0.5f)
            {
                return;
            }

            _speed = Def.Speed * 1.35f;
            _attackTimer -= deltaTime;
            if (_attackTimer > 0f)
            {
                return;
            }

            _attackTimer = 3f;
            const int count = 12;
            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count + Time.time;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _runner.SpawnEnemyProjectile(transform.position, dir, Def.ProjectileSpeed, _damage * 0.6f);
            }

            if (_phaseTwoAnnounced)
            {
                return;
            }

            _runner.Toast("Boss 进入第二阶段！");
            _phaseTwoAnnounced = true;
        }

        /// <summary>移除自身（掉落与计数由 BattleRunner 结算）。</summary>
        public void KillSelf()
        {
            Destroy(gameObject);
        }

        public void SetDead()
        {
            Hp = 0f;
        }
    }
}
