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
        readonly MinimapElement[] _minimap = new MinimapElement[2];
        /// 非表示にした距離バー。UXML に要素が無い間は何もしない（復活時にここを消さずに済むよう呼び続ける）。
        readonly HudProgressBar _progressBar = new();
        VisualElement _resultRoot;
        Label _resultText;
        VisualElement _countdownRoot;
        Label _countdownText;
        bool _bound;

        private void OnEnable()
        {
            _racers = FindObjectsByType<Racer>();
            _raceManager = FindAnyObjectByType<RaceManager>();
            // UIDocument のツリーは作り直されるので、前回作ったミニマップは捨てて貼り直す。
            for (int i = 0; i < _minimap.Length; i++)
                _minimap[i] = null;
            _bound = false;
        }

        private void Update()
        {
            if (!_bound && !TryBind())
                return;

            _progressBar.BeginFrame();
            foreach (var minimap in _minimap)
                minimap?.BeginFrame();

            foreach (var racer in _racers)
            {
                if (racer == null)
                    continue;

                int i = racer.PlayerIndex;
                if (i < 0 || i >= 2)
                    continue;

                _progressBar.Report(i, racer);
                UpdateMinimaps(i, racer);

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

            _progressBar.Apply();
            foreach (var minimap in _minimap)
                minimap?.Apply();

            UpdateCountdown();
            UpdateResult();
        }

        /// 両パネルのミニマップに 2 人ぶんのアイコンを流す。自分だけでなく相手も出すのは、
        /// 勝敗に効くのが絶対進捗ではなく前後差だから（後ろに付けているかを一目で読ませる）。
        void UpdateMinimaps(int racerIndex, Racer racer)
        {
            var course = racer.Course;
            if (course == null || course.Length <= 0f)
                return;

            foreach (var minimap in _minimap)
            {
                if (minimap == null)
                    continue;

                minimap.SetCourse(course);
                minimap.SetRacer(racerIndex, course, racer.DistanceAlongCourse);
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

                // ミニマップは中身を C# で組み立てるので、UXML 側は置き場所だけ用意してある。
                // TryBind は成功するまで毎フレーム呼ばれるので、二重に足さないよう既存を見る。
                var host = root.Q<VisualElement>($"p{i}-minimap");
                if (host != null && _minimap[i] == null)
                {
                    _minimap[i] = new MinimapElement(i);
                    host.Add(_minimap[i]);
                }
            }
            _progressBar.Bind(root);
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
