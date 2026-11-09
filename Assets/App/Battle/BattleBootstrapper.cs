using App.Config;
using Framework.Log;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一局战斗的组合根：决定「用哪张角色表、注入哪些波次/拾取规则、武器视图用哪个实现」。
    ///
    /// 这些选择集中在一个文件里，<see cref="BattleRunner"/> 就只负责「在正确的时机调用装配、
    /// 把结果放进自己的字段」，不再知道 <see cref="BattleSimulation"/> 需要几个注册表、
    /// <see cref="WeaponRuntime"/> 要怎么喂视图实现。
    /// 场景相关的装配（生成地图精灵、镜头、受击遮罩）仍留在 Runner：那本来就是 MonoBehaviour 的活。
    /// </summary>
    public static class BattleBootstrapper
    {
        /// <summary>建会话：种子 + 角色（决定血量/移速/本命武器），缺表时退回默认值。</summary>
        public static BattleSession CreateSession(BattleCatalog catalog, int seed, int characterId)
        {
            var session = new BattleSession(catalog, seed);

            var character = catalog.Character(characterId > 0 ? characterId : BattleSession.DefaultCharacterId);
            if (character != null)
            {
                session.SetCharacter(character);
            }
            else
            {
                AppLog.Warn(LogChannel.Game, "[Battle] character table missing, falling back to default stats");
            }

            // 角色自带的武器：开局就有，不占武器槽。
            var innate = catalog.Weapon(session.InnateWeaponId);
            if (innate != null)
            {
                session.EquipInnate(innate);
            }
            else
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] innate weapon (Id={session.InnateWeaponId}) missing in Weapons.json");
            }

            return session;
        }

        /// <summary>建规则层：玩家位移 / 刷怪 / 敌人 / 接触 / 弹体 / 拾取六个系统，按默认注册表注入。</summary>
        public static BattleSimulation CreateSimulation(BattleSession session, Rect walkable)
        {
            return new BattleSimulation(
                session,
                walkable.center,
                WaveStepRegistry.CreateDefault(),
                session.Catalog.Waves,
                PickupEffects.CreateDefault());
        }

        /// <summary>建武器层：规则只认 <see cref="IWeaponViews"/>，具体是 Unity 还是空实现由这里决定。</summary>
        public static WeaponRuntime CreateWeapons(IWeaponContext context, BattleSession session, Transform root, PlayerUnit player)
        {
            var weapons = new WeaponRuntime();
            weapons.Init(context, session, new UnityWeaponViews(root, player));
            return weapons;
        }
    }
}
