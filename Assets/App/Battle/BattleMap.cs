using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 战斗地图的尺寸约定。这里是唯一的地图尺寸来源：
    /// - <see cref="Bounds"/> 是整张地图（含墙）；
    /// - <see cref="Walkable"/> 是墙内侧、角色真正能走的范围；
    /// - 相机可见范围（约 5.6 × 10）远小于地图，所以镜头会跟着玩家移动。
    /// 想改地图大小只改这里，地面/墙/相机边界/刷怪点都会自动跟着变。
    /// </summary>
    public static class BattleMap
    {
        /// <summary>地图宽度（世界单位）。竖屏可见宽约 5.6，所以这是约 3.2 屏宽。</summary>
        public const float Width = 18f;

        /// <summary>地图高度（世界单位，约 3.6 屏高）。</summary>
        public const float Height = 36f;

        /// <summary>边界墙的视觉厚度（也是角色与地图边缘的最小距离）。</summary>
        public const float WallThickness = 0.62f;

        /// <summary>整张地图（含墙）。</summary>
        public static Rect Bounds => new Rect(-Width * 0.5f, -Height * 0.5f, Width, Height);

        /// <summary>可走范围（墙内侧）。</summary>
        public static Rect Walkable => new Rect(
            Bounds.xMin + WallThickness,
            Bounds.yMin + WallThickness,
            Width - WallThickness * 2f,
            Height - WallThickness * 2f);

        /// <summary>把任意坐标夹进可走范围（敌人的环形刷新可能落在墙外）。</summary>
        public static Vector2 ClampToWalkable(Vector2 position, float margin = 0f)
        {
            var rect = Walkable;
            return new Vector2(
                Mathf.Clamp(position.x, rect.xMin + margin, rect.xMax - margin),
                Mathf.Clamp(position.y, rect.yMin + margin, rect.yMax - margin));
        }
    }
}
