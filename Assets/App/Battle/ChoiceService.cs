using System.Collections.Generic;
using App.Config;

namespace App.Battle
{
    public enum ChoiceKind
    {
        WeaponNew,
        WeaponUpgrade,
        PassiveNew,
        PassiveUpgrade,
        Heal
    }

    /// <summary>升级三选一里的一个选项。</summary>
    public sealed class ChoiceOption
    {
        public ChoiceKind Kind;
        public int DefId;
        public string Name;
        public string Description;

        public override string ToString() => $"{Kind}:{Name}";
    }

    /// <summary>
    /// 升级三选一的候选生成与结算。
    /// 规则（骨架版）：
    /// - 武器槽 4 把、被动槽 4 个，槽满后只出"升级"；
    /// - 已持有且未满级的项权重更高（更容易被抽到）；
    /// - 至少保底一个升级项，避免三选一全是新东西导致 Build 卡在 Lv1；
    /// - 没有可选项时给"回复 30% HP"。
    ///
    /// 生成分三步：<c>收集候选 → 洗牌 → 按槽位拼盘</c>，
    /// 每一步各自一个方法，改规则时只动其中一步（洗牌顺序决定同种子的抽卡结果，别随手调）。
    /// </summary>
    public static class ChoiceService
    {
        private const int OptionCount = 3;

        private const float HealPercent = 0.3f;

        /// <summary>本局的候选池：升级项、新武器、新被动三堆，互不干扰。</summary>
        private sealed class Candidates
        {
            public readonly List<ChoiceOption> Upgrades = new List<ChoiceOption>();

            public readonly List<ChoiceOption> WeaponNews = new List<ChoiceOption>();

            public readonly List<ChoiceOption> PassiveNews = new List<ChoiceOption>();
        }

        public static List<ChoiceOption> Build(BattleSession session)
        {
            var candidates = new Candidates();
            CollectUpgrades(session, candidates);
            CollectNewWeapons(session, candidates);
            CollectNewPassives(session, candidates);

            // 洗牌顺序即抽卡顺序：武器新闻 → 被动新闻 → 升级，改动会让同种子的结果分叉。
            Shuffle(candidates.WeaponNews, session.Rng);
            Shuffle(candidates.PassiveNews, session.Rng);
            Shuffle(candidates.Upgrades, session.Rng);

            var result = new List<ChoiceOption>(OptionCount);
            var evolutionPity = CollectEvolutionPity(session, candidates.Upgrades);

            // 结构上保证「升级 / 新武器 / 新被动」各占一个位置：
            // 不然纯随机池子很容易连着出三个升级，Build 永远长不全。
            TryAddDistinct(result, evolutionPity);
            TryAddDistinct(result, candidates.WeaponNews);
            TryAddDistinct(result, candidates.PassiveNews);
            TryAddDistinct(result, candidates.Upgrades);

            FillFromPool(result, candidates, session.Rng);
            FillWithHeal(result);
            return result;
        }

        /// <summary>把选项应用到会话（UI 选中后调用）。</summary>
        public static void Apply(BattleSession session, ChoiceOption option)
        {
            if (session == null || option == null)
            {
                return;
            }

            switch (option.Kind)
            {
                case ChoiceKind.WeaponNew:
                    session.AddWeapon(WeaponDef.Get(option.DefId));
                    break;

                case ChoiceKind.WeaponUpgrade:
                    session.TryUpgradeWeapon(FindWeapon(session, option.DefId));
                    break;

                case ChoiceKind.PassiveNew:
                    session.AddPassive(PassiveDef.Get(option.DefId));
                    break;

                case ChoiceKind.PassiveUpgrade:
                    session.TryUpgradePassive(FindPassive(session, option.DefId));
                    break;

                case ChoiceKind.Heal:
                    session.HealPercent(HealPercent);
                    break;
            }
        }

        // ---------- 第一步：收集候选 ----------

        private static void CollectUpgrades(BattleSession session, Candidates candidates)
        {
            // 本命武器也能升级（它在 AllWeapons 末尾，不占槽位）
            foreach (var weapon in session.AllWeapons)
            {
                if (weapon.IsMaxLevel)
                {
                    continue;
                }

                candidates.Upgrades.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.WeaponUpgrade,
                    DefId = weapon.Def.Id,
                    Name = weapon.Def.Name,
                    Description = $"Lv{weapon.Level} → Lv{weapon.Level + 1}：伤害与范围提升"
                });
            }

            foreach (var passive in session.Passives)
            {
                if (passive.IsMaxLevel)
                {
                    continue;
                }

                candidates.Upgrades.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.PassiveUpgrade,
                    DefId = passive.Def.Id,
                    Name = passive.Def.Name,
                    Description = $"Lv{passive.Level} → Lv{passive.Level + 1}：{passive.Def.Description}"
                });
            }
        }

        private static void CollectNewWeapons(BattleSession session, Candidates candidates)
        {
            if (!session.CanAddWeapon)
            {
                return;
            }

            foreach (var def in WeaponDef.All.Values)
            {
                // 进化形态不进普通掉落池，只能靠开宝箱进化。
                if (def.Id >= 100 || session.HasWeapon(def.Id))
                {
                    continue;
                }

                candidates.WeaponNews.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.WeaponNew,
                    DefId = def.Id,
                    Name = def.Name,
                    Description = $"新武器：{def.Description}"
                });
            }
        }

        private static void CollectNewPassives(BattleSession session, Candidates candidates)
        {
            if (!session.CanAddPassive)
            {
                return;
            }

            foreach (var def in PassiveDef.All.Values)
            {
                if (session.FindPassive(def.Id) != null)
                {
                    continue;
                }

                candidates.PassiveNews.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.PassiveNew,
                    DefId = def.Id,
                    Name = def.Name,
                    Description = $"新被动：{def.Description}"
                });
            }
        }

        /// <summary>
        /// 保底：某把武器已满级、但配套被动还没满级时，优先推它的被动，
        /// 不然「Lv5 武器 + 满级被动 → 宝箱进化」几乎凑不齐。
        /// </summary>
        private static List<ChoiceOption> CollectEvolutionPity(BattleSession session, List<ChoiceOption> upgrades)
        {
            var pity = new List<ChoiceOption>();
            foreach (var weapon in session.AllWeapons)
            {
                if (!weapon.IsMaxLevel || weapon.Def.EvolvedId <= 0)
                {
                    continue;
                }

                var paired = session.FindPassive(weapon.Def.PassiveId);
                if (paired == null || paired.IsMaxLevel)
                {
                    continue;
                }

                foreach (var option in upgrades)
                {
                    if (option.Kind != ChoiceKind.PassiveUpgrade || option.DefId != paired.Def.Id)
                    {
                        continue;
                    }

                    // 本命武器也照顾，但排在后半段：玩家想要的 build 不应该被本命武器抢走选项
                    if (weapon.IsInnate)
                    {
                        pity.Add(option);
                    }
                    else
                    {
                        pity.Insert(0, option);
                    }

                    break;
                }
            }

            return pity;
        }

        // ---------- 第二步：拼盘 ----------

        private static void FillFromPool(List<ChoiceOption> result, Candidates candidates, System.Random rng)
        {
            var pool = new List<ChoiceOption>();
            pool.AddRange(candidates.WeaponNews);
            pool.AddRange(candidates.PassiveNews);
            pool.AddRange(candidates.Upgrades);
            Shuffle(pool, rng);

            for (var i = 0; i < pool.Count && result.Count < OptionCount; i++)
            {
                if (!ContainsSame(result, pool[i]))
                {
                    result.Add(pool[i]);
                }
            }
        }

        private static void FillWithHeal(List<ChoiceOption> result)
        {
            if (result.Count == 0)
            {
                result.Add(HealOption());
            }

            // 不足 3 个时用回血补位（例如槽全满、全部满级）。
            while (result.Count < OptionCount)
            {
                result.Add(HealOption());
            }
        }

        private static ChoiceOption HealOption()
        {
            return new ChoiceOption
            {
                Kind = ChoiceKind.Heal,
                Name = "小蛋糕",
                Description = "回复 30% 最大生命值"
            };
        }

        private static WeaponInstance FindWeapon(BattleSession session, int defId)
        {
            foreach (var weapon in session.AllWeapons)
            {
                if (weapon.Def.Id == defId)
                {
                    return weapon;
                }
            }

            return null;
        }

        private static PassiveInstance FindPassive(BattleSession session, int defId)
        {
            foreach (var passive in session.Passives)
            {
                if (passive.Def.Id == defId)
                {
                    return passive;
                }
            }

            return null;
        }

        private static void TryAddDistinct(List<ChoiceOption> result, List<ChoiceOption> candidates)
        {
            foreach (var option in candidates)
            {
                if (!ContainsSame(result, option))
                {
                    result.Add(option);
                    return;
                }
            }
        }

        private static bool ContainsSame(List<ChoiceOption> list, ChoiceOption option)
        {
            foreach (var item in list)
            {
                if (item.Kind == option.Kind && item.DefId == option.DefId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Shuffle(List<ChoiceOption> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
