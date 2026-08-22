using UnityEngine;
using UnityEngine.UIElements;

namespace Battrail
{
    /// コース全体を真上から見た形で描くミニマップ。
    /// コース線は Painter2D で描き、走者アイコンは通常の VisualElement を動かす。
    /// 線とアイコンを分けているのは、走者が動くたびにコース線まで描き直さないため。
    public class MinimapElement : VisualElement
    {
        /// コース線のサンプル数。ヘアピンが角張らない程度で、毎フレーム描かないので多めに取れる。
        const int SampleCount = 256;
        /// 線幅ぶん端が切れないよう内側に取る余白。
        const float Padding = 8f;
        const float DotSize = 8f;
        const float SelfDotSize = 12f;

        readonly VisualElement _track;
        readonly VisualElement[] _dots = new VisualElement[2];
        readonly int _selfIndex;

        /// コース中心線のワールド XZ。null ならコース未解決。
        Vector2[] _points;
        CourseSpline _course;
        float _courseLength;

        Vector2 _worldCenter;
        Vector2 _worldSize;
        Vector2 _viewCenter;
        float _scale = 1f;

        readonly Vector2[] _racerWorld = new Vector2[2];
        readonly bool[] _racerVisible = new bool[2];

        /// selfIndex: このミニマップを表示するパネルの持ち主。そのアイコンだけ大きく描く。
        public MinimapElement(int selfIndex)
        {
            _selfIndex = selfIndex;
            pickingMode = PickingMode.Ignore;
            AddToClassList("minimap");

            _track = new VisualElement { pickingMode = PickingMode.Ignore };
            _track.AddToClassList("minimap-track");
            _track.generateVisualContent += DrawCourse;
            Add(_track);

            for (int i = 0; i < _dots.Length; i++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("minimap-dot");
                dot.AddToClassList(i == 0 ? "minimap-dot--p1" : "minimap-dot--p2");
                if (i == selfIndex)
                    dot.AddToClassList("minimap-dot--self");
                dot.AddToClassList("hidden");
                _dots[i] = dot;
                Add(dot);
            }

            RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        /// 毎フレームの頭で呼ぶ。以降 SetRacer が来なかった走者はアイコンを隠す。
        public void BeginFrame()
        {
            for (int i = 0; i < _racerVisible.Length; i++)
                _racerVisible[i] = false;
        }

        /// コースが差し替わった／長さが変わったときだけ線を取り直す。
        public void SetCourse(CourseSpline course)
        {
            if (course == null || course.Length <= 0f)
            {
                if (_points == null)
                    return;
                _points = null;
                _course = null;
                _courseLength = 0f;
                Refresh();
                return;
            }

            if (_course == course && Mathf.Approximately(_courseLength, course.Length))
                return;

            _course = course;
            _courseLength = course.Length;
            _points = new Vector2[SampleCount];
            for (int i = 0; i < SampleCount; i++)
            {
                float s = _courseLength * i / (SampleCount - 1);
                var world = course.GetWorldPosition(s, 0f);
                _points[i] = new Vector2(world.x, world.z);
            }

            var min = _points[0];
            var max = _points[0];
            foreach (var p in _points)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            _worldCenter = (min + max) * 0.5f;
            _worldSize = max - min;

            Refresh();
        }

        public void SetRacer(int racerIndex, CourseSpline course, float distanceAlongCourse)
        {
            if (racerIndex < 0 || racerIndex >= _dots.Length || course == null)
                return;

            var world = course.GetWorldPosition(distanceAlongCourse, 0f);
            _racerWorld[racerIndex] = new Vector2(world.x, world.z);
            _racerVisible[racerIndex] = true;
        }

        /// SetCourse / SetRacer を反映する。毎フレームの最後に呼ぶ。
        public void Apply()
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                bool visible = _racerVisible[i] && _points != null;
                if (!visible)
                {
                    _dots[i].AddToClassList("hidden");
                    continue;
                }

                _dots[i].RemoveFromClassList("hidden");
                var local = WorldToLocal2D(_racerWorld[i]);
                float size = i == _selfIndex ? SelfDotSize : DotSize;
                _dots[i].style.left = local.x - size * 0.5f;
                _dots[i].style.top = local.y - size * 0.5f;
            }
        }

        void Refresh()
        {
            RecalculateTransform();
            _track.MarkDirtyRepaint();
            Apply();
        }

        /// ワールド XZ を要素ローカル座標へ写す倍率と中心を求める。
        /// X と Z で倍率を分けないのは、コースの形を歪ませないため。
        void RecalculateTransform()
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f || _points == null)
                return;

            float usableX = Mathf.Max(1f, rect.width - Padding * 2f);
            float usableY = Mathf.Max(1f, rect.height - Padding * 2f);
            float scaleX = _worldSize.x > 0.001f ? usableX / _worldSize.x : float.MaxValue;
            float scaleY = _worldSize.y > 0.001f ? usableY / _worldSize.y : float.MaxValue;
            _scale = Mathf.Min(scaleX, scaleY);
            if (_scale <= 0f || float.IsInfinity(_scale))
                _scale = 1f;

            _viewCenter = new Vector2(rect.width * 0.5f, rect.height * 0.5f);
        }

        /// ワールド +Z は画面奥なので、ローカル Y は符号を反転させて上向きにする。
        Vector2 WorldToLocal2D(Vector2 world)
        {
            var offset = world - _worldCenter;
            return new Vector2(_viewCenter.x + offset.x * _scale, _viewCenter.y - offset.y * _scale);
        }

        void DrawCourse(MeshGenerationContext ctx)
        {
            if (_points == null || _points.Length < 2)
                return;

            var painter = ctx.painter2D;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;

            // 背景が明るい場面でも線が消えないよう、暗い縁取りの上に明るい本線を重ねる。
            StrokeCourse(painter, 6f, new Color(0f, 0f, 0f, 0.6f));
            StrokeCourse(painter, 3f, new Color(1f, 1f, 1f, 0.75f));

            // ゴール。線だけだとどちらの端が終点か読めないので点を打つ。
            painter.fillColor = new Color(1f, 1f, 1f, 0.95f);
            painter.BeginPath();
            painter.Arc(WorldToLocal2D(_points[^1]), 4f, 0f, 360f);
            painter.Fill();
        }

        void StrokeCourse(Painter2D painter, float width, Color color)
        {
            painter.lineWidth = width;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(WorldToLocal2D(_points[0]));
            for (int i = 1; i < _points.Length; i++)
                painter.LineTo(WorldToLocal2D(_points[i]));
            painter.Stroke();
        }
    }
}
