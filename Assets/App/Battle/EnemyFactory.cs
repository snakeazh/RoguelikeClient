using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人工厂的默认实现：预制体 → AddComponent → Init。
    /// 生成点位由调用方负责夹进可走范围（`BattleMap.ClampToWalkable`），这里只管造。
    /// </summary>
    public sealed class EnemyFactory : IEnemyFactory
    {
        private readonly IBattleContext _context;
        private readonly Transform _root;

        public EnemyFactory(IBattleContext context, Transform root)
        {
            _context = context;
            _root = root;
        }

        public EnemyUnit Create(EnemyState state)
        {
            var go = BattleVisuals.Spawn(BattleVisuals.EnemyPrefab, "Enemy_" + state.Def.Name, _root);
            var enemy = go.AddComponent<EnemyUnit>();
            enemy.Init(_context, state);
            return enemy;
        }
    }
}
