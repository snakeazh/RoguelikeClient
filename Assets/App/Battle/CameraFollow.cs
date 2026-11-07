using UnityEngine;

namespace App.Battle
{
    /// <summary>
    /// 2D 相机跟随：把镜头挂在玩家身上，并把可视矩形夹在地图边界内
    /// （走到地图边上时镜头停住，不会露出地图外的空白）。
    /// 只改 x/y，z 保持原值，避免把镜头推到 UI 相机所在的平面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target { get; private set; }
        public Rect Bounds { get; private set; }

        /// <summary>平滑时间，0 表示硬跟随（冒烟测试与第一帧用这个）。</summary>
        public float SmoothTime = 0.12f;

        private Camera _camera;
        private Vector3 _position;
        private Vector3 _velocity;
        private float _shakeLeft;
        private float _shakeDuration;
        private float _shakeAmplitude;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        /// <summary>屏幕震动（受击、爆炸）。amplitude 是最大偏移的世界单位。</summary>
        public void Shake(float amplitude, float duration)
        {
            // 连续受击时取更强的那一次，而不是叠加到失控
            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeLeft = Mathf.Max(_shakeLeft, duration);
        }

        /// <summary>绑定目标与地图边界，并立即把镜头对到目标上（避免开局从原点滑过去）。</summary>
        public void Configure(Transform target, Rect bounds)
        {
            Target = target;
            Bounds = bounds;
            _camera = GetComponent<Camera>();
            _position = transform.position;
            _velocity = Vector3.zero;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (Target == null)
            {
                return;
            }

            _position = ClampToBounds(new Vector3(Target.position.x, Target.position.y, transform.position.z));
            _velocity = Vector3.zero;
            transform.position = _position;
        }

        private void LateUpdate()
        {
            if (Target == null)
            {
                return;
            }

            var basePosition = ClampToBounds(new Vector3(Target.position.x, Target.position.y, _position.z));
            _position = SmoothTime <= 0f
                ? basePosition
                : Vector3.SmoothDamp(_position, basePosition, ref _velocity, SmoothTime);

            transform.position = _position + ShakeOffset();
        }

        /// <summary>衰减的随机抖动；用 unscaled 时间，暂停时也能抖完。</summary>
        private Vector3 ShakeOffset()
        {
            if (_shakeLeft <= 0f || _shakeAmplitude <= 0f)
            {
                return Vector3.zero;
            }

            _shakeLeft = Mathf.Max(0f, _shakeLeft - Time.unscaledDeltaTime);
            var falloff = _shakeDuration <= 0f ? 0f : _shakeLeft / _shakeDuration;
            var offset = Random.insideUnitCircle * (_shakeAmplitude * falloff);

            if (_shakeLeft <= 0f)
            {
                _shakeAmplitude = 0f;
                _shakeDuration = 0f;
            }

            return new Vector3(offset.x, offset.y, 0f);
        }

        private Vector3 ClampToBounds(Vector3 position)
        {
            var halfHeight = _camera != null ? _camera.orthographicSize : 5f;
            // 有横向黑边（camera.rect）时，真正可见的宽度要乘上视口宽度占比。
            var screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0.5625f;
            var viewportWidth = _camera != null ? Mathf.Max(0.01f, _camera.rect.width) : 1f;
            var halfWidth = halfHeight * screenAspect * viewportWidth;

            position.x = ClampAxis(position.x, Bounds.xMin + halfWidth, Bounds.xMax - halfWidth);
            position.y = ClampAxis(position.y, Bounds.yMin + halfHeight, Bounds.yMax - halfHeight);
            return position;
        }

        /// <summary>地图比视野还小时的兜底：整体居中，而不是把镜头夹到地图外。</summary>
        private static float ClampAxis(float value, float min, float max)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
        }
    }
}
