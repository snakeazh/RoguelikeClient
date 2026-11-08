using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 命中飘字：从目标头上冒出的伤害数字，上浮 + 淡出。
    /// 同屏实体很多，所以做成固定容量的对象池（池满时直接丢弃，不会无限 new）。
    /// </summary>
    public sealed class DamagePopup : MonoBehaviour
    {
        private const int PoolSize = 48;
        private const float Lifetime = 0.55f;
        private const float RiseDistance = 0.8f;

        // TMP 3D 文本的字号是世界单位级别的，48 号字配 0.028 缩放约等于 0.35 世界单位高（≈67px）。
        private const float FontSize = 48f;
        private const float WorldScale = 0.028f;

        private static readonly List<DamagePopup> s_pool = new List<DamagePopup>(PoolSize);
        private static Transform s_root;
        private static int s_cursor;

        /// <summary>
        /// 丢弃整个池与根节点引用。UI 层随场景销毁后必须调用，
        /// 否则下一局会往已经销毁的父节点里挂字（表现为飘字再也不出现 + MissingReference）。
        /// </summary>
        public static void ResetPool()
        {
            ClearAll();
            s_pool.Clear();
            s_root = null;
            s_cursor = 0;
        }

        private TextMeshPro _text;
        private Vector3 _origin;
        private Vector3 _drift;
        private Color _color;
        private float _elapsed;
        private float _size;

        /// <summary>在某个世界坐标弹一个数字。amount 四舍五入后显示，&lt;1 不弹（避免刷屏）。</summary>
        public static void Show(Vector3 worldPosition, float amount, bool crit, Color color)
        {
            var value = Mathf.RoundToInt(amount);
            if (value < 1)
            {
                return;
            }

            var popup = Rent();
            if (popup == null)
            {
                return;
            }

            popup.Play(worldPosition, value.ToString(), crit, color);
        }

        /// <summary>战斗结束时把还活着的飘字收回去。</summary>
        public static void ClearAll()
        {
            foreach (var popup in s_pool)
            {
                if (popup != null)
                {
                    popup.gameObject.SetActive(false);
                }
            }
        }

        private static DamagePopup Rent()
        {
            for (var i = 0; i < s_pool.Count; i++)
            {
                var index = (s_cursor + i) % s_pool.Count;
                var candidate = s_pool[index];
                if (candidate == null)
                {
                    s_pool.RemoveAt(index);
                    i--;
                    continue;
                }

                if (!candidate.gameObject.activeSelf)
                {
                    s_cursor = (index + 1) % s_pool.Count;
                    return candidate;
                }
            }

            if (s_pool.Count >= PoolSize)
            {
                return null;
            }

            var popup = Create();
            s_pool.Add(popup);
            s_cursor = s_pool.Count % PoolSize;
            return popup;
        }

        private static DamagePopup Create()
        {
            if (s_root == null)
            {
                var root = new GameObject("DamageNumbers");
                s_root = root.transform;
            }

            var go = new GameObject("DamagePopup", typeof(TextMeshPro));
            go.transform.SetParent(s_root, false);

            var text = go.GetComponent<TextMeshPro>();
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Bold;
            text.text = "0";
            text.sortingOrder = BattleSort.Popup;
            text.raycastTarget = false;

            var popup = go.AddComponent<DamagePopup>();
            popup._text = text;
            go.SetActive(false);
            return popup;
        }

        private void Play(Vector3 worldPosition, string value, bool crit, Color color)
        {
            transform.position = worldPosition;
            transform.localScale = Vector3.one * WorldScale * (crit ? 1.35f : 1f);
            _origin = worldPosition;
            _drift = new Vector3(Random.Range(-0.18f, 0.18f), 0f, 0f);
            _color = color;
            _elapsed = 0f;
            _size = crit ? 1.15f : 1f;

            if (_text != null)
            {
                _text.text = crit ? value + "!" : value;
                _text.color = color;
                _text.ForceMeshUpdate();
            }

            gameObject.SetActive(true);
        }

        private void Update()
        {
            // 用 unscaled 时间：升级面板把世界停住时，飘字也要继续走完。
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / Lifetime);

            // 先快后慢地上浮，末尾 45% 淡出
            var rise = 1f - (1f - t) * (1f - t);
            transform.position = _origin + Vector3.up * (RiseDistance * rise) + _drift * t;

            var pop = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI * 0.5f);
            transform.localScale = Vector3.one * WorldScale * _size * (0.6f + 0.4f * pop);

            if (_text != null)
            {
                var c = _color;
                c.a = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
                _text.color = c;
            }

            if (t >= 1f)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
