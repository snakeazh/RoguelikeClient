using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一帧战斗的**编排者**（P3-1b 第 5 步）。
    ///
    /// 只做两件事：按固定顺序调用各子系统、持有它们；**不写任何规则**。
    /// 每个子系统负责一件事（玩家位移 / 刷怪 / 敌人行为 / 接触伤害 / 武器 / 弹体 / 拾取），
    /// 规则改动落在对应子系统里，这个文件应该长期保持在 120 行左右不再变长。
    ///
    /// 依赖方向：BattleRunner（表现宿主）→ BattleSimulation → 各 System → State。
    /// 表现层只读 <see cref="Player"/> 与各 State，模拟层不认识 Transform / 相机 / HUD。
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly PlayerSim _playerSim = new PlayerSim();
        private readonly EnemySystem _enemySystem = new EnemySystem();
        private readonly ContactSystem _contactSystem = new ContactSystem();
        private readonly ProjectileSystem _projectiles = new ProjectileSystem();
        private readonly PickupSystem _pickups;
        private readonly SpawnSystem _spawns;

        /// <summary>敌人状态列表：模拟层唯一权威，表现层只读它来维护视图。</summary>
        private readonly List<EnemyState> _enemies = new List<EnemyState>(256);


        public BattleSimulation(
            BattleSession session,
            Vector2 playerStart,
            IWaveStepRegistry waves,
            IEnumerable<WaveDef> waveDefs,
            PickupEffects pickupEffects)
        {
            Session = session;
            Player = new PlayerState { Position = playerStart };
            Spawner = new EnemySpawner(waves, waveDefs);
            _spawns = new SpawnSystem(session.Rng);
            _pickups = new PickupSystem(pickupEffects);
        }

        public BattleSession Session { get; }

        /// <summary>玩家逻辑状态：只有 <see cref="PlayerSim"/> 写它。</summary>
        public PlayerState Player { get; }

        public EnemySpawner Spawner { get; }

        /// <summary>活着的敌人状态（权威列表）。</summary>
        public IReadOnlyList<EnemyState> Enemies => _enemies;

        /// <summary>给模拟编排用的可变视图（只有 Runner 组帧时读取，外部不要长期持有）。</summary>
        internal List<EnemyState> EnemyStates => _enemies;

        /// <summary>本帧死亡的敌人（表现层销毁视图用）。</summary>
        public IReadOnlyList<EnemyState> RemovedEnemies => _enemySystem.Removed;

        /// <summary>当前地上的拾取物（表现层按它同步视图）。</summary>
        public IReadOnlyList<PickupState> ActivePickups => _pickups.States;

        /// <summary>玩家弹体：规则状态由 <see cref="ProjectileSystem"/> 生成，视图由表现层补。</summary>
        public ProjectileState SpawnPlayerProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            EnemyState homingTarget)
        {
            return _projectiles.SpawnPlayer(
                position, direction, speed, damage, radius, pierce, splash, life, 0f, homingTarget);
        }

        public ProjectileState SpawnEnemyProjectile(
            Vector3 position, Vector2 direction, float speed, float damage, float radius)
        {
            return _projectiles.SpawnEnemy(position, direction, speed, damage, radius);
        }

        public void Tick(float deltaTime, in SimulationWorld world, ISimulationStep weapons)
        {
            // 顺序与拆分前一致，别随意调整：它会改变随机流与手感。
            _playerSim.Tick(Player, Session, world.Input, deltaTime, world.Walkable);
            Session.TickTime(deltaTime);

            // 刷怪用整场上下文（波次要能刷怪/刷 Boss/掉补给/报警），敌人规则只读敌人上下文。
            Spawner.Tick(deltaTime, Session.Elapsed, world.Battle);
            _enemySystem.Tick(deltaTime, world.Enemies, _enemies, world.Frozen);
            _contactSystem.Tick(deltaTime, world.Enemies, _enemies);

            weapons.Tick(deltaTime);

            _projectiles.Tick(deltaTime, world.Battle);
            _pickups.Tick(deltaTime, Player.Position, true, world.MagnetRadius, world.Pickups);

        }

        /// <summary>按出生规则刷一只敌人（<see cref="SpawnSystem"/> 决定数值与点位）。</summary>
        public EnemyState SpawnEnemy(int defId, Vector2 position, EliteAffix affix, Rect walkable)
        {
            var state = _spawns.Create(defId, position, affix, Session.Elapsed, walkable);
            if (state != null)
            {
                _enemies.Add(state);
            }

            return state;
        }

        public EnemyState SpawnBoss(int defId, Vector2 position, Rect walkable)
        {
            var state = _spawns.CreateBoss(defId, position, walkable);
            if (state != null)
            {
                _enemies.Add(state);
            }

            return state;
        }

        /// <summary>生成一枚拾取物（掉落由武器/敌人规则决定，这里只负责落地）。</summary>
        public PickupState SpawnPickup(PickupDef def, int amount, Vector2 position)
        {
            return _pickups.Spawn(def, amount, position);
        }

        /// <summary>清场：结算面板后面不该再留着弹体与掉落物。</summary>
        public void ClearTransients()
        {
            _enemies.Clear();
            _projectiles.Clear();
            _pickups.Clear();
        }
    }

    /// <summary>参与模拟的子系统（目前只有武器）。加进来是为了让 <see cref="BattleSimulation"/> 保持纯编排。</summary>
    public interface ISimulationStep
    {
        void Tick(float deltaTime);
    }
}
