using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Bootstrap;
using App.Config;
using App.UI;
using App.UI.Battle;
using Framework.Log;
using Framework.UI;
using Framework.UI.Navigation;
using UnityEngine;

namespace App.Battle
{
    public sealed partial class BattleRunner : MonoBehaviour
    {
        // ---------- 刷怪 ----------
        public void SpawnEnemy(int enemyId, Vector2 position, EliteAffix affix, float elapsed)
        {
            var def = EnemyDef.Get(enemyId);
            if (def == null)
            {
                AppLog.Warn(LogChannel.Game, $"[Battle] enemy id {enemyId} not found");
                return;
            }

            // 环形刷新之类的点位可能落在墙外，统一夹进可走范围。
            position = BattleMap.ClampToWalkable(position, 0.4f);

            var go = BattleVisuals.Spawn(BattleVisuals.EnemyPrefab, "Enemy_" + def.Name, transform);
            var enemy = go.AddComponent<EnemyUnit>();
            enemy.Init(this, def, position, affix, EnemySpawner.HpScale(elapsed), EnemySpawner.DamageScale(elapsed));
            _enemies.Add(enemy);
        }

        public void SpawnBoss(int enemyId, float elapsed)
        {
            var def = EnemyDef.Get(enemyId);
            if (def == null)
            {
                return;
            }

            // Boss 从玩家正上方、屏幕外走进来；地图边界比屏幕大，所以不会刷在墙外。
            var player = Player != null ? (Vector2)Player.transform.position : _walkable.center;
            var view = CameraView(1.2f);
            var position = BattleMap.ClampToWalkable(
                new Vector2(player.x, view.yMax + 0.6f),
                0.5f);
            var go = BattleVisuals.Spawn(BattleVisuals.EnemyPrefab, "Boss_" + def.Name, transform);
            var boss = go.AddComponent<EnemyUnit>();
            boss.Init(this, def, position, EliteAffix.None, 1f, 1f);
            _enemies.Add(boss);
        }

        /// <summary>
        /// 普通刷怪点：贴着相机可视区的外沿（沿射线找到屏幕边框再向外 0.9 世界单位），
        /// 再夹进地图可走范围。这样既不会眼看见怪凭空出现，怪也不用从很远的地方走过来。
        /// </summary>
        public Vector2 RandomSpawnPosition()
        {
            var player = Player != null ? (Vector2)Player.transform.position : _walkable.center;
            var view = CameraView();
            var center = view.center;

            for (var attempt = 0; attempt < 10; attempt++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // 射线与可视矩形边界的交点距离（取 x/y 两方向里更近的那个）。
                var distance = float.MaxValue;
                if (Mathf.Abs(dir.x) > 0.0001f)
                {
                    distance = Mathf.Min(distance, view.width * 0.5f / Mathf.Abs(dir.x));
                }

                if (Mathf.Abs(dir.y) > 0.0001f)
                {
                    distance = Mathf.Min(distance, view.height * 0.5f / Mathf.Abs(dir.y));
                }

                var candidate = player + dir * (distance + 0.45f);
                var clamped = BattleMap.ClampToWalkable(candidate, 0.4f);

                // 被墙挤回来以后必须仍在视野外，否则会眼看着怪凭空出现。
                if (!view.Contains(clamped))
                {
                    return clamped;
                }

                _ = center;
            }

            // 兜底：从地图边缘随机取一点（地图比屏幕大，边缘一定在视野外）。
            var edge = UnityEngine.Random.Range(0, 4);
            var bounds = _walkable;
            switch (edge)
            {
                case 0:
                    return new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMax - 0.5f);
                case 1:
                    return new Vector2(bounds.xMax - 0.5f, UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
                case 2:
                    return new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMin + 0.5f);
                default:
                    return new Vector2(bounds.xMin + 0.5f, UnityEngine.Random.Range(bounds.yMin, bounds.yMax));
            }
        }

        public void RunTide(WaveDef wave)
        {
            StartCoroutine(TideRoutine(wave));
        }

        private System.Collections.IEnumerator TideRoutine(WaveDef wave)
        {
            Toast("怪物潮来袭！");
            var waves = Mathf.Max(1, Mathf.RoundToInt(wave.Duration / Mathf.Max(0.2f, wave.Interval)));
            for (var i = 0; i < waves; i++)
            {
                for (var j = 0; j < wave.SpawnCount; j++)
                {
                    SpawnEnemy(wave.EnemyId, RandomSpawnPosition(), wave.Affix, Session.Elapsed);
                }

                yield return new WaitForSeconds(wave.Interval);
            }
        }

    }
}
