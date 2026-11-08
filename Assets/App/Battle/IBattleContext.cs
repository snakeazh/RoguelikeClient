using System.Collections.Generic;
using App.Config;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗世界对「单位」暴露的最小接口。
    ///
    /// 以前 <see cref="EnemyUnit"/> / <see cref="WeaponRuntime"/> / <see cref="ProjectileUnit"/> /
    /// <see cref="EnemySpawner"/> 都直接持有 <see cref="BattleRunner"/> 这个具体的上帝类，
    /// 导致「单位 → 整局战斗」的单向依赖被拉成双向，写单测或换一种战斗循环都无从下手。
    ///
    /// 这里只保留单位真正需要的东西：
    /// <list type="bullet">
    /// <item>读世界：玩家、敌人列表、可走区域、刷怪点。</item>
    /// <item>发请求：刷怪 / 潮水 / 掉落补给、伤害、投射物、爆炸、Toast。</item>
    /// </list>
    /// 之后 P2 的 <c>IWeaponBehavior</c> / <c>IEnemyBehavior</c> / <c>IWaveStep</c> 都只依赖这个接口，
    /// 不再认识 <see cref="BattleRunner"/>。
    /// </summary>
    public interface IBattleContext
    {
        /// <summary>战场根节点，单位/弹体都挂到它下面。</summary>
        Transform Root { get; }

        PlayerUnit Player { get; }

        IReadOnlyList<EnemyUnit> Enemies { get; }

        /// <summary>可走区域（地图内缩掉墙厚）。</summary>
        Rect Walkable { get; }

        /// <summary>本局已进行的秒数（刷怪数值放大与分裂怪用）。</summary>
        float ElapsedSeconds { get; }

        /// <summary>在镜头边缘外取一个可走、且不在当前视野里的刷怪点。</summary>
        Vector2 RandomSpawnPosition();

        void SpawnEnemy(int enemyId, Vector2 position, EliteAffix affix, float elapsed);

        void SpawnBoss(int enemyId, float elapsed);

        void RunTide(WaveDef wave);

        void DropRestPickups();

        void DamageEnemy(EnemyUnit enemy, DamageInfo damage);

        void DamagePlayer(float amount);

        /// <summary>点是否还在地图内（弹体飞出地图就回收）。</summary>
        bool IsInsideArena(Vector3 position, float margin);

        void SpawnPlayerProjectile(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            EnemyUnit homingTarget,
            string spriteKey);

        void SpawnEnemyProjectile(Vector3 position, Vector2 direction, float speed, float damage);

        void ExplodeAt(Vector3 position, float radius, DamageInfo damage, bool hitPlayer);

        void Toast(string message);
    }
}
