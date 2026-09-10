using Battrail.Racing;
using UnityEngine;
using UnityEngine.UIElements;

namespace Battrail.UI
{
    /// 旧「ゴールまでの距離バー」。ミニマップに置き換えたため現在 PlayerHud.uxml から外してあり、
    /// Bind しても要素が見つからず何もしない状態になっている。
    /// 復活させる手順は Assets/UI/PlayerHudProgressBar.uxml のヘッダコメントを参照。
    public class HudProgressBar
    {
        readonly Label[] _distance = new Label[2];
        /// [パネル, Racer] の進行アイコン。両パネルに 2 人ぶん置くので 2 次元で持つ。
        readonly VisualElement[,] _marker = new VisualElement[2, 2];
        /// Racer ごとのコース進捗 0..1。コース未解決なら負値（アイコンを隠す）。
        readonly float[] _progress = new float[2];

        public void Bind(VisualElement root)
        {
            for (int i = 0; i < 2; i++)
            {
                _distance[i] = root.Q<Label>($"p{i}-distance");
                for (int j = 0; j < 2; j++)
                    _marker[i, j] = root.Q<VisualElement>($"p{i}-mark{j}");
            }
        }

        public void BeginFrame()
        {
            for (int i = 0; i < _progress.Length; i++)
                _progress[i] = -1f;
        }

        /// 進捗率と残距離を求める。コース長は Racer から引く（将来コースが差し替わっても追従できるように）。
        public void Report(int index, Racer racer)
        {
            var course = racer.Course;
            bool hasCourse = course != null && course.Length > 0f;
            if (hasCourse)
                _progress[index] = Mathf.Clamp01(racer.DistanceAlongCourse / course.Length);

            if (_distance[index] == null)
                return;

            // コース未解決でも空欄にしない。空欄だと「表示位置が悪い」のか「値が来ていない」のか切り分けられないため。
            _distance[index].text = hasCourse
                ? $"{Mathf.Max(0f, course.Length - racer.DistanceAlongCourse):F0}"
                : "--";
        }

        /// 両パネルに 2 人ぶんのアイコンを置く。自分だけでなく相手も出すのは、
        /// 勝敗に効くのが絶対進捗ではなく前後差だから（後ろに付けているかを一目で読ませる）。
        public void Apply()
        {
            for (int panel = 0; panel < 2; panel++)
            {
                for (int racer = 0; racer < 2; racer++)
                {
                    var marker = _marker[panel, racer];
                    if (marker == null)
                        continue;

                    if (_progress[racer] < 0f)
                    {
                        marker.AddToClassList("hidden");
                        continue;
                    }

                    marker.RemoveFromClassList("hidden");
                    marker.style.left = Length.Percent(_progress[racer] * 100f);
                }
            }
        }
    }
}
