using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace App.Battle
{
    /// <summary>回旋刃：绕玩家转的刀刃，碰到敌人就造成伤害（同一只敌人有 0.35 秒冷却）。</summary>
    public sealed class OrbitBehavior : IWeaponBehavior
    {
        private const float HitCooldown = 0.35f;

        private sealed class Blade
        {
            public Transform Transform;
            public float AngleOffset;
        }

        private sealed class State
        {
            public readonly List<Blade> Blades = new List<Blade>();
            public float Angle;
        }

        private readonly Dictionary<WeaponInstance, State> _states = new Dictionary<WeaponInstance, State>();

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            var state = GetState(weapon);
            Sync(host, weapon, state);

            var player = host.Player;
            if (player == null || state.Blades.Count == 0)
            {
                return;
            }

            var radius = host.AreaOf(weapon, weapon.Def.Range);
            state.Angle += weapon.Def.Speed * deltaTime;
            var origin = (Vector2)player.transform.position;
            var now = Time.time;

            for (var i = 0; i < state.Blades.Count; i++)
            {
                var angle = state.Angle + state.Blades[i].AngleOffset;
                var position = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                state.Blades[i].Transform.position = position;

                // 刀刃朝外（贴图默认朝上，所以减 90°），绕玩家转起来才像风车而不是一群静止的贴图
                state.Blades[i].Transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);

                var damage = host.RollDamage(host.DamageOf(weapon));
                foreach (var enemy in host.Context.Enemies.ToArray())
                {
                    if (enemy.IsDead || now < enemy.NextBladeHitTime)
                    {
                        continue;
                    }

                    if (Vector2.Distance(position, enemy.transform.position) <= weapon.Def.Area + enemy.Radius)
                    {
                        enemy.NextBladeHitTime = now + HitCooldown;
                        host.Context.DamageEnemy(enemy, damage);
                    }
                }
            }
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            if (!_states.TryGetValue(weapon, out var state))
            {
                return;
            }

            Clear(state);
            _states.Remove(weapon);
        }

        private State GetState(WeaponInstance weapon)
        {
            if (_states.TryGetValue(weapon, out var state))
            {
                return state;
            }

            state = new State();
            _states[weapon] = state;
            return state;
        }

        private void Sync(WeaponRuntime host, WeaponInstance weapon, State state)
        {
            var desired = host.BladeCount(weapon);

            while (state.Blades.Count > desired)
            {
                var last = state.Blades[state.Blades.Count - 1];
                if (last.Transform != null)
                {
                    Object.Destroy(last.Transform.gameObject);
                }

                state.Blades.RemoveAt(state.Blades.Count - 1);
            }

            while (state.Blades.Count < desired)
            {
                var size = host.AreaOf(weapon, weapon.Def.Area);
                var bladeGo = new GameObject("Blade");
                bladeGo.transform.SetParent(host.Context.Root, false);
                BattleVisuals.Body(
                    bladeGo.transform,
                    BattleVisuals.Blade,
                    Color.white,
                    size * 3.0f,
                    BattleSort.Blade,
                    PlaceholderArt.Square);

                state.Blades.Add(new Blade
                {
                    Transform = bladeGo.transform,
                    AngleOffset = state.Blades.Count * Mathf.PI * 2f / Mathf.Max(1, desired)
                });
            }
        }

        private static void Clear(State state)
        {
            foreach (var blade in state.Blades)
            {
                if (blade.Transform != null)
                {
                    Object.Destroy(blade.Transform.gameObject);
                }
            }

            state.Blades.Clear();
        }
    }
}
