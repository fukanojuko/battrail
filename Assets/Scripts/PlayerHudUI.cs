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
        readonly Label[] _info = new Label[2];
        readonly VisualElement[] _fill = new VisualElement[2];
        bool _bound;

        void OnEnable()
        {
            _racers = FindObjectsByType<Racer>(FindObjectsSortMode.None);
            _bound = false;
        }

        void Update()
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
