using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace App.Battle
{
    /// <summary>定时炸弹：朝敌人脚下抛雷，引信结束后范围爆炸。</summary>
    public sealed class BombBehavior : IWeaponBehavior
    {
        private sealed class Bomb
        {
            public IBombView View;
            public float Fuse;
            public DamageInfo Damage;
            public float Radius;
            public Color Color;
        }

        private readonly Dictionary<WeaponInstance, List<Bomb>> _bombs = new Dictionary<WeaponInstance, List<Bomb>>();

        public void Tick(WeaponRuntime host, WeaponInstance weapon, float deltaTime)
        {
            var bombs = GetBombs(weapon);

            // 引信先走：不管这把武器现在能不能再扔，已经在场上的雷都要爆
            TickFuses(host, bombs, deltaTime);

            weapon.AdvanceCooldown(deltaTime);
            if (!weapon.IsReady)
            {
                return;
            }

            var target = host.FindNearestEnemy(weapon.Def.Range);
            if (target == null)
            {
                weapon.ClearCooldown();
                return;
            }

            weapon.ResetCooldown(host.CooldownOf(weapon));

            var count = host.CountOf(weapon);
            var damage = host.DamageOf(weapon);
            var radius = Mathf.Max(
                0.6f,
                host.AreaOf(weapon, weapon.Def.SplashRadius > 0f ? weapon.Def.SplashRadius : weapon.Def.Area));
            var center = target.Position;

            for (var i = 0; i < count; i++)
            {
                var offset = count > 1
                    ? host.Session.Rng.NextInsideUnitCircle() * 0.9f
                    : host.Session.Rng.NextInsideUnitCircle() * 0.25f;
                var position = center + offset;

                bombs.Add(new Bomb
                {
                    View = host.Views.CreateBomb(position, 0.34f),
                    Fuse = Mathf.Max(0.2f, weapon.Def.Duration),
                    Damage = host.RollDamage(damage),
                    Radius = radius,
                    Color = new Color(1f, 0.6f, 0.2f)
                });
            }
        }

        public void Dispose(WeaponRuntime host, WeaponInstance weapon)
        {
            if (!_bombs.TryGetValue(weapon, out var bombs))
            {
                return;
            }

            Clear(bombs);
            _bombs.Remove(weapon);
        }

        private void TickFuses(WeaponRuntime host, List<Bomb> bombs, float deltaTime)
        {
            for (var i = bombs.Count - 1; i >= 0; i--)
            {
                var bomb = bombs[i];
                bomb.Fuse -= deltaTime;
                bomb.View.Pulse(host.Context.ElapsedSeconds);

                if (bomb.Fuse > 0f)
                {
                    continue;
                }

                var position = bomb.View.Position;
                foreach (var enemy in host.Context.Enemies.ToArray())
                {
                    if (enemy.IsDead)
                    {
                        continue;
                    }

                    if (Vector2.Distance(position, enemy.Position) <= bomb.Radius + enemy.Radius)
                    {
                        host.Context.DamageEnemy(enemy, bomb.Damage);
                    }
                }

                host.Views.Flash(position, bomb.Radius * 2f, bomb.Color, 0.22f);
                bomb.View.Dispose();

                bombs.RemoveAt(i);
            }
        }

        private List<Bomb> GetBombs(WeaponInstance weapon)
        {
            if (_bombs.TryGetValue(weapon, out var bombs))
            {
                return bombs;
            }

            bombs = new List<Bomb>();
            _bombs[weapon] = bombs;
            return bombs;
        }

        private static void Clear(List<Bomb> bombs)
        {
            foreach (var bomb in bombs)
            {
                bomb.View.Dispose();
            }

            bombs.Clear();
        }
    }
}
