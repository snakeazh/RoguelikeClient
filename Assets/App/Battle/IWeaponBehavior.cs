namespace App.Battle
{
    /// <summary>
    /// 一种武器形态的开火逻辑。
    ///
    /// 以前所有形态挤在 <c>WeaponRuntime.TickWeapon</c> 的 <c>switch</c> 里，加一种形态要改枚举 + 改 switch + 动一个 700 行的文件。
    /// 现在新增形态 = 写一个实现 + 在 <see cref="WeaponBehaviors"/> 注册一行，互不影响。
    /// </summary>
    public interface IWeaponBehavior
    {
        /// <summary>
        /// 每帧调用一次（武器是否冷却完毕由形态自己判断，不在这里过滤 ——
        /// 环绕刃、炸弹这类形态即使不开火也要推进自己的表现物）。
        /// </summary>
        void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime);

        /// <summary>武器被移除或战斗结束时调用，用来销毁该形态创建的表现物。</summary>
        void Dispose(WeaponRuntime host, WeaponInstance weapon);
    }
}
