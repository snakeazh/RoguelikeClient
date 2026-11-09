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
    /// - 流程状态收敛在 <see cref="BattlePhase"/>：只有 Running 推进模拟，升级/暂停/结算都在等面板或玩家。
    /// </summary>
    public sealed partial class BattleRunner : MonoBehaviour, IBattleContext, IPickupContext, IEnemyContext
    {
        /// <summary>敌人视图：状态是权威，这里只保存「状态 → 显示对象」的映射。</summary>
        private readonly Dictionary<EnemyState, EnemyUnit> _enemyViews = new Dictionary<EnemyState, EnemyUnit>(256);
        private readonly List<ProjectileUnit> _projectiles = new List<ProjectileUnit>(128);
        private readonly List<PickupUnit> _pickups = new List<PickupUnit>(256);



        private IEnemyFactory _enemyFactory;
        private ProjectileViewPool _projectileViews;
        private PickupViewPool _pickupViews;

        /// <summary>战斗编排者（P3-1b 第 5 步）：持有玩家/刷怪/敌人/接触/弹体/拾取，Runner 只管表现与流程。</summary>
        private BattleSimulation _simulation;

        private WeaponRuntime _weapons;

        /// <summary>本局输入源：玩家摇杆或自动战斗机器人。</summary>
        private IUnitInput _input;
        private IUIManager _ui;
        private Action _onExit;
        private BattleRecords _records;
        private Rect _arena;      // 整张地图（含墙）
        private Rect _walkable;   // 墙内侧，角色可走范围
        private CameraFollow _cameraFollow;
        private float _appliedScreenAspect = -1f;
        private HurtOverlay _hurtOverlay;

        /// <summary>受击后的无敌时间：够长才不会被一群怪物在同一瞬间连续扣血。</summary>
        private const float PlayerHitInvulnerability = 0.6f;

        private float _playerInvulnerableUntil;
        private float _freezeUntil;
        private float _hudTimer;
        private PlayerInput _playerInput;
        private AutoPlayerInput _autoInput;

        public BattleSession Session { get; private set; }
        /// <summary>表现层的玩家视图（挂在场景里的 MonoBehaviour）。规则层只看到 <see cref="PlayerState"/>。</summary>
        public PlayerUnit Player { get; private set; }

        /// <summary>规则层视角的玩家：纯数据，行为层据此算位置与朝向。</summary>
        PlayerState IBattleContext.Player => _simulation?.Player;
        public IReadOnlyList<EnemyState> Enemies => _simulation != null ? _simulation.Enemies : System.Array.Empty<EnemyState>();
        public Transform Root => transform;
        public float ElapsedSeconds => Session != null ? Session.Elapsed : 0f;
        public IReadOnlyList<ProjectileUnit> Projectiles => _projectiles;
        public IReadOnlyList<PickupUnit> Pickups => _pickups;

        /// <summary>当前阶段。只能用 <see cref="SetPhase"/> 改，转移合法性由 <see cref="BattlePhaseRules"/> 决定。</summary>
        public BattlePhase Phase { get; private set; } = BattlePhase.Idle;

        /// <summary>模拟是否冻结（UI 层想加「暂停中」提示时读这个）。</summary>
        public bool IsSimulationPaused => Phase.BlocksSimulation();

        /// <summary>阶段变化通知，调试面板 / HUD 可订阅。</summary>
        public event Action<BattlePhase, BattlePhase> PhaseChanged;

        /// <summary>冒烟测试用的加速倍率。</summary>
        public float SimulationSpeed { get; set; } = 1f;

        /// <summary>
        /// 固定仿真步长（秒）。0 = 用真实帧时间。
        /// headless 数值回归与「可复现冒烟」把它设成 1/60，配合固定随机种子即可逐位复现。
        /// </summary>
        public float FixedStepSeconds { get; set; }

        /// <summary>自动战斗：由 BattleSmokeTest 打开，玩家会自己绕圈并冲刺。</summary>
        public bool AutoPlay
        {
            get => _autoPlay;
            set
            {
                _autoPlay = value;
                _input = value ? (IUnitInput)_autoInput : _playerInput;
            }
        }

        private bool _autoPlay;

        /// <summary>HUD 冲刺按钮：转给玩家输入，由下一帧消费。</summary>
        public void RequestDash()
        {
            _playerInput?.RequestDash();
        }

        /// <summary>HUD 刷新信号（每帧一次，HUD 只读 Session 属性）。</summary>
        public event Action StateChanged;

        private void Awake()
        {
            name = "BattleRunner";
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            AutoPlay = false;

            // 战斗里这些类各自缓存了运行时生成的贴图/材质/对象池，跨局不清理会一直累积。
            DamagePopup.ResetPool();
            PlaceholderArt.ResetCache();
            TransientFx.ResetCache();
            HurtOverlay.ResetCache();
            LightningFx.ResetCache();

            // 退出战斗时还原相机视口（战斗期间可能为了竖屏比例加过左右黑边）
            var camera = Camera.main;
            if (camera != null)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        public void Begin(IUIManager ui, Action onExit, int seed = 0, int characterId = 0, BattleRecords records = null)
        {
            _ui = ui;
            _onExit = onExit;
            _records = records;
            Session = new BattleSession(seed);

            // 战斗随机全部走 Session.Rng（种子在这里生效）；表现层才用 UnityEngine.Random。

            // 角色：决定血量、移速和本命武器；缺表时 SetCharacter 不生效，退回 100 血 / 5 速 / 长弓。
            var character = CharacterDef.Get(characterId > 0 ? characterId : BattleSession.DefaultCharacterId);
            if (character != null)
            {
                Session.SetCharacter(character);
            }
            else
            {
                AppLog.Warn(LogChannel.Game, "[Battle] character table missing, falling back to default stats");
            }

            // 角色自带的武器：开局就有，不占武器槽。
            var innate = WeaponDef.Get(Session.InnateWeaponId);
            if (innate != null)
            {
                Session.EquipInnate(innate);
            }
            else
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] innate weapon (Id={Session.InnateWeaponId}) missing in Weapons.json");
            }

            SetupMap();

            // 整张地图：地砖平铺 + 四周边界墙（没有精灵资源时 BattleVisuals 内部静默降级）。
            BattleVisuals.CreateMap(transform, _arena, _walkable);

            var playerGo = BattleVisuals.Spawn(BattleVisuals.PlayerPrefab, "PlayerRoot", transform);
            Player = playerGo.AddComponent<PlayerUnit>();
            Player.Init(_walkable.center);

            // 输入：玩家默认摇杆/键盘；开自动战斗时换成机器人写入的被动通道。
            _playerInput = new PlayerInput();
            _autoInput = new AutoPlayerInput(this);
            _input = AutoPlay ? (IUnitInput)_autoInput : _playerInput;

            // 分配归因探针打开时才需要帧尾标记，平时不加组件。
            if (BattleAllocProbe.Enabled && GetComponent<BattleFrameTail>() == null)
            {
                gameObject.AddComponent<BattleFrameTail>();
            }

            // 镜头跟着玩家，并且只在地图范围内移动。
            AttachCameraFollow(playerGo.transform);

            // 玩家受击时的屏幕边缘泛红（挂在 Hud 层，屏幕空间，不跟镜头）。
            if (_hurtOverlay == null && _ui != null && _ui.Root != null)
            {
                _hurtOverlay = HurtOverlay.Create(_ui.Root.GetLayer(UILayer.Hud));
            }

            // 生成器：敌人/拾取物各自一个工厂（MonoBehaviour 不能用构造器），波次策略按默认注册表注入。
            _enemyFactory = new EnemyFactory(this, transform);
            _projectileViews = new ProjectileViewPool(transform);
            _pickupViews = new PickupViewPool(transform);

            // 规则层由 BattleSimulation 组合：玩家位移 / 刷怪 / 敌人 / 接触 / 弹体 / 拾取。
            _simulation = new BattleSimulation(
                Session, _walkable.center, WaveStepRegistry.CreateDefault(), WaveDef.All.Values, PickupEffects.CreateDefault());
            Player.Bind(_simulation.Player);

            _weapons = new WeaponRuntime();
            _weapons.Init(this, Session, new UnityWeaponViews(transform, Player));

            // 初始武器不再是「掉落武器」：角色自带的兵器在 Begin 开头用 EquipInnate 装上，
            // 它不占武器槽，也不会再走这里，否则同一把长弓会被算两把。
            Time.timeScale = 1f;
            SetPhase(BattlePhase.Running);

            // 排查用：播放器目录下有 autocam.flag 时自动接管操作（发布包不受影响）。
            AutoPlay = AutoPlay || DebugAutoplayHook.Enabled;

            AppLog.Info(LogChannel.Game, "[Battle] begin");
        }

        /// <summary>
        /// 唯一的阶段写入口：非法转移只告警不抛异常，避免异步回调里的一次误调用把整局拧坏。
        /// </summary>
        private bool SetPhase(BattlePhase next)
        {
            if (!BattlePhaseRules.CanTransition(Phase, next))
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] illegal transition {Phase} -> {next}, ignored");
                return false;
            }

            var previous = Phase;
            Phase = next;
            AppLog.Info(LogChannel.Game, $"[Battle] phase {previous} -> {next}");
            PhaseChanged?.Invoke(previous, next);
            return true;
        }

        /// <summary>主动暂停（切后台、以后的 ESC 菜单）。只有战斗中才生效。</summary>
        public bool RequestPause()
        {
            return SetPhase(BattlePhase.Paused);
        }

        /// <summary>从暂停恢复。</summary>
        public bool RequestResume()
        {
            return SetPhase(BattlePhase.Running);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                return;
            }

            // 手机切后台：回来时不该被怪白打几秒。
            if (Application.isMobilePlatform)
            {
                RequestPause();
            }
        }

        private void Update()
        {
            // 分配归因：帧首记录一次，帧尾再记一次，差值是「本帧 Update 内」的 GC，
            // 其余落在两者之间，就是场景里别的 MonoBehaviour 与 Unity 自身的开销。
            BattleAllocProbe.MarkFrameStart();

            // 黑边在任何阶段都要维持，否则窗口变化后暂停中也会露出画面外的部分。
            RefreshAspectGuard();

            if (Phase == BattlePhase.Running)
            {
                TickRunning();
            }

            BattleAllocProbe.MarkFrameEnd();
        }

        private void TickRunning()
        {
            // FixedStepSeconds > 0 时用固定步长（headless 数值回归 / 可复现冒烟），否则跟真实帧时间。
            var frameTime = FixedStepSeconds > 0f ? FixedStepSeconds : Time.deltaTime;
            var deltaTime = frameTime * Mathf.Max(0.01f, SimulationSpeed);
            if (deltaTime > 0.1f)
            {
                deltaTime = 0.1f;
            }

            // 规则层：一次 Tick 走完玩家位移 → 刷怪 → 敌人 → 接触 → 武器 → 弹体 → 拾取。
            var world = new SimulationWorld(
                this,
                this,
                this,
                _simulation.EnemyStates,
                _input,
                _walkable,
                Session.Elapsed < _freezeUntil,
                1.8f * Session.MagnetMultiplier);

            var worldAlloc = BattleAllocProbe.Begin();
            _simulation.Tick(deltaTime, world, _weapons);
            BattleAllocProbe.End("world+sim", worldAlloc);

            // 表现层：只把状态同步到 Transform / 精灵，不参与任何判定。
            var viewAlloc = BattleAllocProbe.Begin();
            SyncBattleViews(deltaTime);
            BattleAllocProbe.End("views", viewAlloc);

            if (Session.IsOver)
            {
                _ = EndAsync(Session.IsVictory);
                return;
            }

            if (Session.CanLevelUp)
            {
                _ = RunLevelUpAsync();
                return;
            }

            CheckRoundEnd();

            _hudTimer -= deltaTime;
            if (_hudTimer <= 0f)
            {
                _hudTimer = 0.1f;
                var hudAlloc = BattleAllocProbe.Begin();
                StateChanged?.Invoke();
                BattleAllocProbe.End("hud", hudAlloc);
            }
        }

        /// <summary>把本帧算好的状态同步到所有视图（敌人/弹体/拾取物）。</summary>
        private void SyncBattleViews(float deltaTime)
        {
            SyncEnemyViews(deltaTime);
            SyncProjectileViews();
            SyncPickupViews();
        }

        /// <summary>界面出口：由组合根注入（UI 层用 Toast，headless 用 <see cref="NullBattleNotifier"/>）。</summary>
        public IBattleNotifier Notifier { get; set; } = NullBattleNotifier.Instance;

        public void Toast(string message)
        {
            Notifier.Notify(message);
        }

        /// <summary>结算时清场：结算面板后面不该再留着敌人、弹体与掉落物。</summary>
        private void ClearBattlefield()
        {
            foreach (var view in _enemyViews.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _enemyViews.Clear();

            _projectileViews.ReturnAll(_projectiles);
            _pickupViews.ReturnAll(_pickups);
            _simulation?.ClearTransients();

            _weapons?.Dispose();
        }
    }
}
