using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 弹体：玩家投射物与敌人投射物共用。直线飞行 / 低速追踪，圆形命中判定，支持穿透与小范围爆炸。
    /// </summary>
    public sealed class ProjectileUnit : MonoBehaviour
    {
        private readonly HashSet<EnemyUnit> _hit = new HashSet<EnemyUnit>();

        // 命中判定会击杀敌人（进而增删 BattleRunner 的敌人列表），所以要先拷一份再遍历。
        private static readonly List<EnemyUnit> s_targets = new List<EnemyUnit>(512);

        private Vector2 _velocity;
        private DamageInfo _damage;
        private float _radius;
        private int _pierce;
        private float _splash;
        private float _life;
        private float _spin;
        private float _spinAngle;
        private EnemyUnit _homingTarget;

        public bool FromPlayer { get; private set; }

        /// <summary>当前飞行速度（调试 / 朝向校验用）。</summary>
        public Vector2 Velocity => _velocity;

        public void InitPlayer(
            Vector2 position,
            Vector2 direction,
            float speed,
            DamageInfo damage,
            float radius,
            int pierce,
            float splash,
            float life,
            float spin = 0f,
            EnemyUnit homingTarget = null,
            string spriteKey = null)
        {
            FromPlayer = true;
            transform.position = position;
            _velocity = direction.normalized * speed;
            _damage = damage;
            _radius = Mathf.Max(0.05f, radius);
            _pierce = pierce;
            _splash = splash;
            _life = life;
            _spin = spin;
            _spinAngle = 0f;
            _homingTarget = homingTarget;

            var key = string.IsNullOrEmpty(spriteKey) ? BattleVisuals.ProjectileFallback : spriteKey;
            BattleVisuals.Body(transform, key, Color.white, _radius * 2f, BattleSort.Projectile, PlaceholderArt.Circle);
            ApplyFacing();

            var body = transform.Find("Body");
            if (body != null)
            {
                body.name = "PlayerProjectile";
            }

            name = "PlayerProjectile";
        }

        public void InitEnemy(Vector2 position, Vector2 direction, float speed, float damage, float radius)
        {
            FromPlayer = false;
            transform.position = position;
            _velocity = direction.normalized * speed;
            _damage = DamageInfo.Plain(damage);
            _radius = Mathf.Max(0.05f, radius);
            _pierce = 0;
            _splash = 0f;
            _life = 6f;
            _spin = 0f;
            _spinAngle = 0f;
            _homingTarget = null;

            BattleVisuals.Body(transform, "Battle/Sprites/proj_enemy", Color.white, _radius * 2f, BattleSort.Projectile, PlaceholderArt.Circle);
            ApplyFacing();
            name = "EnemyProjectile";
        }

        /// <summary>
        /// 让贴图朝着速度方向。所有弹体贴图都是「朝上（+Y）」画的，
        /// 所以角度要减 90°；<see cref="_spin"/> 是额外的自转。
        /// </summary>
        private void ApplyFacing()
        {
            if (_velocity.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            var angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg - 90f + _spinAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>返回 true 表示本帧应该销毁。</summary>
        public bool Tick(float deltaTime, BattleRunner runner)
        {
            _life -= deltaTime;
            if (_life <= 0f)
            {
                return true;
            }

            // 追踪弹：朝目标缓慢转向
            if (_homingTarget != null && !_homingTarget.IsDead)
            {
                var desired = ((Vector2)(_homingTarget.transform.position - transform.position)).normalized * _velocity.magnitude;
                _velocity = Vector2.Lerp(_velocity, desired, Mathf.Clamp01(deltaTime * 6f));
            }

            transform.position += (Vector3)(_velocity * deltaTime);

            if (_spin != 0f)
            {
                _spinAngle += _spin * deltaTime;
            }

            // 弹体跟随飞行方向旋转（箭头/剑气这类有朝向的贴图才不会一直朝上）。
            ApplyFacing();

            if (!runner.IsInsideArena(transform.position, 3f))
            {
                return true;
            }

            return FromPlayer ? TickPlayerProjectile(runner) : TickEnemyProjectile(runner);
        }

        private bool TickPlayerProjectile(BattleRunner runner)
        {
            var position = (Vector2)transform.position;
            s_targets.Clear();
            s_targets.AddRange(runner.Enemies);
            foreach (var enemy in s_targets)
            {
                if (enemy.IsDead || _hit.Contains(enemy))
                {
                    continue;
                }

                var distance = Vector2.Distance(position, enemy.transform.position);
                if (distance > _radius + enemy.Radius)
                {
                    continue;
                }

                _hit.Add(enemy);
                runner.DamageEnemy(enemy, _damage);

                if (_splash > 0f)
                {
                    runner.ExplodeAt(transform.position, _splash, _damage.Scaled(0.5f), false);
                }

                if (_pierce <= 0)
                {
                    return true;
                }

                _pierce--;
            }

            return false;
        }

        private bool TickEnemyProjectile(BattleRunner runner)
        {
            var player = runner.Player;
            if (player == null)
            {
                return true;
            }

            var distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance <= _radius + PlayerUnit.Radius)
            {
                if (!player.IsInvulnerable)
                {
                    runner.DamagePlayer(_damage.Amount);
                }

                return true;
            }

            return false;
        }
    }
}
