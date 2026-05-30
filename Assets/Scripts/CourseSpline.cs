using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Battrail
{
    /// コース定義。SplineContainer をラップし、(s, t) → ワールド位置／回転を提供する。
    /// SplineContainer が空のときは Awake で簡易 S 字を埋める。Editor で knot を編集すればそれが優先される。
    [RequireComponent(typeof(SplineContainer))]
    public class CourseSpline : MonoBehaviour
    {
        [SerializeField] float halfWidth = 3f;

        SplineContainer _container;
        float _length;

        public float Length => _length;
        public float HalfWidth => halfWidth;

        void Awake()
        {
            _container = GetComponent<SplineContainer>();
            if (_container.Spline == null || _container.Spline.Count == 0)
                BuildDefaultCourse();
            RecalculateLength();
        }

        public Vector3 GetWorldPosition(float s, float t)
        {
            EvaluateAtDistance(s, out var position, out _, out var right, out _);
            return position + right * t;
        }

        public Quaternion GetRotation(float s)
        {
            EvaluateAtDistance(s, out _, out var tangent, out _, out var up);
            return Quaternion.LookRotation(tangent, up);
        }

        void EvaluateAtDistance(float s, out Vector3 position, out Vector3 tangent, out Vector3 right, out Vector3 up)
        {
            float length = Mathf.Max(_length, 0.0001f);
            float normalized = Mathf.Clamp01(s / length);
            _container.Evaluate(normalized, out var p, out var t, out var u);
            position = (Vector3)p;
            tangent = math.lengthsq(t) > 0f ? math.normalize(t) : new float3(0, 0, 1);
            up = math.lengthsq(u) > 0f ? math.normalize(u) : new float3(0, 1, 0);
            right = math.normalize(math.cross(up, tangent));
        }

        void BuildDefaultCourse()
        {
            var spline = _container.Spline;
            spline.Clear();
            spline.Add(new BezierKnot(new float3(0f, 0f, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(6f, 0f, 18f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(-6f, 0f, 36f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(8f, 0f, 60f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(-8f, 0f, 90f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(0f, 0f, 120f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(0f, 0f, 160f)), TangentMode.AutoSmooth);
        }

        void RecalculateLength()
        {
            _length = _container.CalculateLength();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_container == null) _container = GetComponent<SplineContainer>();
            if (_container != null && _container.Spline != null)
                RecalculateLength();
        }
#endif
    }
}
