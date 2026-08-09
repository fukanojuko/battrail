using UnityEngine;
using UnityEngine.UIElements;

namespace Battrail
{
    /// UI Toolkit (UXML/USS) ベースの HUD バインダ。UIDocument のツリーを取得し、
    /// playerIndex 0 = 左パネル / 1 = 右パネル に各 Racer の速度・順位・ゲージを反映する。
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHudUI : MonoBehaviour
    {
        static readonly Color BoostColor = new(1f, 0.85f, 0.2f);
        static readonly Color NormalColor = new(0.25f, 0.7f, 1f);
        static readonly Color StunColor = new(1f, 0.3f, 0.3f);

        Racer[] _racers;
        RaceManager _raceManager;
        readonly Label[] _info = new Label[2];
        readonly VisualElement[] _fill = new VisualElement[2];
        readonly Label[] _distance = new Label[2];
        /// [パネル, Racer] の進行アイコン。両パネルに 2 人ぶん置くので 2 次元で持つ。
        readonly VisualElement[,] _marker = new VisualElement[2, 2];
        /// Racer ごとのコース進捗 0..1。コース未解決なら負値（アイコンを隠す）。
        readonly float[] _progress = new float[2];
        VisualElement _resultRoot;
        Label _resultText;
        VisualElement _countdownRoot;
        Label _countdownText;
        bool _bound;

        private void OnEnable()
        {
            _racers = FindObjectsByType<Racer>();
            _raceManager = FindAnyObjectByType<RaceManager>();
            _bound = false;
        }

        private void Update()
        {
            if (!_bound && !TryBind())
                return;

            for (int i = 0; i < 2; i++)
                _progress[i] = -1f;

            foreach (var racer in _racers)
            {
                if (racer == null)
                    continue;

                int i = racer.PlayerIndex;
                if (i < 0 || i >= 2)
                    continue;

                UpdateProgress(i, racer);

                if (_info[i] != null)
                    _info[i].text = $"{PlayerLabel(i)}   {Rank(racer)}位   {racer.ForwardSpeed:F0}";

                var fill = _fill[i];
                if (fill != null)
                {
                    fill.style.width = Length.Percent(Mathf.Clamp01(racer.GaugeRatio) * 100f);
                    fill.style.backgroundColor =
                        racer.IsStunned ? StunColor : racer.IsBoosting ? BoostColor : NormalColor;
                }
            }

            ApplyProgressMarkers();
            UpdateCountdown();
            UpdateResult();
        }

        /// 進捗率と残距離を求める。コース長は Racer から引く（将来コースが差し替わっても追従できるように）。
        void UpdateProgress(int index, Racer racer)
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
        void ApplyProgressMarkers()
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

        void UpdateCountdown()
        {
            if (_countdownRoot == null || _raceManager == null)
                return;

            var label = _raceManager.CountdownLabel;
            if (label == null)
            {
                _countdownRoot.AddToClassList("hidden");
                return;
            }

            _countdownRoot.RemoveFromClassList("hidden");
            if (_countdownText != null)
                _countdownText.text = label;
        }

        void UpdateResult()
        {
            if (_resultRoot == null || _raceManager == null)
                return;

            if (_raceManager.IsFinished)
            {
                _resultRoot.RemoveFromClassList("hidden");
                if (_resultText != null)
                    _resultText.text = $"{PlayerLabel(_raceManager.Winner.PlayerIndex)}   WIN";
            }
            else
            {
                _resultRoot.AddToClassList("hidden");
            }
        }

        bool TryBind()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null)
                return false;

            for (int i = 0; i < 2; i++)
            {
                _info[i] = root.Q<Label>($"p{i}-info");
                _fill[i] = root.Q<VisualElement>($"p{i}-fill");
                _distance[i] = root.Q<Label>($"p{i}-distance");
                for (int j = 0; j < 2; j++)
                    _marker[i, j] = root.Q<VisualElement>($"p{i}-mark{j}");
            }
            _resultRoot = root.Q<VisualElement>("result");
            _resultText = root.Q<Label>("result-text");
            _countdownRoot = root.Q<VisualElement>("countdown");
            _countdownText = root.Q<Label>("countdown-text");
            _bound = _info[0] != null || _info[1] != null;
            return _bound;
        }

        /// 表示名。1 人プレイでは NPC が操作する側を "P2" ではなく "NPC" と出す。
        static string PlayerLabel(int playerIndex)
        {
            return GameMode.VsNpc && playerIndex == GameMode.NpcPlayerIndex
                ? "NPC"
                : $"P{playerIndex + 1}";
        }

        int Rank(Racer racer)
        {
            int rank = 1;
            foreach (var other in _racers)
            {
                if (other != null && other != racer &&
                    other.DistanceAlongCourse > racer.DistanceAlongCourse)
                    rank++;
            }
            return rank;
        }
    }
}
