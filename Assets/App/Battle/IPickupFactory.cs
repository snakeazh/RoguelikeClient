using UnityEngine;

namespace App.Battle
{
    /// <summary>拾取物生成入口：与 <see cref="IEnemyFactory"/> 同一套理由（MonoBehaviour 不能用构造器）。</summary>
    public interface IPickupFactory
    {
        PickupUnit Create(PickupKind kind, int value, Vector2 position);
    }
}
