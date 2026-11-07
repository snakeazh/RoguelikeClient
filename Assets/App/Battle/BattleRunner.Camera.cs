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
        // ---------- 工具 ----------

        /// <summary>
        /// 地图尺寸来自 <see cref="BattleMap"/>（18×36），不再由相机反推一屏。
        /// 相机只负责“看地图的一块”，由 <see cref="CameraFollow"/> 跟随玩家并夹在地图边界内。
        /// </summary>
        private void SetupMap()
        {
            _arena = BattleMap.Bounds;
            _walkable = BattleMap.Walkable;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                // 竖屏 9:16 时可见 5.63 × 10；地图 18×36，所以横向约 3.2 屏、纵向约 3.6 屏。
                camera.orthographicSize = 5f;
                ApplyAspectGuard(camera);
            }
        }

        /// <summary>竖屏游戏在宽屏窗口里会被“看穿”整张地图（可视宽度 ≥ 地图宽度时镜头夹取只剩中心），
        /// 这里把游戏相机限制成 9:16 的中间一列（两侧留黑），保证任何分辨率下都有可跟随的横向余量。</summary>
        private static void ApplyAspectGuard(Camera camera)
        {
            const float TargetAspect = 9f / 16f;
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1f;

            if (screenAspect <= TargetAspect + 0.001f)
            {
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                return;
            }

            var width = TargetAspect / screenAspect;
            camera.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        /// <summary>窗口被拉伸 / 最大化后重新算一次黑边，避免镜头夹取范围失效。</summary>
        private void RefreshAspectGuard()
        {
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0f;
            if (Mathf.Abs(screenAspect - _appliedScreenAspect) < 0.001f)
            {
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            _appliedScreenAspect = screenAspect;
            ApplyAspectGuard(camera);
        }

        private void AttachCameraFollow(Transform target)
        {
            var camera = Camera.main;
            if (camera == null || target == null)
            {
                return;
            }

            _cameraFollow = camera.GetComponent<CameraFollow>();
            if (_cameraFollow == null)
            {
                _cameraFollow = camera.gameObject.AddComponent<CameraFollow>();
            }

            _cameraFollow.Configure(target, _arena);
        }

        public bool IsInsideArena(Vector3 position, float margin)
        {
            return position.x >= _arena.xMin - margin
                   && position.x <= _arena.xMax + margin
                   && position.y >= _arena.yMin - margin
                   && position.y <= _arena.yMax + margin;
        }

        /// <summary>整张地图（含墙）。</summary>
        public Rect Arena => _arena;

        /// <summary>墙内侧的可走范围。</summary>
        public Rect Walkable => _walkable;

        /// <summary>相机可视区域（用于“刷在屏幕外”的判断）。</summary>
        public Rect CameraView(float padding = 0f)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return new Rect(-2.81f - padding, -5f - padding, 5.63f + padding * 2f, 10f + padding * 2f);
            }

            var halfHeight = camera.orthographicSize + padding;
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0.5625f;
            var halfWidth = halfHeight * screenAspect * Mathf.Max(0.01f, camera.rect.width);
            var center = camera.transform.position;
            return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        }
    }
}
