using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人「出生规则」的唯一入口（P3-1b 第 6 步）。
    ///
    /// 负责：查表、按时间追加难度、把点位夹进可走区、组装出 <see cref="EnemyState"/>。
    /// 不负责：什么时候刷（那是 <see cref="EnemySpawner"/> + <see cref="IWaveStep"/>）、
    /// 长什么样（那是 <see cref="EnemyFactory"/> 与 <see cref="EnemyUnit"/>）。
    /// </summary>
    public sealed class SpawnSystem
    {
        /// <summary>每分钟血量成长（与改版前 `EnemySpawner.HpScale` 完全一致）。</summary>
        public const float HpGrowthPerMinute = 0.4f;

        /// <summary>每分钟伤害成长。</summary>
        public const float DamageGrowthPerMinute = 0.2f;

        public static float HpScale(float elapsed)
        {
            return 1f + HpGrowthPerMinute * Mathf.Floor(Mathf.Max(0f, elapsed) / 60f);
        }

        public static float DamageScale(float elapsed)
        {
            return 1f + DamageGrowthPerMinute * Mathf.Floor(Mathf.Max(0f, elapsed) / 60f);
        }

        /// <summary>
        /// 造一个敌人状态。查表失败返回 null（调用方只打日志、不再往下走）。
        /// Boss 不吃按分钟成长，所以它的血量完全由 `Enemies.json` 决定。
        /// </summary>
        public EnemyState Create(int defId, Vector2 position, EliteAffix affix, float elapsed, Rect walkable, float margin = 0.4f)
        {
            var def = EnemyDef.Get(defId);
            if (def == null)
            {
                Debug.LogWarning($"[Spawn] enemy id {defId} not found");
                return null;
            }

            return Create(def, position, affix, HpScale(elapsed), DamageScale(elapsed), walkable, margin);
        }

        /// <summary>Boss 不吃按分钟成长：强度完全由 `Enemies.json` 的血量决定。</summary>
        public EnemyState CreateBoss(int defId, Vector2 position, Rect walkable)
        {
            var def = EnemyDef.Get(defId);
            if (def == null)
            {
                Debug.LogWarning($"[Spawn] boss id {defId} not found");
                return null;
            }

            return Create(def, position, EliteAffix.None, 1f, 1f, walkable, 0.5f);
        }

        private static EnemyState Create(
            EnemyDef def,
            Vector2 position,
            EliteAffix affix,
            float hpScale,
            float damageScale,
            Rect walkable,
            float margin)
        {
            // 环形刷新之类的点位可能落在墙外，统一夹进可走范围。
            position = BattleMap.ClampToWalkable(position, margin);

            var state = new EnemyState
            {
                Def = def,
                Affix = affix,
                IsElite = affix != EliteAffix.None,
                IsBoss = def.Id >= BattleSession.BossEnemyId,
                MaxHp = def.Hp * hpScale,
                Speed = def.Speed,
                Damage = def.Damage * damageScale,
                Radius = def.Radius,
                AttackTimer = def.AttackInterval,
                WanderAngle = Random.value * Mathf.PI * 2f,
                Position = position
            };

            state.Hp = state.MaxHp;
            state.ApplyAffix();
            return state;
        }
    }
}
