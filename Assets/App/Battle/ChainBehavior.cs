using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>雷电法典：先打最近的敌人，再在敌人之间跳跃，每次跳伤害衰减 15%，最多 8 跳。</summary>
    public sealed class ChainBehavior : IWeaponBehavior
    {
        private const int MaxJumps = 8;
        private const float JumpFalloff = 0.85f;

        private readonly Dictionary<WeaponInstance, List<EnemyState>> _visited = new Dictionary<WeaponInstance, List<EnemyState>>();

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var first = host.FindNearestEnemy(weapon.Def.Range);
            if (first == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));

            var visited = GetVisited(weapon);
            visited.Clear();

            var jumps = Mathf.Clamp(1 + host.CountOf(weapon), 1, MaxJumps);
            var chainRadius = Mathf.Max(1.2f, host.AreaOf(weapon, weapon.Def.Area));
            var damage = host.DamageOf(weapon);
            var origin = host.PlayerPosition;

            var current = first;
            var boltFrom = (Vector3)origin + Vector3.up * 0.3f;
            for (var i = 0; i < jumps && current != null; i++)
            {
                visited.Add(current);

                // 电弧：玩家 -> 第一个敌人，之后每跳在敌人之间连一段，越靠后越暗
                var target = current.Position;
                var boltColor = new Color(0.68f, 0.88f, 1f, Mathf.Lerp(1f, 0.55f, i / (float)jumps));
                LightningFx.Bolt(boltFrom, target, boltColor, 0.15f + i * 0.015f);
                boltFrom = target;

                host.Context.DamageEnemy(current, host.RollDamage(damage));

                TransientFx.Flash(current.Position, current.Radius * 2.4f, new Color(0.7f, 0.85f, 1f), 0.16f);

                damage *= JumpFalloff;
                current = FindNext(host, current, origin, weapon.Def.Range, chainRadius, visited);
            }
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            _visited.Remove(weapon);
        }

        private List<EnemyState> GetVisited(WeaponInstance weapon)
        {
            if (_visited.TryGetValue(weapon, out var list))
            {
                return list;
            }

            list = new List<EnemyState>();
            _visited[weapon] = list;
            return list;
        }

        private static EnemyState FindNext(
            WeaponRuntime host,
            EnemyState from,
            Vector2 origin,
            float range,
            float chainRadius,
            List<EnemyState> visited)
        {
            EnemyState best = null;
            var bestDistance = float.MaxValue;
            var fromPosition = (Vector2)from.Position;

            foreach (var enemy in host.Context.Enemies)
            {
                if (enemy.IsDead || visited.Contains(enemy))
                {
                    continue;
                }

                if (Vector2.Distance(origin, enemy.Position) > range)
                {
                    continue;
                }

                var distance = Vector2.Distance(fromPosition, enemy.Position);
                if (distance > chainRadius || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = enemy;
            }

            return best;
        }
    }
}
