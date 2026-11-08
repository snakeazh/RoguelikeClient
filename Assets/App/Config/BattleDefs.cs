using System;

namespace App.Config
{
    // ───────────────────────── 战斗配置表定义 ─────────────────────────
    // 所有表都走 Assets/Res/Config/Battle/*.json，字段名必须与 json 完全一致（JsonUtility 规则）。
    // 枚举在 json 里写成整数（JsonUtility 不支持字符串枚举）。

    public enum EnemyBehavior
    {
        Chaser = 0,
        Runner = 1,
        Tank = 2,
        Shooter = 3,
        Exploder = 4,
        Splitter = 5
    }

    public enum WeaponKind
    {
        Projectile = 0,
        Orbit = 1,
        Homing = 2,
        Aura = 3,
        Cleave = 4,
        Chain = 5,
        Bomb = 6,

        /// <summary>毒雾：在地面留下持续伤害区域（P2-7 的 POC，实现见 <c>PoisonBehavior</c>）。</summary>
        Poison = 7
    }

    public enum PassiveKind
    {
        Damage = 0,
        Cooldown = 1,
        ProjectileCount = 2,
        Area = 3,
        MoveSpeed = 4,
        MaxHp = 5,
        Armor = 6,
        Crit = 7,
        Magnet = 8,
        Luck = 9
    }

    public enum WaveKind
    {
        Spawn = 0,
        Elite = 1,
        Ring = 2,
        Tide = 3,
        Boss = 4,
        Rest = 5
    }

    [Serializable]
    public sealed class EnemyDef : ConfigRowBase<EnemyDef>
    {
        public string Name = string.Empty;
        public EnemyBehavior Behavior;
        public float Hp = 10f;
        public float Speed = 2f;
        public float Damage = 10f;
        public float Radius = 0.35f;
        public int Exp = 1;
        public string Color = "FFFFFF";

        /// <summary>2D 精灵名，对应 Assets/Res/Battle/Sprites/&lt;Sprite&gt;.png；留空用默认精灵。</summary>
        public string Sprite = string.Empty;

        // Shooter 专用
        public float AttackInterval = 2f;
        public float AttackRange = 5.5f;
        public float ProjectileSpeed = 5f;

        // Exploder 专用
        public float FuseTime = 1f;
        public float ExplodeRadius = 1.8f;

        /// <summary>接触后给玩家附加的减速时长（秒），0 = 不减速。</summary>
        public float SlowOnHit;
    }

    [Serializable]
    public sealed class WeaponDef : ConfigRowBase<WeaponDef>
    {
        public string Name = string.Empty;
        public string Description = string.Empty;
        public WeaponKind Kind;
        public int MaxLevel = 5;

        public float Damage = 10f;
        public float Cooldown = 0.8f;
        public int Projectiles = 1;
        public float Range = 9f;
        public float Area = 1f;
        public float Speed = 9f;
        public int Pierce;

        /// <summary>弹体本身的碰撞/显示半径（不是 AoE）；Area 只用于光环、环绕刃与爆炸范围。</summary>
        public float ProjectileRadius = 0.16f;

        public float SplashRadius;
        public float Duration = 1.5f;
        public float SlowFactor = 0.7f;

        // 每级成长（百分比或绝对值，见各字段名）
        public float DamagePerLevel = 0.25f;
        public float CooldownPerLevel = 0.06f;
        public int CountPerLevel;
        public float AreaPerLevel = 0.12f;

        /// <summary>与该武器配套的被动 Id，用于后续「可进化」判定。</summary>
        public int PassiveId;

        /// <summary>弹体/法球使用的 2D 精灵名（Assets/Res/Battle/Sprites/&lt;Sprite&gt;.png）；留空用默认弹体精灵。</summary>
        public string Sprite = string.Empty;

        /// <summary>满级且配套被动满级后，开宝箱可进化成的武器 Id（0 = 不可进化）。</summary>
        public int EvolvedId;

        /// <summary>
        /// 0 = 由玩家本体施放；1 = 身边生成炮台（魔法炮 / 湮灭炮），
        /// 炮台负责自动瞄准、蓄能表现，弹体从炮口射出。
        /// </summary>
        public int Mount;
    }

    [Serializable]
    public sealed class PassiveDef : ConfigRowBase<PassiveDef>
    {
        public string Name = string.Empty;
        public string Description = string.Empty;
        public PassiveKind Kind;
        public int MaxLevel = 3;

        /// <summary>每级数值，长度 == MaxLevel。</summary>
        public float[] Values = Array.Empty<float>();
    }

    [Serializable]
    public sealed class WaveDef : ConfigRowBase<WaveDef>
    {
        public int Time;
        public WaveKind Kind;
        public int EnemyId;
        public int SpawnCount = 1;
        public float Interval = 2f;
        public float Duration = 10f;
        public float Radius = 3f;
        public EliteAffix Affix;
    }

    [Serializable]
    public sealed class LevelDef : ConfigRowBase<LevelDef>
    {
        /// <summary>升到 Id+1 级所需经验（Id 1 = 从 1 级升 2 级）。</summary>
        public int ExpToNext = 5;
    }

    /// <summary>
    /// 可选角色：决定开局血量、移速和自带的兵器（本命武器）。
    /// 缺表时 <see cref="BattleSession"/> 会退回 100 血 / 5 速 / 长弓，不会崩。
    /// </summary>
    [Serializable]
    public sealed class CharacterDef : ConfigRowBase<CharacterDef>
    {
        public string Name = string.Empty;

        /// <summary>选人界面卡片的副标题，例如「轻装远程」。</summary>
        public string Title = string.Empty;

        /// <summary>自带兵器的 WeaponDef Id（不占武器槽）。</summary>
        public int InnateWeaponId = 1;

        public float MaxHp = 100f;

        public float MoveSpeed = 5f;

        /// <summary>选人卡片的立绘精灵名（Assets/Res/Battle/Sprites/&lt;Sprite&gt;.png），留空用圆形占位。</summary>
        public string Sprite = string.Empty;

        public string Description = string.Empty;
    }

    /// <summary>精英词缀，与文档一致；数值在 BattleRunner 里按词缀套用。</summary>
    public enum EliteAffix
    {
        None = 0,
        Swift = 1,
        Giant = 2,
        Split = 3,
        Shell = 4,
        Frenzy = 5
    }
}
