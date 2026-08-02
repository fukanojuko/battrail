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

        [Header("Curvature")]
        [Tooltip("曲率テーブルのサンプル間隔。前後 1 サンプル分の接線差から求めるので、" +
                 "小さくするほど局所的な曲率を拾い、大きくするほど均される")]
        [SerializeField] float curvatureSampleSpacing = 4f;

        [Header("Road mesh")]
        [Tooltip("路面メッシュ 1 セグメントあたりの長さ。コースを伸ばしても分割密度が保たれるよう、固定分割数ではなく長さ基準で持つ")]
        [SerializeField] float roadSegmentLength = 1.5f;
        [Tooltip("路面メッシュの Y。地面との z-fight を避けるためわずかに浮かせる")]
        [SerializeField] float roadY = 0.02f;

        SplineContainer _container;
        float _length;
        float[] _curvature;

        public float Length => _length;
        public float HalfWidth => halfWidth;

        private void OnEnable()
        {
            EnsureContainer();
            if (_container.Spline == null || _container.Spline.Count == 0)
                BuildDefaultCourse();
            RecalculateLength();
            BuildCurvatureTable();
            BuildRoadMesh();
        }

        /// s 地点の曲率 [rad/unit]。中心線の向きが 1 進むごとに何ラジアン変わるか。
        /// 直線が 0 で、大きいほど急カーブ（回転半径は 1/曲率）。
        /// 毎フレーム呼ばれるので、スプラインを都度評価せず事前計算したテーブルを線形補間する。
        public float GetCurvature(float s)
        {
            if (_curvature == null || _curvature.Length < 2 || _length <= 0f)
                return 0f;

            float spacing = _length / (_curvature.Length - 1);
            float u = Mathf.Clamp(s / spacing, 0f, _curvature.Length - 1);
            int i = Mathf.FloorToInt(u);
            int next = Mathf.Min(i + 1, _curvature.Length - 1);
            return Mathf.Lerp(_curvature[i], _curvature[next], u - i);
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

        /// 既定コース。ヘアピンを 2 箇所入れて往路・復路・最終路の 3 本が並走する形にしてある。
        /// 一方向に蛇行するだけだと、上から見ても走っていても「まっすぐな廊下」にしか見えないため。
        /// カーブ自体に走行上の難度は無い（移動はスプライン相対で、コーナリング物理を持たない）ので、
        /// 形は見た目のための要素。ヘアピンの回転半径は路面幅（halfWidth）より十分大きく取り、
        /// 内側でメッシュが自己交差しないようにしている。
        static readonly float3[] DefaultKnots =
        {
            // 導入: 緩い S 字でウォームアップ（平坦）
            new(0f, 0f, 0f),
            new(6f, 0f, 18f),
            new(-6f, 0f, 36f),
            new(8f, 0f, 60f),
            new(-8f, 0f, 90f),
            new(0f, 0f, 120f),
            new(0f, 0f, 160f),

            // 往路: 右へ振りながら上る高速セクション。ブーストの使いどころ
            new(15f, 2f, 205f),
            new(40f, 5f, 250f),
            new(70f, 8f, 290f),

            // 第 1 ヘアピン: 最高所を右回りに 180°、進行方向が -z へ反転する
            new(105f, 11f, 325f),
            new(145f, 13f, 360f),
            new(170f, 14f, 405f),
            new(165f, 14f, 450f),
            new(130f, 13f, 480f),
            new(90f, 12f, 470f),
            new(60f, 11f, 440f),

            // 復路: 下りながら往路の脇を逆走して戻る
            new(30f, 9f, 395f),
            new(5f, 7f, 345f),
            new(-15f, 5f, 290f),
            new(-25f, 3f, 235f),

            // 第 2 ヘアピン: 最低所を左回りに 180°、再び +z へ向き直る
            new(-40f, 2f, 185f),
            new(-70f, 1f, 150f),
            new(-105f, 1f, 150f),
            new(-135f, 2f, 185f),
            new(-150f, 3f, 235f),

            // 最終: 平坦に戻して長い追い抜きセクション
            new(-155f, 4f, 300f),
            new(-155f, 4f, 370f),
            new(-150f, 3f, 440f),
            new(-140f, 2f, 510f),
            new(-125f, 1f, 580f),
            new(-105f, 0f, 640f),
            new(-80f, 0f, 690f),
            new(-50f, 0f, 730f),
        };

        void BuildDefaultCourse()
        {
            var spline = _container.Spline;
            spline.Clear();
            foreach (var knot in DefaultKnots)
                spline.Add(new BezierKnot(knot), TangentMode.AutoSmooth);
        }

        void RecalculateLength()
        {
            _length = _container.CalculateLength();
        }

        void BuildCurvatureTable()
        {
            if (_length <= 0f)
            {
                _curvature = null;
                return;
            }

            int count = Mathf.Clamp(
                Mathf.RoundToInt(_length / Mathf.Max(0.5f, curvatureSampleSpacing)), 2, 8000) + 1;
            _curvature = new float[count];

            float step = _length / (count - 1);
            for (int i = 0; i < count; i++)
            {
                // 端では前後どちらかが切り詰められるので、実際に使った区間長で割る。
                float a = Mathf.Max(0f, step * i - step);
                float b = Mathf.Min(_length, step * i + step);
                EvaluateAtDistance(a, out _, out var tangentA, out _, out _);
                EvaluateAtDistance(b, out _, out var tangentB, out _, out _);
                _curvature[i] = Vector3.Angle(tangentA, tangentB) * Mathf.Deg2Rad
                                / Mathf.Max(0.001f, b - a);
            }
        }

        void BuildRoadMesh()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null || _length <= 0f)
                return;

            int segments = Mathf.Clamp(Mathf.RoundToInt(_length / Mathf.Max(0.1f, roadSegmentLength)), 2, 8000);
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

            // 手続き生成メッシュをシーンに焼き込まない（保存のたびに差分が出るのを防ぐ）。
            // OnEnable でロード時に再生成される。
            var mesh = new Mesh { name = "CourseRoad", hideFlags = HideFlags.DontSave };
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
        private void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;
            EnsureContainer();
            if (_container == null || _container.Spline == null)
                return;
            RecalculateLength();
            BuildCurvatureTable();
            BuildRoadMesh();
        }
#endif
    }
}
