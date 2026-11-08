namespace App.Battle
{
    /// <summary>拾取物生成入口：逻辑状态由 <see cref="PickupSystem"/> 建，工厂只负责造表现。</summary>
    public interface IPickupFactory
    {
        PickupUnit Create(PickupState state);
    }
}
