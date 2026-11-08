using System.Collections.Generic;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 冒烟机器人：躲开近处敌人、顺手捡经验，被逼到墙角就绕回场中心。
    /// 它和玩家摇杆实现的是同一个 <see cref="IUnitInput"/>，所以切换控制方式不需要改角色逻辑。
    /// </summary>
    public sealed class AutoPlayerInput : IUnitInput
    {
        private const float ThreatRadius = 4.5f;
        private const float PickupRadius = 6f;
        private const float WallMargin = 1.5f;
        private const float AutoDashRange = 1.4f;

        private readonly BattleRunner _runner;

        public AutoPlayerInput(BattleRunner runner)
        {
            _runner = runner;
        }

        public Vector2 Move { get; private set; }

        public bool DashRequested { get; private set; }

        public void Update(float deltaTime)
        {
            var player = _runner.Player;
            if (player == null)
            {
                Move = Vector2.zero;
                DashRequested = false;
                return;
            }

            var position = (Vector2)player.transform.position;
            var walkable = _runner.Walkable;

            Vector2 move;
            var threatCount = 0;
            var threat = Vector2.zero;

            var enemies = _runner.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var offset = position - (Vector2)enemy.transform.position;
                var distance = offset.magnitude;
                if (distance > ThreatRadius)
                {
                    continue;
                }

                threatCount++;
                threat += offset / Mathf.Max(0.3f, distance * distance);
            }

            if (threatCount > 0)
            {
                // 一边逃跑一边绕场中心切向移动，避免被逼到墙角。
                var toCenter = walkable.center - position;
                var tangent = new Vector2(-toCenter.y, toCenter.x).normalized;
                var flee = threat.sqrMagnitude > 0.0001f ? threat.normalized : Vector2.zero;
                move = flee + tangent * 0.9f;
            }
            else
            {
                move = FindPickupDirection(position);
                if (move == Vector2.zero)
                {
                    var toCenter = walkable.center - position;
                    move = new Vector2(-toCenter.y, toCenter.x).normalized * 0.8f + toCenter.normalized * 0.2f;
                }
            }

            if (position.x < walkable.xMin + WallMargin) move.x += 1.2f;
            if (position.x > walkable.xMax - WallMargin) move.x -= 1.2f;
            if (position.y < walkable.yMin + WallMargin) move.y += 1.2f;
            if (position.y > walkable.yMax - WallMargin) move.y -= 1.2f;

            move = Vector2.ClampMagnitude(move, 1f);
            Move = move.sqrMagnitude > 0.0001f ? move : Vector2.up;

            // 贴脸时冲刺脱身（玩家自己也可以用 HUD 的冲刺按钮做同样的事）
            DashRequested = player.DashCooldownLeft <= 0f && NearestEnemyDistance(position) < AutoDashRange;
        }

        private Vector2 FindPickupDirection(Vector2 position)
        {
            var best = float.MaxValue;
            var target = Vector2.zero;
            var found = false;

            var pickups = _runner.Pickups;
            for (var i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (pickup == null)
                {
                    continue;
                }

                var delta = (Vector2)pickup.transform.position - position;
                var distance = delta.magnitude;
                if (distance > PickupRadius || distance >= best)
                {
                    continue;
                }

                best = distance;
                target = delta;
                found = true;
            }

            return found ? target.normalized : Vector2.zero;
        }

        private float NearestEnemyDistance(Vector2 position)
        {
            var nearest = float.MaxValue;
            var enemies = _runner.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var distance = Vector2.Distance(position, enemy.transform.position);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest;
        }
    }
}
