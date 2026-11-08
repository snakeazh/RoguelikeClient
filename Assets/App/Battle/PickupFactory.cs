using App.Config;
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

        public PickupUnit Create(PickupDef def, int amount, Vector2 position)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.PickupPrefab, "Pickup", _root);
            var pickup = go.AddComponent<PickupUnit>();
            pickup.Init(def, amount, position);
            return pickup;
        }
    }
}
