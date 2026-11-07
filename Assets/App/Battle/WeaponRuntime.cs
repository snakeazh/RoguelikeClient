using System.Linq;
using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 武器系统：每帧驱动 4 把武器的开火逻辑。
    /// 目前实现 4 种：投掷物（长弓）、环绕（回旋刃）、追踪（魔法飞弹）、光环（权能法杖）。
    /// 大剑 / 魔法炮 / 雷电法典 / 定时炸弹按同样方式在 switch 里加分支即可。
    /// </summary>
    public sealed class WeaponRuntime
    {
        private const float MinCooldown = 0.12f;
        private const float BladeHitCooldown = 0.35f;

        private BattleRunner _runner;
        private BattleSession _session;
        private readonly List<Blade> _blades = new List<Blade>();
        private readonly List<Bomb> _bombs = new List<Bomb>();
        private readonly List<EnemyUnit> _chainVisited = new List<EnemyUnit>();
        private float _orbitAngle;
        private float _auraPulse;

        // 随身炮台（魔法炮 / 湮灭炮）：有 Mount=1 的武器时存在
        private CannonRig _cannon;
        private EnemyUnit _cannonTarget;

        // 炮口与目标夹角小于这个度数才开火（用真实的炮管角度，不是目标角度），避免"甩炮"
        private const float FireAlignment = 14f;

        // 炮口对不准时最多等这么久（秒），超时就先打出去
        private const float CannonFireGrace = 0.6f;

        // 大剑扇形刀光张开的总角度（度）。改这个就能调"刀光有多宽"。
        private const float CleaveSpread = 120f;

        private sealed class Blade
        {
            public Transform Transform;
            public float AngleOffset;
        }

        private sealed class Bomb
        {
            public Transform Transform;
            public float Fuse;
            public DamageInfo Damage;
            public float Radius;
            public Color Color;
        }

        public void Init(BattleRunner runner, BattleSession session)
        {
            _runner = runner;
            _session = session;
            _orbitAngle = 0f;
            ClearBlades();
        }

        public void Dispose()
        {
            ClearBlades();
            foreach (var bomb in _bombs)
            {
                if (bomb.Transform != null)
                {
                    Object.Destroy(bomb.Transform.gameObject);
                }
            }

            _bombs.Clear();
            DestroyCannon();
        }

        public void Tick(float deltaTime)
        {
            if (_session == null)
            {
                return;
            }

            UpdateCannon(deltaTime);

            var orbitCount = 0;
            for (var i = 0; i < _session.Weapons.Count; i++)
            {
                var weapon = _session.Weapons[i];
                if (weapon.Def.Kind == WeaponKind.Orbit)
                {
                    orbitCount += BladeCount(weapon);
                }

                TickWeapon(weapon, deltaTime);
            }

            SyncBlades(orbitCount);
            TickBlades(deltaTime);
            TickBombs(deltaTime);
            TickAuraVisual();
        }

        private void TickWeapon(WeaponInstance weapon, float deltaTime)
        {
            switch (weapon.Def.Kind)
            {
                case WeaponKind.Projectile:
                case WeaponKind.Homing:
                    TickShooter(weapon, deltaTime);
                    break;

                case WeaponKind.Aura:
                    TickAura(weapon, deltaTime);
                    break;

                case WeaponKind.Cleave:
                    TickCleave(weapon, deltaTime);
                    break;

                case WeaponKind.Chain:
                    TickChain(weapon, deltaTime);
                    break;

                case WeaponKind.Bomb:
                    TickBomb(weapon, deltaTime);
                    break;

                case WeaponKind.Orbit:
                    // 环绕武器没有开火计时，伤害在 TickBlades 里结算
                    break;
            }
        }

        /// <summary>
        /// 维护随身炮台：有 Mount=1 的武器就造一台并在玩家身边转向最近的敌人、
        /// 同步蓄能进度；没有这种武器就拆掉。
        /// </summary>
        private void UpdateCannon(float deltaTime)
        {
            var cannonWeapon = FindCannonWeapon();
            if (cannonWeapon == null)
            {
                DestroyCannon();
                return;
            }

            var player = _runner != null ? _runner.Player : null;
            if (player == null)
            {
                return;
            }

            if (_cannon == null)
            {
                _cannon = CannonRig.Create(player.transform);
            }

            // 目标锁定带一点"粘性"：只要当前目标还活着且没跑太远就继续打它，
            // 否则两只距离相近的敌人会让炮管来回摆动。
            var range = cannonWeapon.Def.Range;
            if (!IsCannonTargetValid(_cannonTarget, range * 1.25f))
            {
                _cannonTarget = FindNearestEnemy(range);
            }

            var target = _cannonTarget;
            var aim = target != null
                ? ((Vector2)target.transform.position - (Vector2)player.transform.position).normalized
                : _cannon.AimDirection;
            _cannon.AimAt(aim, deltaTime);

            if (target == null)
            {
                // 没有目标就别亮着炮口，避免"空蓄能"的假象
                _cannon.SetCharge(0f);
                return;
            }

            var cooldown = CooldownOf(cannonWeapon);
            var charge = cooldown <= 0.0001f
                ? 0f
                : 1f - Mathf.Clamp01(cannonWeapon.CooldownTimer / cooldown);
            _cannon.SetCharge(charge);
        }

        private bool IsCannonTargetValid(EnemyUnit target, float range)
        {
            if (target == null || _runner == null || _runner.Player == null)
            {
                return false;
            }

            var distance = Vector2.Distance(target.transform.position, _runner.Player.transform.position);
            return distance <= range;
        }

        private WeaponInstance FindCannonWeapon()
        {
            for (var i = 0; i < _session.Weapons.Count; i++)
            {
                var weapon = _session.Weapons[i];
                if (weapon.Def != null && weapon.Def.Mount == 1)
                {
                    return weapon;
                }
            }

            return null;
        }

        private void DestroyCannon()
        {
            if (_cannon == null)
            {
                return;
            }

            Object.Destroy(_cannon.gameObject);
            _cannon = null;
            _cannonTarget = null;
        }

        /// <summary>武器对应的弹体精灵（看 json 的 Sprite 字段，留空用默认弹体）。</summary>
        private static string ProjectileSprite(WeaponDef def)
        {
            return string.IsNullOrEmpty(def.Sprite)
                ? BattleVisuals.ProjectileFallback
                : "Battle/Sprites/" + def.Sprite;
        }

        private float CooldownOf(WeaponInstance weapon)        {
            var levelScale = 1f - (weapon.Level - 1) * weapon.Def.CooldownPerLevel;
            return Mathf.Max(MinCooldown, weapon.Def.Cooldown * levelScale * _session.CooldownMultiplier);
        }

        private float DamageOf(WeaponInstance weapon)
        {
            var levelScale = 1f + (weapon.Level - 1) * weapon.Def.DamagePerLevel;
            return weapon.Def.Damage * levelScale * _session.DamageMultiplier;
        }

        private float AreaOf(WeaponInstance weapon, float baseValue)
        {
            var levelScale = 1f + (weapon.Level - 1) * weapon.Def.AreaPerLevel;
            return baseValue * levelScale * _session.AreaMultiplier;
        }

        private int CountOf(WeaponInstance weapon)
        {
            return Mathf.Max(
                1,
                weapon.Def.Projectiles + (weapon.Level - 1) * weapon.Def.CountPerLevel + _session.ExtraProjectiles);
        }

        private int BladeCount(WeaponInstance weapon)
        {
            return Mathf.Max(1, weapon.Def.Projectiles + (weapon.Level - 1) * weapon.Def.CountPerLevel);
        }

        private void TickShooter(WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var target = FindNearestEnemy(weapon.Def.Range);
            if (target == null)
            {
                // 没有目标时停在"可开火"状态，出现目标立刻打
                weapon.ClearCooldown();
                return;
            }

            // 有炮台的武器（魔法炮 / 湮灭炮）：等炮管真的转到位了再从炮口打出去，
            // 这样视觉上永远是「慢慢转过去 → 蓄满 → 开炮」，不会出现瞬间甩头。
            var mount = weapon.Def.Mount == 1 ? _cannon : null;
            if (mount != null)
            {
                var toTarget = (Vector2)target.transform.position - mount.MuzzlePosition;
                // 转到位再开火；最多等 0.6 秒，避免目标一直绕圈时这门炮永远不开火
                if (Vector2.Angle(mount.BarrelDirection, toTarget) > FireAlignment
                    && weapon.CooldownTimer > -CannonFireGrace)
                {
                    return;
                }
            }

            weapon.ResetCooldown(CooldownOf(weapon));

            Vector2 origin;
            Vector2 aim;
            if (mount != null)
            {
                origin = mount.MuzzlePosition;
                aim = ((Vector2)target.transform.position - origin).normalized;
            }
            else
            {
                origin = (Vector2)_runner.Player.transform.position;
                aim = ((Vector2)target.transform.position - origin).normalized;
            }

            var count = CountOf(weapon);
            var spread = count > 1 ? 22f : 0f;

            for (var i = 0; i < count; i++)
            {
                var offset = count > 1 ? Mathf.Lerp(-spread, spread, i / (float)(count - 1)) : 0f;
                var dir = Rotate(aim, offset);
                var damage = RollDamage(DamageOf(weapon));
                _runner.SpawnPlayerProjectile(
                    origin,
                    dir,
                    weapon.Def.Speed,
                    damage,
                    weapon.Def.ProjectileRadius,
                    weapon.Def.Pierce,
                    weapon.Def.SplashRadius,
                    weapon.Def.Duration,
                    weapon.Def.Kind == WeaponKind.Homing ? target : null,
                    ProjectileSprite(weapon.Def));
            }

            if (mount != null)
            {
                mount.Kick();
            }
        }

        private void TickAura(WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            weapon.ResetCooldown(CooldownOf(weapon));

            var radius = AreaOf(weapon, weapon.Def.Area);
            var damage = RollDamage(DamageOf(weapon));
            var origin = (Vector2)_runner.Player.transform.position;
            var slow = 0.6f;

            foreach (var enemy in _runner.Enemies.ToArray())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                if (Vector2.Distance(origin, enemy.transform.position) <= radius + enemy.Radius)
                {
                    _runner.DamageEnemy(enemy, damage);
                    enemy.ApplySlow(slow);
                }
            }
        }

        /// <summary>
        /// 大剑：只朝玩家当前面向挥出扇形刀光（不自动索敌），命中扇形内的所有敌人。
        /// 想改成"自动瞄准最近敌人"就解开下面的注释。
        /// </summary>
        private void TickCleave(WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var player = _runner.Player;
            if (player == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(CooldownOf(weapon));

            var origin = (Vector2)player.transform.position;
            var aim = player.FacingDirection;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.up;
            }

            aim.Normalize();

            var angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var radius = AreaOf(weapon, weapon.Def.Area);
            var halfAngle = CleaveSpread * 0.5f;
            var damage = RollDamage(DamageOf(weapon));

            var hits = new List<EnemyUnit>();
            foreach (var enemy in _runner.Enemies.ToArray())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                var offset = (Vector2)enemy.transform.position - origin;
                if (offset.magnitude > radius + enemy.Radius)
                {
                    continue;
                }

                if (offset.sqrMagnitude > 0.0001f && Vector2.Angle(offset, aim) > halfAngle)
                {
                    continue;
                }

                hits.Add(enemy);
            }

            foreach (var enemy in hits)
            {
                _runner.DamageEnemy(enemy, damage);
            }

            TransientFx.Fan(origin, angle, radius * 1.05f, CleaveSpread, new Color(1f, 0.97f, 0.86f, 1f), 0.26f);
        }

        /// <summary>雷电法典：先打最近的敌人，再在敌人之间跳跃，每次跳伤害衰减。</summary>
        private void TickChain(WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var first = FindNearestEnemy(weapon.Def.Range);
            if (first == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(CooldownOf(weapon));

            var jumps = Mathf.Clamp(1 + CountOf(weapon), 1, 8);
            var chainRadius = Mathf.Max(1.2f, AreaOf(weapon, weapon.Def.Area));
            var damage = DamageOf(weapon);
            var origin = (Vector2)_runner.Player.transform.position;

            _chainVisited.Clear();
            var current = first;
            var boltFrom = (Vector3)origin + Vector3.up * 0.3f;
            for (var i = 0; i < jumps && current != null; i++)
            {
                _chainVisited.Add(current);

                // 电弧：玩家 -> 第一个敌人，之后每跳在敌人之间连一段，越靠后越暗
                var target = current.transform.position;
                var boltColor = new Color(0.68f, 0.88f, 1f, Mathf.Lerp(1f, 0.55f, i / (float)jumps));
                LightningFx.Bolt(boltFrom, target, boltColor, 0.15f + i * 0.015f);
                boltFrom = target;

                _runner.DamageEnemy(current, RollDamage(damage));

                TransientFx.Flash(current.transform.position, current.Radius * 2.4f, new Color(0.7f, 0.85f, 1f), 0.16f);

                damage *= 0.85f;
                current = FindChainNext(current, origin, weapon.Def.Range, chainRadius);
            }
        }

        private EnemyUnit FindChainNext(EnemyUnit from, Vector2 origin, float range, float chainRadius)
        {
            EnemyUnit best = null;
            var bestDistance = float.MaxValue;
            var fromPosition = (Vector2)from.transform.position;

            foreach (var enemy in _runner.Enemies)
            {
                if (enemy.IsDead || _chainVisited.Contains(enemy))
                {
                    continue;
                }

                if (Vector2.Distance(origin, enemy.transform.position) > range)
                {
                    continue;
                }

                var distance = Vector2.Distance(fromPosition, enemy.transform.position);
                if (distance > chainRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }

        /// <summary>定时炸弹：朝敌人脚下抛雷，引信结束后范围爆炸。</summary>
        private void TickBomb(WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var target = FindNearestEnemy(weapon.Def.Range);
            if (target == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(CooldownOf(weapon));

            var count = CountOf(weapon);
            var damage = DamageOf(weapon);
            var radius = Mathf.Max(0.6f, AreaOf(weapon, weapon.Def.SplashRadius > 0f ? weapon.Def.SplashRadius : weapon.Def.Area));
            var center = (Vector2)target.transform.position;

            for (var i = 0; i < count; i++)
            {
                var offset = count > 1
                    ? Random.insideUnitCircle * 0.9f
                    : Random.insideUnitCircle * 0.25f;
                var position = center + offset;

                var bombGo = new GameObject("Bomb");
                bombGo.transform.SetParent(_runner.transform, false);
                bombGo.transform.position = position;
                BattleVisuals.Body(bombGo.transform, "Battle/Sprites/proj_bomb", Color.white, 0.34f, BattleSort.Bomb, PlaceholderArt.Square);

                _bombs.Add(new Bomb
                {
                    Transform = bombGo.transform,
                    Fuse = Mathf.Max(0.2f, weapon.Def.Duration),
                    Damage = RollDamage(damage),
                    Radius = radius,
                    Color = new Color(1f, 0.6f, 0.2f)
                });
            }
        }

        private void TickBombs(float deltaTime)
        {
            for (var i = _bombs.Count - 1; i >= 0; i--)
            {
                var bomb = _bombs[i];
                bomb.Fuse -= deltaTime;

                if (bomb.Transform != null)
                {
                    var pulse = 1f + Mathf.Sin(Time.time * 26f) * 0.16f;
                    bomb.Transform.localScale = Vector3.one * pulse;
                }

                if (bomb.Fuse > 0f)
                {
                    continue;
                }

                var position = bomb.Transform == null ? Vector3.zero : bomb.Transform.position;
                foreach (var enemy in _runner.Enemies.ToArray())
                {
                    if (enemy.IsDead)
                    {
                        continue;
                    }

                    if (Vector2.Distance(position, enemy.transform.position) <= bomb.Radius + enemy.Radius)
                    {
                        _runner.DamageEnemy(enemy, bomb.Damage);
                    }
                }

                TransientFx.Flash(position, bomb.Radius * 2f, bomb.Color, 0.22f);

                if (bomb.Transform != null)
                {
                    Object.Destroy(bomb.Transform.gameObject);
                }

                _bombs.RemoveAt(i);
            }
        }

        private void SyncBlades(int desired)
        {
            while (_blades.Count > desired)
            {
                var last = _blades[_blades.Count - 1];
                if (last.Transform != null)
                {
                    Object.Destroy(last.Transform.gameObject);
                }

                _blades.RemoveAt(_blades.Count - 1);
            }

            while (_blades.Count < desired)
            {
                var bladeWeapon = FindOrbitWeapon(_blades.Count);
                var size = bladeWeapon == null ? 0.34f : AreaOf(bladeWeapon, bladeWeapon.Def.Area);
                var bladeGo = new GameObject("Blade");
                bladeGo.transform.SetParent(_runner.transform, false);
                var sr = BattleVisuals.Body(
                    bladeGo.transform,
                    BattleVisuals.Blade,
                    Color.white,
                    size * 3.0f,
                    BattleSort.Blade,
                    PlaceholderArt.Square);

                _blades.Add(new Blade
                {
                    Transform = bladeGo.transform,
                    AngleOffset = _blades.Count * Mathf.PI * 2f / Mathf.Max(1, desired)
                });
            }
        }

        private WeaponInstance FindOrbitWeapon(int index)
        {
            foreach (var weapon in _session.Weapons)
            {
                if (weapon.Def.Kind == WeaponKind.Orbit)
                {
                    return weapon;
                }
            }

            return null;
        }

        private void TickBlades(float deltaTime)
        {
            if (_blades.Count == 0 || _runner.Player == null)
            {
                return;
            }

            var weapon = FindOrbitWeapon(0);
            if (weapon == null)
            {
                return;
            }

            var radius = AreaOf(weapon, weapon.Def.Range);
            var speed = weapon.Def.Speed;
            _orbitAngle += speed * deltaTime;
            var origin = (Vector2)_runner.Player.transform.position;

            for (var i = 0; i < _blades.Count; i++)
            {
                var angle = _orbitAngle + _blades[i].AngleOffset;
                var position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                _blades[i].Transform.position = position;

                // 刀刃朝外（贴图默认朝上，所以减 90°），绕玩家转起来才像风车而不是一群静止的贴图
                _blades[i].Transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);

                var damage = RollDamage(DamageOf(weapon));
                foreach (var enemy in _runner.Enemies.ToArray())
                {
                    if (enemy.IsDead || Time.time < enemy.NextBladeHitTime)
                    {
                        continue;
                    }

                    if (Vector2.Distance(position, enemy.transform.position) <= weapon.Def.Area + enemy.Radius)
                    {
                        enemy.NextBladeHitTime = Time.time + BladeHitCooldown;
                        _runner.DamageEnemy(enemy, damage);
                    }
                }
            }
        }

        private void TickAuraVisual()
        {
            var player = _runner.Player;
            if (player == null)
            {
                return;
            }

            WeaponInstance aura = null;
            foreach (var weapon in _session.Weapons)
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

        /// <summary>
        /// 伤害骰：暴击则翻倍，数值与暴击标记一起装进 <see cref="DamageInfo"/> 返回。
        /// 调用方直接把它交给 <c>DamageEnemy</c>，不用再关心求值顺序。
        /// </summary>
        private DamageInfo RollDamage(float damage)
        {
            var crit = _session.CritChance > 0f && Random.value < _session.CritChance;
            return new DamageInfo(crit ? damage * 2f : damage, crit);
        }

        private EnemyUnit FindNearestEnemy(float range)
        {
            EnemyUnit best = null;
            var bestScore = float.MaxValue;
            var origin = (Vector2)_runner.Player.transform.position;

            foreach (var enemy in _runner.Enemies)
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                var distance = Vector2.Distance(origin, enemy.transform.position);
                if (distance > range)
                {
                    continue;
                }

                // Boss / 精英加权：不然武器永远在打身边的小怪，Boss 到时间都打不掉。
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

        private void ClearBlades()
        {
            foreach (var blade in _blades)
            {
                if (blade.Transform != null)
                {
                    Object.Destroy(blade.Transform.gameObject);
                }
            }

            _blades.Clear();
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
        }
    }
}
