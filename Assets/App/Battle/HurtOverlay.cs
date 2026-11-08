using UnityEngine;
using UnityEngine.UI;

namespace App.Battle
{
    /// <summary>
    /// 玩家受击时的红色边缘泛光：贴一张运行时生成的径向渐变图（中间透明、四周红），
    /// 受击时把透明度推上去再自己衰减。挂在 UIRoot 的 Hud 层，跟着屏幕不跟世界。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class HurtOverlay : MonoBehaviour
    {
        private const float DecaySpeed = 4.2f;
        private const float MaxAlpha = 0.45f;
        private const int TextureSize = 128;

        private static Sprite s_vignette;

        /// <summary>释放运行时生成的晕影贴图（战斗结束时调用）。</summary>
        public static void ResetCache()
        {
            if (s_vignette == null)
            {
                return;
            }

            var texture = s_vignette.texture;
            if (Application.isPlaying)
            {
                Destroy(s_vignette);
                if (texture != null)
                {
                    Destroy(texture);
                }
            }
            else
            {
                DestroyImmediate(s_vignette);
                if (texture != null)
                {
                    DestroyImmediate(texture);
                }
            }

            s_vignette = null;
        }

        private Image _image;
        private float _alpha;

        public static HurtOverlay Create(RectTransform parent)
        {
            if (parent == null)
            {
                return null;
            }

            var go = new GameObject("HurtOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();

            var image = go.GetComponent<Image>();
            image.sprite = Vignette();
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.color = new Color(0.95f, 0.12f, 0.14f, 0f);

            var overlay = go.AddComponent<HurtOverlay>();
            overlay._image = image;
            return overlay;
        }

        /// <summary>闪一下，strength 0~1（血越少或伤害越高越明显）。</summary>
        public void Pulse(float strength)
        {
            _alpha = Mathf.Max(_alpha, Mathf.Clamp01(strength));
        }

        private void Update()
        {
            if (_image == null)
            {
                return;
            }

            _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime * DecaySpeed);
            var color = _image.color;
            color.a = _alpha * MaxAlpha;
            _image.color = color;
        }

        /// <summary>径向渐变：中心透明、边缘接近纯色，形成「屏幕四周泛红」的观感。</summary>
        private static Sprite Vignette()
        {
            if (s_vignette != null)
            {
                return s_vignette;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "HurtVignette",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var half = TextureSize * 0.5f;
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var dx = (x + 0.5f - half) / half;
                    var dy = (y + 0.5f - half) / half;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);

                    var alpha = Mathf.Clamp01((distance - 0.68f) / 0.32f);
                    alpha = alpha * alpha * alpha;

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            s_vignette = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
            s_vignette.name = "HurtVignette";
            return s_vignette;
        }
    }
}
