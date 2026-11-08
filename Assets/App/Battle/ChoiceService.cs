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
    /// </summary>
    public static class ChoiceService
    {
        private const int OptionCount = 3;

        public static List<ChoiceOption> Build(BattleSession session)
        {
            var upgrades = new List<ChoiceOption>();
            var news = new List<ChoiceOption>();

            // 本命武器也能升级（它在 AllWeapons 末尾，不占槽位）
            foreach (var weapon in session.AllWeapons)
            {
                if (!weapon.IsMaxLevel)
                {
                    upgrades.Add(new ChoiceOption
                    {
                        Kind = ChoiceKind.WeaponUpgrade,
                        DefId = weapon.Def.Id,
                        Name = weapon.Def.Name,
                        Description = $"Lv{weapon.Level} → Lv{weapon.Level + 1}：伤害与范围提升"
                    });
                }
            }

            foreach (var passive in session.Passives)
            {
                if (!passive.IsMaxLevel)
                {
                    upgrades.Add(new ChoiceOption
                    {
                        Kind = ChoiceKind.PassiveUpgrade,
                        DefId = passive.Def.Id,
                        Name = passive.Def.Name,
                        Description = $"Lv{passive.Level} → Lv{passive.Level + 1}：{passive.Def.Description}"
                    });
                }
            }

            if (session.CanAddWeapon)
            {
                foreach (var def in WeaponDef.All.Values)
                {
                    // 进化形态不进普通掉落池，只能靠开宝箱进化。
                    if (def.Id >= 100)
                    {
                        continue;
                    }

                    if (!session.HasWeapon(def.Id))
                    {
                        news.Add(new ChoiceOption
                        {
                            Kind = ChoiceKind.WeaponNew,
                            DefId = def.Id,
                            Name = def.Name,
                            Description = $"新武器：{def.Description}"
                        });
                    }
                }
            }

            if (session.CanAddPassive)
            {
                foreach (var def in PassiveDef.All.Values)
                {
                    if (session.FindPassive(def.Id) == null)
                    {
                        news.Add(new ChoiceOption
                        {
                            Kind = ChoiceKind.PassiveNew,
                            DefId = def.Id,
                            Name = def.Name,
                            Description = $"新被动：{def.Description}"
                        });
                    }
                }
            }

            var result = new List<ChoiceOption>(OptionCount);
            var rng = session.Rng;

            var weaponNews = new List<ChoiceOption>();
            var passiveNews = new List<ChoiceOption>();
            foreach (var option in news)
            {
                if (option.Kind == ChoiceKind.WeaponNew)
                {
                    weaponNews.Add(option);
                }
                else
                {
                    passiveNews.Add(option);
                }
            }

            Shuffle(weaponNews, rng);
            Shuffle(passiveNews, rng);
            Shuffle(upgrades, rng);

            // 保底：某把武器已满级、但配套被动还没满级时，优先推它的被动，
            // 不然「Lv5 武器 + 满级被动 → 宝箱进化」几乎凑不齐。
            var evolutionPity = new List<ChoiceOption>();
            foreach (var weapon in session.AllWeapons)
            {
                if (!weapon.IsMaxLevel || weapon.Def.EvolvedId <= 0)
                {
                    continue;
                }

                // 本命武器也照顾，但排在后半段：玩家想要的 build 不应该被本命武器抢走选项
                var paired = session.FindPassive(weapon.Def.PassiveId);
                if (paired == null || paired.IsMaxLevel)
                {
                    continue;
                }

                foreach (var option in upgrades)
                {
                    if (option.Kind == ChoiceKind.PassiveUpgrade && option.DefId == paired.Def.Id)
                    {
                        if (weapon.IsInnate)
                        {
                            evolutionPity.Add(option);
                        }
                        else
                        {
                            evolutionPity.Insert(0, option);
                        }

                        break;
                    }
                }
            }

            // 结构上保证「升级 / 新武器 / 新被动」各占一个位置：
            // 不然纯随机池子很容易连着出三个升级，Build 永远长不全。
            TryAddDistinct(result, evolutionPity);
            TryAddDistinct(result, weaponNews);
            TryAddDistinct(result, passiveNews);
            TryAddDistinct(result, upgrades);

            var pool = new List<ChoiceOption>();
            pool.AddRange(weaponNews);
            pool.AddRange(passiveNews);
            pool.AddRange(upgrades);
            Shuffle(pool, rng);

            for (var i = 0; i < pool.Count && result.Count < OptionCount; i++)
            {
                if (!ContainsSame(result, pool[i]))
                {
                    result.Add(pool[i]);
                }
            }

            if (result.Count == 0)
            {
                result.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.Heal,
                    Name = "小蛋糕",
                    Description = "回复 30% 最大生命值"
                });
            }

            // 不足 3 个时用回血补位（例如槽全满、全部满级）。
            while (result.Count < OptionCount)
            {
                result.Add(new ChoiceOption
                {
                    Kind = ChoiceKind.Heal,
                    Name = "小蛋糕",
                    Description = "回复 30% 最大生命值"
                });
            }

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
                    session.HealPercent(0.3f);
                    break;
            }
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
