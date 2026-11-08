using UnityEngine;

namespace App.Battle
{
    /// <summary>拾取物的默认工厂：预制体 → AddComponent → Init（只造表现，不决定它是什么）。</summary>
    public sealed class PickupFactory : IPickupFactory
    {
        private readonly Transform _root;

        public PickupFactory(Transform root)
        {
            _root = root;
        }

        public PickupUnit Create(PickupState state)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.PickupPrefab, "Pickup", _root);
            var pickup = go.AddComponent<PickupUnit>();
            pickup.Init(state);
            return pickup;
        }
    }
}
