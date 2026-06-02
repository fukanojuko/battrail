using UnityEngine;

namespace Battrail
{
    /// 検証用の簡易 HUD。各 Racer の速度・ゲージ・ブースト状態を画面左上に表示する。
    /// 本実装の viewport 別 HUD ができたら置き換える。
    public class DebugHud : MonoBehaviour
    {
        Racer[] _racers;
        GUIStyle _style;

        void Start()
        {
            _racers = FindObjectsByType<Racer>(FindObjectsSortMode.None);
        }

        void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };

            float y = 10f;
            foreach (var racer in _racers)
            {
                if (racer == null)
                    continue;

                string boost = racer.IsBoosting ? "  ★BOOST" : "";
                string text =
                    $"P{racer.PlayerIndex + 1}  spd {racer.ForwardSpeed,5:F1}   " +
                    $"gauge {racer.Gauge,5:F0}/{racer.MaxGauge:F0}{boost}";
                _style.normal.textColor = racer.IsBoosting ? Color.yellow : Color.white;
                GUI.Label(new Rect(12f, y, 700f, 30f), text, _style);
                y += 30f;
            }
        }
    }
}
