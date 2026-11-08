using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 敌人生成入口。`EnemyUnit` 是 MonoBehaviour，不能用构造器创建，
    /// 所以「怎么造」这一层单独抽成工厂：调用方只给数据，不接触预制体、AddComponent 与 Init 顺序。
    /// 以后要做对象池，只改实现，`BattleRunner` 与各波次都不用动。
    /// </summary>
    public interface IEnemyFactory
    {
        EnemyUnit Create(EnemyDef def, Vector2 position, EliteAffix affix, float hpScale, float dmgScale);
    }
}
