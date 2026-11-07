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
    public sealed partial class BattleRunner : MonoBehaviour
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
    }
}
