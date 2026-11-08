using UnityEngine;

namespace App.Battle
{
    /// <summary>拾取物的默认工厂：预制体 → AddComponent → Init。</summary>
    public sealed class PickupFactory : IPickupFactory
    {
        private readonly Transform _root;

        public PickupFactory(Transform root)
        {
            _root = root;
        }

        public PickupUnit Create(PickupKind kind, int value, Vector2 position)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.PickupPrefab, "Pickup", _root);
            var pickup = go.AddComponent<PickupUnit>();
            pickup.Init(kind, value, position);
            return pickup;
        }
    }
}
