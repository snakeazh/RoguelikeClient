using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 一闪而过的电弧：用 <see cref="LineRenderer"/> 画一条带抖动折线的闪电，两端加光斑，几十毫秒内淡出。
    /// 由雷电法典（Chain 形态）在每次跳跃时调用，见 <see cref="WeaponRuntime"/>。
    ///
    /// 池化：一次连锁会同时画出好几道电弧，每次 `new GameObject` + `AddComponent` 都会留垃圾，
    /// 这里和 <see cref="TransientFx"/> 走同一套 <see cref="ViewPool{T}"/>。
    /// </summary>
    public sealed class LightningFx : MonoBehaviour
    {
        private LineRenderer _line;
        private Color _color;
        private float _life;
        private float _elapsed;
        private float _flicker;

        private static Material s_material;
        private static ViewPool<LightningFx> s_pool;

        /// <summary>释放共享的白色描边材质与池内对象（战斗结束时调用，下次用到会重建）。</summary>
        public static void ResetCache()
        {
            s_pool?.Clear();
            s_pool = null;

            if (s_material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(s_material);
            }
            else
            {
                DestroyImmediate(s_material);
            }

            s_material = null;
        }

        /// <summary>在两点之间画一道电弧。</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color color, float life = 0.14f, int order = 0)
        {
            var delta = to - from;
            var distance = delta.magnitude;
            if (distance < 0.05f)
            {
                return;
            }

            var fx = Pool().Rent();
            if (fx == null)
            {
                return;
            }

            fx.Play(from, to, delta, distance, color, life, order);

            TransientFx.Flash(from, 0.7f, color, 0.1f, order);
            TransientFx.Flash(to, 1.1f, color, 0.14f, order);
        }

        private static ViewPool<LightningFx> Pool()
        {
            if (s_pool != null)
            {
                return s_pool;
            }

            s_pool = new ViewPool<LightningFx>(null, CreatePooled, 32);
            return s_pool;
        }

        private static LightningFx CreatePooled()
        {
            var go = new GameObject("Lightning");
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.material = LineMaterial();
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Stretch;

            var fx = go.AddComponent<LightningFx>();
            fx._line = line;
            return fx;
        }

        private void Play(Vector3 from, Vector3 to, Vector3 delta, float distance, Color color, float life, int order)
        {
            _color = color;
            _life = Mathf.Max(0.03f, life);
            _elapsed = 0f;
            _flicker = Random.Range(9f, 16f);

            var line = _line;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, color.a * 0.7f);
            line.widthMultiplier = Mathf.Clamp(distance * 0.045f, 0.03f, 0.09f);
            line.sortingOrder = BattleSort.Lightning + order;

            // 折线：两端保持不动，中间点沿法线抖动，越靠中间抖得越厉害。
            // 用 SetPosition 逐点写入，避免每次电弧都 new 一个 Vector3[]。
            var segments = Mathf.Clamp(Mathf.RoundToInt(distance / 0.28f), 4, 14);
            var normal = new Vector2(-delta.y, delta.x).normalized;
            var amplitude = Mathf.Clamp(distance * 0.09f, 0.05f, 0.22f);

            line.positionCount = segments + 1;
            for (var i = 0; i <= segments; i++)
            {
                var t = i / (float)segments;
                var point = Vector3.Lerp(from, to, t);
                if (i > 0 && i < segments)
                {
                    var offset = normal * (Random.Range(-1f, 1f) * amplitude * Mathf.Sin(t * Mathf.PI));
                    point += (Vector3)offset;
                }

                line.SetPosition(i, point);
            }
        }

        private static Material LineMaterial()
        {
            if (s_material != null)
            {
                return s_material;
            }

            // Sprites/Default 是 2D 工程必带的无光照 shader，播放器里也能取到
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("UI/Default");
            }

            s_material = new Material(shader)
            {
                name = "FxLightning"
            };

            // 关键：从 shader 直接 new 出来的材质，_MainTex 可能是空贴图（会被采样成黑色），
            // 电弧在深色地面上就完全看不见了，所以显式给一张白图，让顶点色决定颜色。
            s_material.mainTexture = Texture2D.whiteTexture;
            return s_material;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / _life);

            // 闪电的闪烁感：快速明暗跳变，整体随时间衰减
            var flicker = 0.75f + 0.25f * Mathf.Sin(_elapsed * _flicker * Mathf.PI * 2f);
            var alpha = (1f - t) * (1f - t) * flicker;

            if (_line != null)
            {
                var start = _color;
                start.a = _color.a * alpha;
                var end = _color;
                end.a = _color.a * alpha * 0.7f;
                _line.startColor = start;
                _line.endColor = end;
            }

            if (t >= 1f)
            {
                Pool().Return(this);
            }
        }
    }
}
