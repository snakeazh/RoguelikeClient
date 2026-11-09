namespace App.Battle
{
    /// <summary>
    /// 参与模拟的子系统（目前是武器层）。
    ///
    /// 抽出来是为了让 <see cref="BattleSimulation"/> 保持纯编排：它只知道「有一批 step 要按顺序 Tick」，
    /// 不需要知道武器怎么开火。以后加「状态效果结算」这类子系统也实现这个接口。
    /// </summary>
    public interface ISimulationStep
    {
        void Tick(float deltaTime);
    }
}
