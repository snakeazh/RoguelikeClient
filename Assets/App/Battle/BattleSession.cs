using System;
using System.Collections.Generic;
using App.Config;
using Framework.Log;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一局战斗的全部运行时状态（纯 C#，不碰 Unity 对象）。
    /// 数值来源：Assets/Res/Config/Battle/*.json，通过 ConfigTables 加载。
    ///
    /// 这里只保存"已经算好的结果"，具体伤害/冷却由 BattleRunner 与 WeaponRuntime 读取本类属性。
    /// </summary>
    public sealed class BattleSession
    {
        /// <summary>单局时长（秒）。</summary>
        public const float RoundDuration = 300f;

        public const int MaxWeaponSlots = 4;
        public const int MaxPassiveSlots = 4;
        public const int MaxLevel = 20;

        /// <summary>最终 Boss 未击杀时的狂暴时间点（秒）。</summary>
        public const float BossRageTime = BossSpawnTime + 30f;

        /// <summary>最终 Boss 出场时间（秒），与 Waves.json 的最后一行保持一致。</summary>
        public const float BossSpawnTime = 268f;

        /// <summary>Boss 的怪物表 Id。</summary>
        public const int BossEnemyId = 100;

        private readonly List<WeaponInstance> _weapons = new List<WeaponInstance>(MaxWeaponSlots);
        private readonly List<PassiveInstance> _passives = new List<PassiveInstance>(MaxPassiveSlots);

        public float Elapsed { get; private set; }
        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public float Hp { get; private set; }
        public int Kills { get; private set; }
        public int Gold { get; private set; }
        public bool IsOver { get; private set; }
        public bool IsVictory { get; private set; }
        public bool IsPaused { get; set; }

        /// <summary>用于三选一随机与掉落随机，保证同 seed 可复现。</summary>
        public System.Random Rng { get; }

        public IReadOnlyList<WeaponInstance> Weapons => _weapons;
        public IReadOnlyList<PassiveInstance> Passives => _passives;

        public BattleSession(int seed = 0)
        {
            Rng = seed == 0 ? new System.Random() : new System.Random(seed);
            Hp = MaxHp;
        }

        // ---------- 派生属性（被动叠加）----------
        public float MaxHp => 100f * (1f + PassiveValue(PassiveKind.MaxHp));

        public float MoveSpeed => 5f * (1f + PassiveValue(PassiveKind.MoveSpeed));

        public float DamageMultiplier => 1f + PassiveValue(PassiveKind.Damage);

        public float CooldownMultiplier => Math.Max(0.3f, 1f - PassiveValue(PassiveKind.Cooldown));

        public int ExtraProjectiles => (int)PassiveValue(PassiveKind.ProjectileCount);

        public float AreaMultiplier => 1f + PassiveValue(PassiveKind.Area);

        public float Armor => PassiveValue(PassiveKind.Armor);

        public float CritChance => PassiveValue(PassiveKind.Crit);

        public float MagnetMultiplier => 1f + PassiveValue(PassiveKind.Magnet);

        public int Luck => (int)PassiveValue(PassiveKind.Luck);

        public int ExpToNext
        {
            get
            {
                if (Level >= MaxLevel)
                {
                    return 0;
                }

                var row = LevelDef.Get(Level);
                return row == null ? 9999 : Math.Max(1, row.ExpToNext);
            }
        }

        public float ExpRatio => ExpToNext <= 0 ? 1f : Mathf.Clamp01(Exp / (float)ExpToNext);

        public float HpRatio => MaxHp <= 0 ? 0f : Mathf.Clamp01(Hp / MaxHp);

        public float TimeLeft => Mathf.Max(0f, RoundDuration - Elapsed);

        /// <summary>Boss 是否已经出场（用于 HUD 提示与胜负判定）。</summary>
        public bool BossPhase => Elapsed >= BossSpawnTime;

        // ---------- 被动查询 ----------
        public PassiveInstance FindPassive(int passiveId)
        {
            for (var i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].Def.Id == passiveId)
                {
                    return _passives[i];
                }
            }

            return null;
        }

        public float PassiveValue(PassiveKind kind)
        {
            var sum = 0f;
            for (var i = 0; i < _passives.Count; i++)
            {
                if (_passives[i].Def.Kind == kind)
                {
                    sum += _passives[i].Value;
                }
            }

            return sum;
        }

        // ---------- 经验与等级 ----------
        public bool AddExp(int amount)
        {
            if (amount <= 0 || IsOver || Level >= MaxLevel)
            {
                Exp += amount;
                return false;
            }

            Exp += amount;
            return Exp >= ExpToNext;
        }

        /// <summary>消耗一次升级（一次只升一级，多出来的经验留到下一次）。</summary>
        public bool TryLevelUp()
        {
            if (Level >= MaxLevel || Exp < ExpToNext)
            {
                return false;
            }

            Exp -= ExpToNext;
            Level++;
            AppLog.Info(LogChannel.Game, $"[Battle] level up -> {Level}");
            return true;
        }

        public bool CanLevelUp => Level < MaxLevel && Exp >= ExpToNext;

        // ---------- 受伤与治疗 ----------
        public void ApplyDamage(float raw)
        {
            if (IsOver)
            {
                return;
            }

            var damage = Math.Max(1f, raw - Armor);
            Hp -= damage;
            if (Hp <= 0f)
            {
                Hp = 0f;
                IsOver = true;
                IsVictory = false;
                AppLog.Info(LogChannel.Game, "[Battle] player died");
            }
        }

        public void Heal(float amount)
        {
            Hp = Math.Min(MaxHp, Hp + amount);
        }

        public void HealPercent(float percent) => Heal(MaxHp * percent);

        public void AddKill()
        {
            Kills++;
        }

        public void AddGold(int amount) => Gold += amount;

        /// <summary>立刻结束（胜利或失败），由 BattleRunner 调用。</summary>
        public void Finish(bool victory)
        {
            if (IsOver)
            {
                return;
            }

            IsOver = true;
            IsVictory = victory;
        }

        public void TickTime(float deltaTime) => Elapsed += deltaTime;

        // ---------- 武器/被动添加 ----------
        public bool HasWeapon(int weaponId)
        {
            for (var i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Def.Id == weaponId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanAddWeapon => _weapons.Count < MaxWeaponSlots;

        public bool CanAddPassive => _passives.Count < MaxPassiveSlots;

        public WeaponInstance AddWeapon(WeaponDef def)
        {
            if (!CanAddWeapon || def == null)
            {
                return null;
            }

            var instance = new WeaponInstance(def);
            _weapons.Add(instance);
            AppLog.Info(LogChannel.Game, $"[Battle] weapon +{def.Name}");
            return instance;
        }

        public PassiveInstance AddPassive(PassiveDef def)
        {
            if (!CanAddPassive || def == null)
            {
                return null;
            }

            var instance = new PassiveInstance(def);
            _passives.Add(instance);
            AppLog.Info(LogChannel.Game, $"[Battle] passive +{def.Name}");
            return instance;
        }

        /// <summary>武器是否满足进化条件：本身满级、有进化目标、配套被动也满级。</summary>
        public bool CanEvolve(WeaponInstance weapon)
        {
            if (weapon == null || weapon.Def == null || !weapon.IsMaxLevel || weapon.Def.EvolvedId <= 0)
            {
                return false;
            }

            var paired = FindPassive(weapon.Def.PassiveId);
            return paired != null && paired.IsMaxLevel;
        }

        /// <summary>找一把当前可进化的武器（宝箱用）。</summary>
        public WeaponInstance FindEvolvable()
        {
            foreach (var weapon in _weapons)
            {
                if (CanEvolve(weapon))
                {
                    return weapon;
                }
            }

            return null;
        }

        /// <summary>执行进化，返回进化后的定义（失败返回 null）。</summary>
        public WeaponDef Evolve(WeaponInstance weapon)
        {
            if (!CanEvolve(weapon))
            {
                return null;
            }

            var evolved = WeaponDef.Get(weapon.Def.EvolvedId);
            if (evolved == null)
            {
                return null;
            }

            var previous = weapon.Def.Name;
            weapon.Evolve(evolved);
            AppLog.Info(LogChannel.Game, $"[Battle] evolve {previous} -> {evolved.Name}");
            return evolved;
        }
    }

    /// <summary>一把武器的运行时实例（等级 + 开火计时）。</summary>
    public sealed class WeaponInstance
    {
        public WeaponDef Def { get; private set; }
        public int Level { get; private set; } = 1;
        public float CooldownTimer { get; set; }
        public bool IsMaxLevel => Level >= Def.MaxLevel;
        public bool IsEvolved => Def != null && Def.Id >= 100;
        public WeaponInstance(WeaponDef def) => Def = def;
        public bool TryUpgrade()
        {
            if (IsMaxLevel)
            {
                return false;
            }

            Level++;
            return true;
        }

        /// <summary>进化：直接把定义换成进化后的武器，等级保持满级。</summary>
        public void Evolve(WeaponDef evolvedDef)
        {
            if (evolvedDef == null)
            {
                return;
            }

            Def = evolvedDef;
            Level = evolvedDef.MaxLevel;
            CooldownTimer = 0f;
        }
    }

    /// <summary>一个被动的运行时实例。</summary>
    public sealed class PassiveInstance
    {
        public PassiveDef Def { get; }
        public int Level { get; private set; } = 1;
        public bool IsMaxLevel => Level >= Def.MaxLevel;
        public PassiveInstance(PassiveDef def) => Def = def;

        /// <summary>当前等级对应的数值（Values[Level-1]，越界取最后一个）。</summary>
        public float Value
        {
            get
            {
                var values = Def.Values;
                if (values == null || values.Length == 0)
                {
                    return 0f;
                }

                return values[Mathf.Clamp(Level - 1, 0, values.Length - 1)];
            }
        }

        public bool TryUpgrade()
        {
            if (IsMaxLevel)
            {
                return false;
            }

            Level++;
            return true;
        }
    }
}
