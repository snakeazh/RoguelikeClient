using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 武器系统的"调度层"：每帧把每把武器交给它对应形态的 <see cref="IWeaponBehavior"/> 推进，
    /// 并提供各形态共用的数值换算（等级 / 被动加成）与索敌。
    ///
    /// 具体开火逻辑在 <c>ShooterBehavior / AuraBehavior / CleaveBehavior / OrbitBehavior / ChainBehavior / BombBehavior</c>，
    /// 新增形态不用动这个文件。
    /// </summary>
    public sealed class WeaponRuntime : ISimulationStep
    {
        public const float MinCooldown = 0.12f;

        private IBattleContext _runner;
        private BattleSession _session;

        public WeaponRuntime()
        {
            Cannon = new CannonMount(this);
        }

        /// <summary>随身炮台（魔法炮 / 湮灭炮），没有 Mount 武器时不存在。</summary>
        public CannonMount Cannon { get; }

        public IBattleContext Context => _runner;

        public BattleSession Session => _session;

        public PlayerUnit Player => _runner != null ? _runner.Player : null;

        public Vector2 PlayerPosition =>
            _runner != null && _runner.Player != null ? (Vector2)_runner.Player.transform.position : Vector2.zero;

        public void Init(IBattleContext runner, BattleSession session)
        {
            _runner = runner;
            _session = session;
        }

        public void Dispose()
        {
            foreach (var weapon in _session.AllWeapons)
            {
                WeaponBehaviors.Get(weapon.Def.Kind).Dispose(this, weapon);
            }

            Cannon.Dispose();
        }

        public void Tick(float deltaTime)
        {
            if (_session == null)
            {
                return;
            }

            Cannon.Tick(deltaTime, FindCannonWeapon());

            // 本命武器在 AllWeapons 末尾，和掉落武器走同一套形态逻辑，不占槽位
            foreach (var weapon in _session.AllWeapons)
            {
                WeaponBehaviors.Get(weapon.Def.Kind).Tick(this, weapon, deltaTime);
            }

            TickAuraVisual();
        }

        // ---------- 各形态共用的数值换算 ----------

        public float CooldownOf(WeaponInstance weapon)
        {
            var levelScale = 1f - (weapon.Level - 1) * weapon.Def.CooldownPerLevel;
            var innate = weapon.IsInnate ? BattleSession.InnateCooldownMultiplier : 1f;
            return Mathf.Max(MinCooldown, weapon.Def.Cooldown * levelScale * innate * _session.CooldownMultiplier);
        }

        public float DamageOf(WeaponInstance weapon)
        {
            var levelScale = 1f + (weapon.Level - 1) * weapon.Def.DamagePerLevel;
            var innate = weapon.IsInnate ? BattleSession.InnateDamageMultiplier : 1f;
            return weapon.Def.Damage * levelScale * innate * _session.DamageMultiplier;
        }

        public float AreaOf(WeaponInstance weapon, float baseValue)
        {
            var levelScale = 1f + (weapon.Level - 1) * weapon.Def.AreaPerLevel;
            return baseValue * levelScale * _session.AreaMultiplier;
        }

        public int CountOf(WeaponInstance weapon)
        {
            return Mathf.Max(
                1,
                weapon.Def.Projectiles + (weapon.Level - 1) * weapon.Def.CountPerLevel + _session.ExtraProjectiles);
        }

        public int BladeCount(WeaponInstance weapon)
        {
            return Mathf.Max(1, weapon.Def.Projectiles + (weapon.Level - 1) * weapon.Def.CountPerLevel);
        }

        /// <summary>
        /// 伤害骰：暴击则翻倍，数值与暴击标记一起装进 <see cref="DamageInfo"/> 返回。
        /// 调用方直接把它交给 <c>DamageEnemy</c>，不用再关心求值顺序。
        /// </summary>
        public DamageInfo RollDamage(float damage)
        {
            var crit = _session.CritChance > 0f && _session.Rng.NextDouble() < _session.CritChance;
            return new DamageInfo(crit ? damage * 2f : damage, crit);
        }

        /// <summary>索敌：取射程内"距离 / 权重"最小的敌人，Boss 与精英加权（不然永远在打身边小怪）。</summary>
        public EnemyState FindNearestEnemy(float range)
        {
            if (_runner == null || _runner.Player == null)
            {
                return null;
            }

            EnemyState best = null;
            var bestScore = float.MaxValue;
            var origin = _runner.Player.State.Position;

            foreach (var enemy in _runner.Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, enemy.Position);
                if (distance > range)
                {
                    continue;
                }

                var weight = enemy.IsBoss ? 2.5f : enemy.IsElite ? 1.6f : 1f;
                var score = distance / weight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = enemy;
                }
            }

            return best;
        }

        /// <summary>武器对应的弹体精灵（看 json 的 Sprite 字段，留空用默认弹体）。</summary>
        public static string ProjectileSprite(WeaponDef def)
        {
            return string.IsNullOrEmpty(def.Sprite)
                ? BattleVisuals.ProjectileFallback
                : "Battle/Sprites/" + def.Sprite;
        }

        public static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }

        // ---------- 光环视觉 ----------

        private void TickAuraVisual()
        {
            var player = Player;
            if (player == null)
            {
                return;
            }

            WeaponInstance aura = null;
            foreach (var weapon in _session.AllWeapons)
            {
                if (weapon.Def.Kind == WeaponKind.Aura)
                {
                    aura = weapon;
                    break;
                }
            }

            if (aura == null)
            {
                player.SetAuraVisible(false);
                return;
            }

            player.SetAuraVisible(true);
            player.SetAuraRadius(AreaOf(aura, aura.Def.Area));
        }

        private WeaponInstance FindCannonWeapon()
        {
            foreach (var weapon in _session.AllWeapons)
            {
                if (weapon.Def != null && weapon.Def.Mount == 1)
                {
                    return weapon;
                }
            }

            return null;
        }
    }
}
