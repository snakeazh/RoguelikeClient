namespace App.Battle
{
    /// <summary>
    /// 敌人视图池：一局里同屏上百只、死了又刷，`Instantiate`/`Destroy` 的代价比弹体还高。
    /// 规则状态（`EnemyState`）仍由 <see cref="EnemySystem"/> 每次新建，这里只复用「画出来的那个对象」。
    /// </summary>
    public sealed class EnemyFactory : IEnemyFactory
    {
        private readonly IEnemyContext _context;
        private readonly ViewPool<EnemyUnit> _views;

        public EnemyFactory(IEnemyContext context, ViewPool<EnemyUnit> views)
        {
            _context = context;
            _views = views;
        }

        public EnemyUnit Create(EnemyState state)
        {
            var view = _views.Rent();
            view.Init(_context, state);
            view.name = "Enemy_" + state.Def.Name;
            return view;
        }
    }
}
