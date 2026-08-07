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

            foreach (var racer in _racers)
            {
                if (racer == null)
                    continue;

                int i = racer.PlayerIndex;
                if (i < 0 || i >= 2)
                    continue;

                if (_info[i] != null)
                    _info[i].text = $"P{i + 1}   {Rank(racer)}位   {racer.ForwardSpeed:F0}";

                var fill = _fill[i];
                if (fill != null)
                {
                    fill.style.width = Length.Percent(Mathf.Clamp01(racer.GaugeRatio) * 100f);
                    fill.style.backgroundColor =
                        racer.IsStunned ? StunColor : racer.IsBoosting ? BoostColor : NormalColor;
                }
            }

            UpdateCountdown();
            UpdateResult();
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
                    _resultText.text = $"P{_raceManager.Winner.PlayerIndex + 1}   WIN";
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
            }
            _resultRoot = root.Q<VisualElement>("result");
            _resultText = root.Q<Label>("result-text");
            _countdownRoot = root.Q<VisualElement>("countdown");
            _countdownText = root.Q<Label>("countdown-text");
            _bound = _info[0] != null || _info[1] != null;
            return _bound;
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
