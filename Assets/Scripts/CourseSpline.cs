using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace Battrail
{
    /// コース定義。SplineContainer をラップし、(s, t) → ワールド位置／回転を提供する。
    /// SplineContainer が空のときは簡易 S 字を埋める。Editor で knot を編集すればそれが優先される。
    /// 道路の帯メッシュを生成して MeshFilter に流し込み、コースを可視化する。
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    public class CourseSpline : MonoBehaviour
    {
        [Header("Course")]
        [SerializeField] float halfWidth = 3f;
        [Tooltip("プレイヤー中心を路面からどれだけ持ち上げるか（機体の高さの半分が目安）")]
        [SerializeField] float surfaceLift = 0.5f;

        [Header("Road mesh")]
        [SerializeField] int roadSegments = 200;
        [Tooltip("路面メッシュの Y。地面との z-fight を避けるためわずかに浮かせる")]
        [SerializeField] float roadY = 0.02f;

        SplineContainer _container;
        float _length;

        public float Length => _length;
        public float HalfWidth => halfWidth;

        void OnEnable()
        {
            EnsureContainer();
            if (_container.Spline == null || _container.Spline.Count == 0)
                BuildDefaultCourse();
            RecalculateLength();
            BuildRoadMesh();
        }

        public Vector3 GetWorldPosition(float s, float t)
        {
            EvaluateAtDistance(s, out var position, out _, out var right, out _);
            return position + right * t + Vector3.up * surfaceLift;
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

        void EnsureContainer()
        {
            if (_container == null)
                _container = GetComponent<SplineContainer>();
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

        void BuildRoadMesh()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null || _length <= 0f)
                return;

            int segments = Mathf.Max(2, roadSegments);
            int vertCount = (segments + 1) * 2;
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float s = _length * i / segments;
                EvaluateAtDistance(s, out var pos, out _, out var right, out _);
                var lift = Vector3.up * roadY;
                vertices[i * 2] = pos + right * halfWidth + lift;
                vertices[i * 2 + 1] = pos - right * halfWidth + lift;
                float v = (float)i / segments;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);

                if (i < segments)
                {
                    // 上向き法線になるよう巻き順を CCW（上から見て）にする。
                    int t = i * 6;
                    int baseIndex = i * 2;
                    triangles[t] = baseIndex;
                    triangles[t + 1] = baseIndex + 1;
                    triangles[t + 2] = baseIndex + 2;
                    triangles[t + 3] = baseIndex + 1;
                    triangles[t + 4] = baseIndex + 3;
                    triangles[t + 5] = baseIndex + 2;
                }
            }

            var mesh = new Mesh { name = "CourseRoad" };
            mesh.indexFormat = vertCount > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            EnsureContainer();
            if (_container == null || _container.Spline == null)
                return;
            RecalculateLength();
            BuildRoadMesh();
        }
#endif
    }
}
