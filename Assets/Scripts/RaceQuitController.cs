using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Battrail
{
    /// レース中にタイトルへ戻る導線（ESC / ゲームパッド Select）。
    /// 決着後は同じ操作を PostRaceController が引き継ぐため、レース進行中のみ有効。
    [RequireComponent(typeof(RaceManager))]
    public class RaceQuitController : MonoBehaviour
    {
        [SerializeField] string titleScene = "Title";

        RaceManager _raceManager;

        private void Awake()
        {
            _raceManager = GetComponent<RaceManager>();
        }

        private void Update()
        {
            if (_raceManager.IsFinished)
                return;

            if (QuitPressed())
                SceneManager.LoadScene(titleScene);
        }

        bool QuitPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.selectButton.wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
