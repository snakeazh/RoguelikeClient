using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一次性特效：生成一个占位色块，在 <see cref="_life"/> 秒内缩放到目标尺寸并淡出，然后自毁。
    /// 只用于占位美术（雷电火花、剑气、爆炸圈），换成正式美术后可以整体替换。
    /// </summary>
    public sealed class TransientFx : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _color;
        private float _life;
        private float _elapsed;
        private Vector3 _fromScale;
        private Vector3 _toScale;

        private static Sprite s_softCircle;

        /// <summary>
        /// 扇形刀光：把 <see cref="BattleVisuals.SlashArc"/> 这张月牙弧贴图按 <paramref name="angleDegrees"/>
        /// （+X 为 0 度）摆到玩家身上，快速张开一点并淡出。用于大剑这类「朝面向方向挥砍」的武器。
        /// 没有刀光贴图时退化成一条细剑气，保证任何美术配置下都有反馈。
        /// </summary>
        public static void Fan(Vector3 origin, float angleDegrees, float radius, float spreadDegrees, Color color, float life = 0.18f, int order = 0)
        {
            var sprite = BattleVisuals.GetSprite(BattleVisuals.SlashArc);
            if (sprite == null)
            {
                Swipe(origin, angleDegrees, radius, Mathf.Max(0.2f, radius * 0.35f), color, life);
                return;
            }

            var go = new GameObject("CleaveFan");
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            // order 是相对 BattleSort.Fx 的偏移，保证特效永远压在角色精灵之上。
            sr.sortingOrder = BattleSort.Fx + order;

            // 贴图里弧的外缘正好是精灵的一半宽（Pivot 居中、PPU = 边长），所以直径 = 2 * radius。
            var from = Vector3.one * (radius * 2f * 0.92f);
            var to = Vector3.one * (radius * 2f * 1.08f);
            go.transform.localScale = from;
            Attach(sr, color, life, from, to);
        }

        /// <summary>原地闪现一个柔和的圆形光斑（命中、爆炸等）。</summary>
        public static void Flash(Vector3 position, float diameter, Color color, float life = 0.18f, int order = 0)
        {
            var go = new GameObject("Fx");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SoftCircle();
            sr.color = color;
            sr.sortingOrder = BattleSort.Fx + order;
            go.transform.position = position;

            var start = Vector3.one * (diameter * 0.55f);
            go.transform.localScale = start;
            Attach(sr, color, life, start, Vector3.one * (diameter * 1.45f));
        }

        /// <summary>剑气：一个朝某个方向的细长条，快速张开并淡出。</summary>
        public static void Swipe(Vector3 position, float angleDegrees, float length, float width, Color color, float life = 0.16f)
        {
            var sr = PlaceholderArt.CreateRenderer("Slash", null, PlaceholderArt.Square, color, BattleSort.Fx + 19);
            sr.transform.position = position;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
            sr.transform.localScale = new Vector3(width, length, 1f) / Mathf.Max(0.001f, length);
            Attach(sr, color, life, sr.transform.localScale, Vector3.Scale(sr.transform.localScale, new Vector3(1.4f, 1.15f, 1f)));
        }

        private static void Attach(SpriteRenderer sr, Color color, float life, Vector3 from, Vector3 to)
        {
            var fx = sr.gameObject.AddComponent<TransientFx>();
            fx._renderer = sr;
            fx._color = color;
            fx._life = Mathf.Max(0.02f, life);
            fx._fromScale = from;
            fx._toScale = to;
        }

        /// <summary>运行时生成的柔和圆斑贴图：中心实、边缘渐变到全透明，避免出现硬边白盘子。</summary>
        public static Sprite SoftCircle()
        {
            if (s_softCircle != null)
            {
                return s_softCircle;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "FxSoftCircle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - distance);
                    alpha = alpha * alpha * alpha;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_softCircle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            s_softCircle.name = "FxSoftCircle";
            return s_softCircle;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / _life);
            transform.localScale = Vector3.Lerp(_fromScale, _toScale, t);

            var c = _color;
            c.a = _color.a * (1f - t) * (1f - t);

            if (_renderer != null)
            {
                _renderer.color = c;
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
