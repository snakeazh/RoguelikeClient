using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Bootstrap;
using App.Config;
using App.UI;
using App.UI.Battle;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一局战斗的主循环（唯一的 MonoBehaviour 调度者）：
    /// 输入 → 玩家 → 刷怪 → 敌人 → 武器 → 弹体 → 拾取 → 升级 → 胜负。
    ///
    /// 设计取舍：
    /// - 不用物理组件，全部是圆形距离判定，方便以后换成 ECS / Job，也避免占位美术的 collider 调参；
    /// - 敌人/弹体/拾取物都是普通 List + 每帧倒序遍历销毁，规模在几百个单位以内足够；
    /// - 暂停（升级三选一 / 结算）统一用 Time.timeScale = 0 + Session.IsPaused。
    /// </summary>
    public sealed class BattleRunner : MonoBehaviour
    {
        public static BattleRunner Instance { get; private set; }

        private readonly List<EnemyUnit> _enemies = new List<EnemyUnit>(256);
        private static readonly List<EnemyUnit> s_enemyBuffer = new List<EnemyUnit>(256);
        private readonly List<ProjectileUnit> _projectiles = new List<ProjectileUnit>(128);
        private readonly List<PickupUnit> _pickups = new List<PickupUnit>(256);

        private EnemySpawner _spawner;
        private WeaponRuntime _weapons;
        private IUIManager _ui;
        private Action _onExit;
        private Rect _arena;      // 整张地图（含墙）
        private Rect _walkable;   // 墙内侧，角色可走范围
        private CameraFollow _cameraFollow;
        private float _appliedScreenAspect = -1f;
        private HurtOverlay _hurtOverlay;

        /// <summary>受击后的无敌时间：够长才不会被一群怪物在同一瞬间连续扣血。</summary>
        private const float PlayerHitInvulnerability = 0.6f;

        private float _playerInvulnerableUntil;
        private bool _running;
        private bool _ending;
        private bool _levelUpOpen;
        private float _freezeUntil;
        private float _hudTimer;
        private Vector2 _joystickOrigin;
        private bool _joystickActive;

        public BattleSession Session { get; private set; }
        public PlayerUnit Player { get; private set; }
        public IReadOnlyList<EnemyUnit> Enemies => _enemies;
        public IReadOnlyList<ProjectileUnit> Projectiles => _projectiles;

        /// <summary>冒烟测试用的加速倍率。</summary>
        public float SimulationSpeed { get; set; } = 1f;

        /// <summary>自动战斗：由 BattleSmokeTest 打开，玩家会自己绕圈并冲刺。</summary>
        public bool AutoPlay { get; set; }

        /// <summary>HUD 刷新信号（每帧一次，HUD 只读 Session 属性）。</summary>
        public event Action StateChanged;

        private void Awake()
        {
            Instance = this;
            name = "BattleRunner";
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            Time.timeScale = 1f;
            BattleInput.Reset();
            DamagePopup.ClearAll();

            // 退出战斗时还原相机视口（战斗期间可能为了竖屏比例加过左右黑边）
            var camera = Camera.main;
            if (camera != null)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        public void Begin(IUIManager ui, Action onExit, int seed = 0)
        {
            _ui = ui;
            _onExit = onExit;
            Session = new BattleSession(seed);

            SetupMap();

            // 整张地图：地砖平铺 + 四周边界墙（没有精灵资源时 BattleVisuals 内部静默降级）。
            BattleVisuals.CreateMap(transform, _arena, _walkable);

            var playerGo = BattleVisuals.Spawn(BattleVisuals.PlayerPrefab, "PlayerRoot", transform);
            Player = playerGo.AddComponent<PlayerUnit>();
            Player.Init(Session, _walkable.center);

            // 镜头跟着玩家，并且只在地图范围内移动。
            AttachCameraFollow(playerGo.transform);

            // 玩家受击时的屏幕边缘泛红（挂在 Hud 层，屏幕空间，不跟镜头）。
            if (_hurtOverlay == null && _ui != null && _ui.Root != null)
            {
                _hurtOverlay = HurtOverlay.Create(_ui.Root.GetLayer(UILayer.Hud));
            }

            _spawner = new EnemySpawner();
            _spawner.Init();

            _weapons = new WeaponRuntime();
            _weapons.Init(this, Session);

            // 初始武器：长弓
            var startWeapon = WeaponDef.Get(1);
            if (startWeapon != null)
            {
                Session.AddWeapon(startWeapon);
            }
            else
            {
                AppLog.Warn(LogChannel.Game, "[Battle] start weapon (Id=1) missing in Weapons.json");
            }

            Time.timeScale = 1f;
            _running = true;

            // 排查用：播放器目录下有 autocam.flag 时自动接管操作（发布包不受影响）。
            AutoPlay = AutoPlay || DebugAutoplayHook.Enabled;

            AppLog.Info(LogChannel.Game, "[Battle] begin");
        }

        private void Update()
        {
            if (!_running || Session == null)
            {
                return;
            }

            RefreshAspectGuard();

            // 结束一局后必须先进结算流程：IsOver 由死亡/击杀 Boss/时间到 三种情况设置，
            // 放在暂停判断之前，否则会被上面的 early return 挡住，结算界面永远不弹。
            if (Session.IsOver && !_ending)
            {
                _ = EndAsync(Session.IsVictory);
                return;
            }

            if (Session.IsPaused || _levelUpOpen || _ending)
            {
                return;
            }

            var deltaTime = Time.deltaTime * Mathf.Max(0.01f, SimulationSpeed);
            if (deltaTime > 0.1f)
            {
                deltaTime = 0.1f;
            }

            BattleInput.Poll();

            var move = ReadMove(out var dashRequested);
            Player.Tick(deltaTime, move, dashRequested, _walkable);
            Session.TickTime(deltaTime);

            _spawner.Tick(deltaTime, Session.Elapsed, this);

            TickEnemies(deltaTime);
            _weapons.Tick(deltaTime);
            TickProjectiles(deltaTime);
            TickPickups(deltaTime);

            if (Session.IsOver)
            {
                _ = EndAsync(Session.IsVictory);
                return;
            }

            if (Session.CanLevelUp)
            {
                _ = OpenLevelUpAsync();
                return;
            }

            CheckRoundEnd();

            _hudTimer -= deltaTime;
            if (_hudTimer <= 0f)
            {
                _hudTimer = 0.1f;
                StateChanged?.Invoke();
            }
        }

        // ---------- 输入 ----------
        private Vector2 ReadMove(out bool dash)
        {
            dash = false;
            Vector2 move = Vector2.zero;

            if (AutoPlay || BattleInput.AutoPlay)
            {
                if (BattleInput.AutoDash)
                {
                    BattleInput.AutoDash = false;
                    dash = true;
                }

                move = ComputeAutoMove();
                if (move == Vector2.zero)
                {
                    move = Vector2.up;
                }

                if (Player != null && Player.DashCooldownLeft <= 0f && NearestEnemyDistance() < 1.4f)
                {
                    dash = true;
                }

                return move;
            }

            // 摇杆区域要按“游戏画面”算，而不是按整个窗口算：
            // 宽屏时相机会被裁成 9:16 的中间一列（camera.rect），窗口左右是黑边，
            // 如果还拿 Screen.width/2 当分界线，画面中间按下去会落在“右半屏”里，
            // 表现就是宽屏下按了没反应、窄屏正常。
            var camera = Camera.main;
            var view = camera != null && camera.pixelWidth > 0 && camera.pixelHeight > 0
                ? camera.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            var pointer = BattleInput.PointerPosition;
            var insideView = view.width >= 1f && view.height >= 1f && view.Contains(pointer);

            if (BattleInput.PointerDown && insideView && pointer.x < view.xMin + view.width * 0.5f)
            {
                _joystickOrigin = pointer;
                _joystickActive = true;
            }

            if (BattleInput.PointerHeld && _joystickActive)
            {
                if (insideView)
                {
                    var radius = Mathf.Max(60f, view.height * 0.11f);
                    var delta = (pointer - _joystickOrigin) / radius;
                    move = Vector2.ClampMagnitude(delta, 1f);
                }
            }
            else
            {
                _joystickActive = false;
            }

            // 键盘兜底（PC 调试）
            if (move == Vector2.zero)
            {
                var x = Input.GetAxisRaw("Horizontal");
                var y = Input.GetAxisRaw("Vertical");
                move = new Vector2(x, y);
            }

            dash = BattleInput.ConsumeDash();
            return move;
        }

        // ---------- 自动战斗（冒烟测试用） ----------

        /// <summary>简单的逃跑/捡糖机器人：躲开近处敌人，安全时去捡经验，否则绕场中心转圈。</summary>
        private Vector2 ComputeAutoMove()
        {
            if (Player == null)
            {
                return Vector2.zero;
            }

            var position = (Vector2)Player.transform.position;
            var threat = Vector2.zero;
            var threatCount = 0;

            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var offset = position - (Vector2)enemy.transform.position;
                var distance = offset.magnitude;
                if (distance > 4.5f)
                {
                    continue;
                }

                threatCount++;
                threat += offset / Mathf.Max(0.3f, distance * distance);
            }

            Vector2 move;
            if (threatCount > 0)
            {
                // 一边逃跑一边绕场中心切向移动，避免被逼到墙角。
                var toCenter = _walkable.center - position;
                var tangent = new Vector2(-toCenter.y, toCenter.x).normalized;
                var flee = threat.sqrMagnitude > 0.0001f ? threat.normalized : Vector2.zero;
                move = flee + tangent * 0.9f;
            }
            else
            {
                move = FindPickupDirection(position);
                if (move == Vector2.zero)
                {
                    // 没有威胁也没有糖：绕场中心切向移动，避免贴墙。
                    var toCenter = _walkable.center - position;
                    move = new Vector2(-toCenter.y, toCenter.x).normalized * 0.8f + toCenter.normalized * 0.2f;
                }
            }

            var margin = 1.5f;
            if (position.x < _walkable.xMin + margin) move.x += 1.2f;
            if (position.x > _walkable.xMax - margin) move.x -= 1.2f;
            if (position.y < _walkable.yMin + margin) move.y += 1.2f;
            if (position.y > _walkable.yMax - margin) move.y -= 1.2f;

            return Vector2.ClampMagnitude(move, 1f);
        }

        private Vector2 FindPickupDirection(Vector2 position)
        {
            var best = float.MaxValue;
            var target = Vector2.zero;
            var found = false;

            for (var i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup == null)
                {
                    continue;
                }

                var delta = (Vector2)pickup.transform.position - position;
                var distance = delta.magnitude;
                if (distance > 6f || distance >= best)
                {
                    continue;
                }

                best = distance;
                target = delta;
                found = true;
            }

            return found ? target.normalized : Vector2.zero;
        }

        private float NearestEnemyDistance()
        {
            if (Player == null)
            {
                return float.MaxValue;
            }

            var position = (Vector2)Player.transform.position;
            var nearest = float.MaxValue;
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var distance = Vector2.Distance(position, enemy.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }

        // ---------- 敌人 ----------
        private void TickEnemies(float deltaTime)
        {
            var frozen = Time.time < _freezeUntil;

            // 敌人 Tick 里会连锁击杀（爆炸、分裂）进而增删 _enemies，所以先拷一份再遍历。
            s_enemyBuffer.Clear();
            s_enemyBuffer.AddRange(_enemies);
            for (var i = 0; i < s_enemyBuffer.Count; i++)
            {
                var enemy = s_enemyBuffer[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                if (!frozen)
                {
                    enemy.Tick(deltaTime, Player);
                    TickContact(enemy);
                }
            }

            for (var i = _enemies.Count - 1; i >= 0; i--)
            {
                if (_enemies[i] == null || _enemies[i].IsDead)
                {
                    _enemies.RemoveAt(i);
                }
            }
        }

        private void TickContact(EnemyUnit enemy)
        {
            if (Player == null || enemy.IsDead)
            {
                return;
            }

            var distance = Vector2.Distance(enemy.transform.position, Player.transform.position);
            if (distance > enemy.Radius + PlayerUnit.Radius)
            {
                return;
            }

            if (Time.time < enemy.NextContactTime)
            {
                return;
            }

            enemy.NextContactTime = Time.time + 0.5f;
            DamagePlayer(enemy.Damage);

            if (enemy.Def.SlowOnHit > 0f)
            {
                Player.ApplySlow(enemy.Def.SlowOnHit);
            }
        }

        // ---------- 弹体 ----------
        private void TickProjectiles(float deltaTime)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = _projectiles[i];
                if (projectile == null)
                {
                    _projectiles.RemoveAt(i);
                    continue;
                }

                if (projectile.Tick(deltaTime, this))
                {
                    _projectiles.RemoveAt(i);
                    Destroy(projectile.gameObject);
                }
            }
        }

        // ---------- 拾取 ----------
        private void TickPickups(float deltaTime)
        {
            var magnetRadius = 1.8f * Session.MagnetMultiplier;
            for (var i = _pickups.Count - 1; i >= 0; i--)
            {
                var pickup = _pickups[i];
                if (pickup == null)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                if (!pickup.Tick(deltaTime, Player, magnetRadius))
                {
                    continue;
                }

                ApplyPickup(pickup);
                _pickups.RemoveAt(i);
                Destroy(pickup.gameObject);
            }
        }

        private void ApplyPickup(PickupUnit pickup)
        {
            switch (pickup.Kind)
            {
                case PickupKind.ExpSmall:
                case PickupKind.ExpMedium:
                case PickupKind.ExpLarge:
                    Session.AddExp(pickup.Value);
                    break;

                case PickupKind.Cake:
                    Session.HealPercent(0.3f);
                    Toast("恢复 30% 生命");
                    break;

                case PickupKind.Gold:
                    Session.AddGold(pickup.Value);
                    break;

                case PickupKind.Chest:
                    GrantChest();
                    break;

                case PickupKind.Magnet:
                    foreach (var other in _pickups)
                    {
                        if (other != null && other != pickup)
                        {
                            other.ForceAttract();
                        }
                    }

                    Toast("磁力吸取！");
                    break;

                case PickupKind.Bomb:
                    ExplodeAt(Player.transform.position, 4f, 60f, false);
                    Toast("糖果炸弹！");
                    break;

                case PickupKind.Hourglass:
                    _freezeUntil = Time.time + 3f;
                    Toast("时间定格 3 秒");
                    break;
            }
        }

        private void GrantChest()
        {
            // 1) 有满足条件的武器先进化（满级武器 + 配套被动满级）。
            var evolvable = Session.FindEvolvable();
            if (evolvable != null)
            {
                var evolved = Session.Evolve(evolvable);
                if (evolved != null)
                {
                    Toast($"宝箱：{evolved.Name} 进化完成！");
                    TransientFx.Flash(Player.transform.position, 3.2f, new Color(1f, 0.85f, 0.35f), 0.5f);
                    return;
                }
            }

            var candidates = Session.Weapons.Where(w => !w.IsMaxLevel).ToList();
            if (candidates.Count == 0 && Session.CanAddWeapon)
            {
                var missing = WeaponDef.All.Values.Where(d => d.Id < 100 && !Session.HasWeapon(d.Id)).ToList();
                if (missing.Count > 0)
                {
                    Session.AddWeapon(missing[Session.Rng.Next(missing.Count)]);
                    Toast("宝箱：获得新武器！");
                    return;
                }
            }

            if (candidates.Count > 0)
            {
                var weapon = candidates[Session.Rng.Next(candidates.Count)];
                weapon.TryUpgrade();
                Toast($"宝箱：{weapon.Def.Name} 升到 Lv{weapon.Level}");
                return;
            }

            Session.AddGold(50);
            Toast("宝箱：金币 +50");
        }

        // ---------- 刷怪 ----------
        public void SpawnEnemy(int enemyId, Vector2 position, EliteAffix affix, float elapsed)
        {
            var def = EnemyDef.Get(enemyId);
            if (def == null)
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] enemy id {enemyId} not found");
                return;
            }

            // 环形刷新之类的点位可能落在墙外，统一夹进可走范围。
            position = BattleMap.ClampToWalkable(position, 0.4f);

            var go = BattleVisuals.Spawn(BattleVisuals.EnemyPrefab, "Enemy_" + def.Name, transform);
            var enemy = go.AddComponent<EnemyUnit>();
            enemy.Init(this, def, position, affix, EnemySpawner.HpScale(elapsed), EnemySpawner.DamageScale(elapsed));
            _enemies.Add(enemy);
        }

        public void SpawnBoss(int enemyId, float elapsed)
        {
            var def = EnemyDef.Get(enemyId);
            if (def == null)
            {
                return;
            }

            // Boss 从玩家正上方、屏幕外走进来；地图边界比屏幕大，所以不会刷在墙外。
            var player = Player != null ? (Vector2)Player.transform.position : _walkable.center;
            var view = CameraView(1.2f);
            var position = BattleMap.ClampToWalkable(
                new Vector2(player.x, view.yMax + 0.6f),
                0.5f);
            var go = BattleVisuals.Spawn(BattleVisuals.EnemyPrefab, "Boss_" + def.Name, transform);
            var boss = go.AddComponent<EnemyUnit>();
            boss.Init(this, def, position, EliteAffix.None, 1f, 1f);
            _enemies.Add(boss);
        }

        /// <summary>
        /// 普通刷怪点：贴着相机可视区的外沿（沿射线找到屏幕边框再向外 0.9 世界单位），
        /// 再夹进地图可走范围。这样既不会眼看见怪凭空出现，怪也不用从很远的地方走过来。
        /// </summary>
        public Vector2 RandomSpawnPosition()
        {
            var player = Player != null ? (Vector2)Player.transform.position : _walkable.center;
            var view = CameraView();
            var center = view.center;

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // 射线与可视矩形边界的交点距离（取 x/y 两方向里更近的那个）。
                var distance = float.MaxValue;
                if (Mathf.Abs(dir.x) > 0.0001f)
                {
                    distance = Mathf.Min(distance, view.width * 0.5f / Mathf.Abs(dir.x));
                }

                if (Mathf.Abs(dir.y) > 0.0001f)
                {
                    distance = Mathf.Min(distance, view.height * 0.5f / Mathf.Abs(dir.y));
                }

                var candidate = player + dir * (distance + 0.45f);
                var clamped = BattleMap.ClampToWalkable(candidate, 0.4f);

                // 被墙挤回来以后必须仍在视野外，否则会眼看着怪凭空出现。
                if (!view.Contains(clamped))
                {
                    return clamped;
                }

                _ = center;
            }

            // 兜底：从地图边缘随机取一点（地图比屏幕大，边缘一定在视野外）。
            var edge = UnityEngine.Random.Range(0, 4);
            var bounds = _walkable;
            switch (edge)
            {
                case 0:
                    return new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMax - 0.5f);
                case 1:
                    return new Vector2(bounds.xMax - 0.5f, UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
                case 2:
                    return new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMin + 0.5f);
                default:
                    return new Vector2(bounds.xMin + 0.5f, UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
            }
        }

        public void RunTide(WaveDef wave)
        {
            StartCoroutine(TideRoutine(wave));
        }

        private System.Collections.IEnumerator TideRoutine(WaveDef wave)
        {
            Toast("怪物潮来袭！");
            var waves = Mathf.Max(1, Mathf.RoundToInt(wave.Duration / Mathf.Max(0.2f, wave.Interval)));
            for (var i = 0; i < waves; i++)
            {
                for (var j = 0; j < wave.SpawnCount; j++)
                {
                    SpawnEnemy(wave.EnemyId, RandomSpawnPosition(), wave.Affix, Session.Elapsed);
                }

                yield return new WaitForSeconds(wave.Interval);
            }
        }

        // ---------- 伤害 ----------
        public void DamageEnemy(EnemyUnit enemy, float amount, bool crit = false)
        {
            if (enemy == null || enemy.IsDead)
            {
                return;
            }

            var died = enemy.TakeDamage(amount);

            PlayEnemyHitFx(enemy, amount, crit);

            if (!died)
            {
                return;
            }

            KillEnemy(enemy);
        }

        /// <summary>敌人受击反馈：飘字 + 白色火花（闪白/击退/回弹在 EnemyUnit 内部）。</summary>
        private void PlayEnemyHitFx(EnemyUnit enemy, float amount, bool crit)
        {
            var color = crit ? new Color(1f, 0.85f, 0.3f) : Color.white;
            DamagePopup.Show(
                enemy.transform.position + Vector3.up * (enemy.Radius + 0.25f), amount, crit, color);

            TransientFx.Flash(
                enemy.transform.position,
                enemy.Radius * (crit ? 1.7f : 1.3f),
                new Color(1f, 1f, 1f, crit ? 0.9f : 0.7f),
                crit ? 0.14f : 0.1f,
                22);
        }

        private void KillEnemy(EnemyUnit enemy)
        {
            Session.AddKill();

            var def = enemy.Def;
            var position = (Vector2)enemy.transform.position;

            // 死亡爆散：让「打死了」和「打了一下」在画面上区分开
            TransientFx.Flash(
                position,
                enemy.Radius * (enemy.IsBoss ? 4.2f : 2.6f),
                new Color(1f, 1f, 1f, 0.7f),
                enemy.IsBoss ? 0.35f : 0.2f,
                18);

            // 经验糖：经验值随分钟成长，保证 5 分钟内能打到 20 级（满级需要约 3500 点）。
            if (def.Exp > 0)
            {
                var exp = Mathf.Max(1, Mathf.RoundToInt(def.Exp * ExpScale()));
                var kind = exp >= 20 ? PickupKind.ExpLarge : exp >= 5 ? PickupKind.ExpMedium : PickupKind.ExpSmall;
                SpawnPickup(kind, exp, position);
            }

            // 金币 / 小蛋糕掉落
            var luck = Session.Luck;
            if (UnityEngine.Random.value < 0.08f + luck * 0.002f)
            {
                SpawnPickup(PickupKind.Gold, 10, position + UnityEngine.Random.insideUnitCircle * 0.2f);
            }

            if (UnityEngine.Random.value < 0.04f)
            {
                SpawnPickup(PickupKind.Cake, 0, position);
            }

            if (enemy.IsElite)
            {
                SpawnPickup(PickupKind.Gold, 25, position);
                if (UnityEngine.Random.value < 0.35f)
                {
                    SpawnPickup(PickupKind.Chest, 0, position + UnityEngine.Random.insideUnitCircle * 0.3f);
                }
            }

            // 分裂
            if (def.Behavior == EnemyBehavior.Splitter || enemy.Affix == EliteAffix.Split)
            {
                for (var i = 0; i < 2; i++)
                {
                    var offset = UnityEngine.Random.insideUnitCircle * 0.6f;
                    SpawnEnemy(8, position + offset, EliteAffix.None, Session.Elapsed);
                }
            }

            _enemies.Remove(enemy);
            enemy.KillSelf();

            if (enemy.IsBoss)
            {
                SpawnPickup(PickupKind.Chest, 0, position);
                Session.AddGold(30);

                if (def.Id == BattleSession.BossEnemyId)
                {
                    Session.Finish(true);
                    AppLog.Info(LogChannel.Game, "[Battle] final boss defeated");
                }
                else
                {
                    Toast($"{def.Name} 被击败！");
                    AppLog.Info(LogChannel.Game, $"[Battle] boss defeated id={def.Id}");
                }
            }
        }

        public void DamagePlayer(float amount)
        {
            if (Player == null || Player.IsInvulnerable || Session.IsOver)
            {
                return;
            }

            // 受击后短暂无敌：避免被人群连续判定瞬间打死（一次伤害结算一堆敌人）。
            if (Time.time < _playerInvulnerableUntil)
            {
                return;
            }

            _playerInvulnerableUntil = Time.time + PlayerHitInvulnerability;
            Session.ApplyDamage(amount);

            // 受击反馈：角色闪红 + 屏幕边缘泛红 + 镜头震动 + 红色飘字
            Player.FlashHurt();

            var severity = Mathf.Clamp01(amount / Mathf.Max(1f, Session.MaxHp) * 2.5f);
            if (_hurtOverlay != null)
            {
                _hurtOverlay.Pulse(0.2f + severity * 0.4f);
            }

            if (_cameraFollow != null)
            {
                _cameraFollow.Shake(0.04f + severity * 0.12f, 0.18f);
            }

            DamagePopup.Show(
                Player.transform.position + Vector3.up * (PlayerUnit.Radius + 0.3f),
                amount,
                false,
                new Color(1f, 0.35f, 0.35f));
        }

        public void ExplodeAt(Vector3 position, float radius, float damage, bool hitPlayer)
        {
            var center = (Vector2)position;
            foreach (var enemy in _enemies.ToArray())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                if (Vector2.Distance(center, enemy.transform.position) <= radius + enemy.Radius)
                {
                    DamageEnemy(enemy, damage);
                }
            }

            if (hitPlayer && Player != null)
            {
                if (Vector2.Distance(center, Player.transform.position) <= radius + PlayerUnit.Radius)
                {
                    DamagePlayer(damage);
                }
            }
        }

        public void SpawnPlayerProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            float damage,
            float radius,
            int pierce,
            float splash,
            float life,
            EnemyUnit homingTarget = null,
            string spriteKey = null)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.ProjectilePrefab, "Projectile", transform);
            var projectile = go.AddComponent<ProjectileUnit>();
            projectile.InitPlayer(position, direction, speed, damage, radius, pierce, splash, life, 0f, homingTarget, spriteKey);
            _projectiles.Add(projectile);
        }

        public void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.ProjectilePrefab, "EnemyProjectile", transform);
            var projectile = go.AddComponent<ProjectileUnit>();
            projectile.InitEnemy(position, direction, speed, damage, 0.16f);
            _projectiles.Add(projectile);
        }

        /// <summary>经验成长系数：0 分钟 1 倍，每分钟 +1.8 倍（5 分钟约 10 倍）。</summary>
        private float ExpScale()
        {
            return 1f + Session.Elapsed / 60f * 1.8f;
        }

        private void SpawnPickup(PickupKind kind, int value, Vector2 position)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.PickupPrefab, "Pickup", transform);
            var pickup = go.AddComponent<PickupUnit>();
            pickup.Init(kind, value, position);
            _pickups.Add(pickup);
        }

        public void DropRestPickups()
        {
            if (Player == null)
            {
                return;
            }

            var position = (Vector2)Player.transform.position;
            SpawnPickup(PickupKind.Cake, 0, position + new Vector2(-1.2f, 0.5f));
            SpawnPickup(PickupKind.Magnet, 0, position + new Vector2(1.2f, 0.5f));
        }

        // ---------- 升级 ----------
        private async Task OpenLevelUpAsync()
        {
            if (_levelUpOpen)
            {
                return;
            }

            _levelUpOpen = true;
            Session.IsPaused = true;

            try
            {
                var options = ChoiceService.Build(Session);
                var vm = new LevelUpViewModel();
                vm.Setup(Session.Level + 1, options);
                await _ui.Open(vm);
                var index = await vm.WaitChoiceAsync();
                await _ui.Close(vm);

                Session.IsPaused = false;

                if (index >= 0 && index < options.Count)
                {
                    ChoiceService.Apply(Session, options[index]);
                }

                Session.TryLevelUp();
                Session.HealPercent(0.15f);
                StateChanged?.Invoke();
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
                Session.IsPaused = false;
            }
            finally
            {
                _levelUpOpen = false;
            }
        }

        // ---------- 胜负 ----------
        private void CheckRoundEnd()
        {
            if (Session.IsOver)
            {
                return;
            }

            if (Session.Elapsed >= BattleSession.RoundDuration)
            {
                // 时间到：Boss 已死才算胜利，否则算超时失败（不然 Boss 没打死会一直打下去）。
                var bossAlive = _enemies.Any(e => e != null && e.IsBoss && !e.IsDead);
                Session.Finish(!bossAlive);
            }
        }

        private async Task EndAsync(bool victory)
        {
            if (_ending)
            {
                return;
            }

            _ending = true;
            _running = false;
            Session.Finish(victory);
            Session.IsPaused = true;
            ClearBattlefield();
            StateChanged?.Invoke();

            // 局外结算：金币 / 次数 / 最高记录（服务未注册时静默跳过）。
            try
            {
                var records = AppServices.Resolve<BattleRecords>();
                records?.Commit(Session);
            }
            catch (Exception e)
            {
                AppLog.Warn(LogChannel.Game, "[Battle] records not committed: " + e.Message);
            }

            try
            {
                var vm = new BattleResultViewModel();
                vm.Setup(Session);
                await _ui.Open(vm);
                await vm.WaitCloseAsync();
                await _ui.Close(vm);
            }
            catch (Exception e)
            {
                AppLog.Exception(LogChannel.Game, e);
            }

            Session.IsPaused = false;
            _onExit?.Invoke();
        }

        public void Toast(string message)
        {
            App.UI.Toast.Show(message, 1.2f);
        }

        /// <summary>结算时清场：结算面板后面不该再留着敌人、弹体与掉落物。</summary>
        private void ClearBattlefield()
        {
            foreach (var enemy in _enemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            _enemies.Clear();

            foreach (var projectile in _projectiles)
            {
                if (projectile != null)
                {
                    Destroy(projectile.gameObject);
                }
            }

            _projectiles.Clear();

            foreach (var pickup in _pickups)
            {
                if (pickup != null)
                {
                    Destroy(pickup.gameObject);
                }
            }

            _pickups.Clear();

            _weapons?.Dispose();
        }

        // ---------- 工具 ----------

        /// <summary>
        /// 地图尺寸来自 <see cref="BattleMap"/>（18×36），不再由相机反推一屏。
        /// 相机只负责“看地图的一块”，由 <see cref="CameraFollow"/> 跟随玩家并夹在地图边界内。
        /// </summary>
        private void SetupMap()
        {
            _arena = BattleMap.Bounds;
            _walkable = BattleMap.Walkable;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                // 竖屏 9:16 时可见 5.63 × 10；地图 18×36，所以横向约 3.2 屏、纵向约 3.6 屏。
                camera.orthographicSize = 5f;
                ApplyAspectGuard(camera);
            }
        }

        /// <summary>竖屏游戏在宽屏窗口里会被“看穿”整张地图（可视宽度 ≥ 地图宽度时镜头夹取只剩中心），
        /// 这里把游戏相机限制成 9:16 的中间一列（两侧留黑），保证任何分辨率下都有可跟随的横向余量。</summary>
        private static void ApplyAspectGuard(Camera camera)
        {
            const float TargetAspect = 9f / 16f;
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;

            if (screenAspect <= TargetAspect + 0.001f)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var width = TargetAspect / screenAspect;
            camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        /// <summary>窗口被拉伸 / 最大化后重新算一次黑边，避免镜头夹取范围失效。</summary>
        private void RefreshAspectGuard()
        {
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0f;
            if (Mathf.Abs(screenAspect - _appliedScreenAspect) < 0.001f)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            _appliedScreenAspect = screenAspect;
            ApplyAspectGuard(camera);
        }

        private void AttachCameraFollow(Transform target)
        {
            var camera = Camera.main;
            if (camera == null || target == null)
            {
                return;
            }

            _cameraFollow = camera.GetComponent<CameraFollow>();
            if (_cameraFollow == null)
            {
                _cameraFollow = camera.gameObject.AddComponent<CameraFollow>();
            }

            _cameraFollow.Configure(target, _arena);
        }

        public bool IsInsideArena(Vector3 position, float margin)
        {
            return position.x >= _arena.xMin - margin
                   && position.x <= _arena.xMax + margin
                   && position.y >= _arena.yMin - margin
                   && position.y <= _arena.yMax + margin;
        }

        /// <summary>整张地图（含墙）。</summary>
        public Rect Arena => _arena;

        /// <summary>墙内侧的可走范围。</summary>
        public Rect Walkable => _walkable;

        /// <summary>相机可视区域（用于“刷在屏幕外”的判断）。</summary>
        public Rect CameraView(float padding = 0f)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return new Rect(-2.81f - padding, -5f - padding, 5.63f + padding * 2f, 10f + padding * 2f);
            }

            var halfHeight = camera.orthographicSize + padding;
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0.5625f;
            var halfWidth = halfHeight * screenAspect * Mathf.Max(0.01f, camera.rect.width);
            var center = camera.transform.position;
            return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        }
    }
}
