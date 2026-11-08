using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 纯色几何占位美术：运行时生成圆形/方形/三角形 Sprite，不依赖任何美术资源。
    /// 等正式美术就位后，把这些 Sprite 换成 Resources/Res 里的图即可（调用点都在 EnemyUnit/PlayerUnit 里）。
    /// </summary>
    public static class PlaceholderArt
    {
        private static Sprite _circle;
        private static Sprite _square;
        private static Sprite _triangle;
        private static Sprite _ring;

        public static Sprite Circle => _circle != null ? _circle : (_circle = BuildCircle(64));
        public static Sprite Square => _square != null ? _square : (_square = BuildSquare(64));
        public static Sprite Triangle => _triangle != null ? _triangle : (_triangle = BuildTriangle(64));
        public static Sprite Ring => _ring != null ? _ring : (_ring = BuildRing(128));

        /// <summary>
        /// 释放运行时生成的贴图。战斗结束/退出播放模式时调用：
        /// 这些 Sprite 与 Texture2D 不走资源系统，不主动销毁会一直累积（编辑器里尤其明显）。
        /// 下一次用到时会重新生成，不需要预热。
        /// </summary>
        public static void ResetCache()
        {
            Release(ref _circle);
            Release(ref _square);
            Release(ref _triangle);
            Release(ref _ring);
        }

        private static void Release(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                Object.Destroy(sprite);
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
            else
            {
                Object.DestroyImmediate(sprite);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }
            }

            sprite = null;
        }

        /// <summary>创建一个 SpriteRenderer（排序由 order 控制）。</summary>
        public static SpriteRenderer CreateRenderer(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            float diameter,
            int order = 0,
            Vector3 localPosition = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;

            if (diameter > 0f)
            {
                go.transform.localScale = Vector3.one * diameter;
            }

            return sr;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out var color) ? color : fallback;
        }

        private static Sprite BuildCircle(int size)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var radius = size * 0.5f - 1f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(radius - dist);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite BuildSquare(int size)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var pad = Mathf.Max(2, size / 16);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = x >= pad && x < size - pad && y >= pad && y < size - pad;
                    pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite BuildTriangle(int size)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                var t = y / (float)(size - 1);
                var halfWidth = (1f - t) * (size * 0.5f - 2f);
                var center = (size - 1) * 0.5f;
                for (var x = 0; x < size; x++)
                {
                    var inside = Mathf.Abs(x - center) <= halfWidth;
                    pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite BuildRing(int size)
        {
            var tex = NewTexture(size, size);
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var outer = size * 0.5f - 1f;
            var inner = outer - size * 0.06f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var inside = dist <= outer && dist >= inner;
                    pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Texture2D NewTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return tex;
        }
    }
}
