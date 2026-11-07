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
        // ---------- 输入 ----------
        private Vector2 ReadMove(out bool dash)
        {
            dash = false;
            Vector2 move = Vector2.zero;

            if (AutoPlay || BattleInput.AutoPlay)
            {
                if (BattleInput.AutoDash)
                {
                    BattleInput.AutoDash = false;
                    dash = true;
                }

                move = ComputeAutoMove();
                if (move == Vector2.zero)
                {
                    move = Vector2.up;
                }

                if (Player != null && Player.DashCooldownLeft <= 0f && NearestEnemyDistance() < 1.4f)
                {
                    dash = true;
                }

                return move;
            }

            // 摇杆区域要按“游戏画面”算，而不是按整个窗口算：
            // 宽屏时相机会被裁成 9:16 的中间一列（camera.rect），窗口左右是黑边，
            // 如果还拿 Screen.width/2 当分界线，画面中间按下去会落在“右半屏”里，
            // 表现就是宽屏下按了没反应、窄屏正常。
            var camera = Camera.main;
            var view = camera != null && camera.pixelWidth > 0 && camera.pixelHeight > 0
                ? camera.pixelRect
                : new Rect(0f, 0f, Screen.width, Screen.height);

            var pointer = BattleInput.PointerPosition;
            var insideView = view.width >= 1f && view.height >= 1f && view.Contains(pointer);

            if (BattleInput.PointerDown && insideView && pointer.x < view.xMin + view.width * 0.5f)
            {
                _joystickOrigin = pointer;
                _joystickActive = true;
            }

            if (BattleInput.PointerHeld && _joystickActive)
            {
                if (insideView)
                {
                    var radius = Mathf.Max(60f, view.height * 0.11f);
                    var delta = (pointer - _joystickOrigin) / radius;
                    move = Vector2.ClampMagnitude(delta, 1f);
                }
            }
            else
            {
                _joystickActive = false;
            }

            // 键盘兜底（PC 调试）
            if (move == Vector2.zero)
            {
                var x = Input.GetAxisRaw("Horizontal");
                var y = Input.GetAxisRaw("Vertical");
                move = new Vector2(x, y);
            }

            dash = BattleInput.ConsumeDash();
            return move;
        }

        // ---------- 自动战斗（冒烟测试用） ----------

        /// <summary>简单的逃跑/捡糖机器人：躲开近处敌人，安全时去捡经验，否则绕场中心转圈。</summary>
        private Vector2 ComputeAutoMove()
        {
            if (Player == null)
            {
                return Vector2.zero;
            }

            var position = (Vector2)Player.transform.position;
            var threat = Vector2.zero;
            var threatCount = 0;

            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                var offset = position - (Vector2)enemy.transform.position;
                var distance = offset.magnitude;
                if (distance > 4.5f)
                {
                    continue;
                }

                threatCount++;
                threat += offset / Mathf.Max(0.3f, distance * distance);
            }

            Vector2 move;
            if (threatCount > 0)
            {
                // 一边逃跑一边绕场中心切向移动，避免被逼到墙角。
                var toCenter = _walkable.center - position;
                var tangent = new Vector2(-toCenter.y, toCenter.x).normalized;
                var flee = threat.sqrMagnitude > 0.0001f ? threat.normalized : Vector2.zero;
                move = flee + tangent * 0.9f;
            }
            else
            {
                move = FindPickupDirection(position);
                if (move == Vector2.zero)
                {
                    // 没有威胁也没有糖：绕场中心切向移动，避免贴墙。
                    var toCenter = _walkable.center - position;
                    move = new Vector2(-toCenter.y, toCenter.x).normalized * 0.8f + toCenter.normalized * 0.2f;
                }
            }

            var margin = 1.5f;
            if (position.x < _walkable.xMin + margin) move.x += 1.2f;
            if (position.x > _walkable.xMax - margin) move.x -= 1.2f;
            if (position.y < _walkable.yMin + margin) move.y += 1.2f;
            if (position.y > _walkable.yMax - margin) move.y -= 1.2f;

            return Vector2.ClampMagnitude(move, 1f);
        }

        private Vector2 FindPickupDirection(Vector2 position)
        {
            var best = float.MaxValue;
            var target = Vector2.zero;
            var found = false;

            for (var i = 0; i < _pickups.Count; i++)
            {
                var pickup = _pickups[i];
                if (pickup == null)
                {
                    continue;
                }

                var delta = (Vector2)pickup.transform.position - position;
                var distance = delta.magnitude;
                if (distance > 6f || distance >= best)
                {
                    continue;
                }

                best = distance;
                target = delta;
                found = true;
            }

            return found ? target.normalized : Vector2.zero;
        }

        private float NearestEnemyDistance()
        {
            if (Player == null)
            {
                return float.MaxValue;
            }

            var position = (Vector2)Player.transform.position;
            var nearest = float.MaxValue;
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
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
