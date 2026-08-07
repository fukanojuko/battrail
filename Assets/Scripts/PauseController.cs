using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Battrail
{
    /// レース中の一時停止。ESC / ゲームパッド Start でポーズをトグルし、
    /// ポーズ中は Q / ゲームパッド Select でタイトルへ戻る。
    /// 決着後は PostRaceController が同じ画面遷移を担当するため、未決着時のみ有効。
    [RequireComponent(typeof(RaceManager))]
    public class PauseController : MonoBehaviour
    {
        [SerializeField] string titleScene = "Title";

        RaceManager _raceManager;
        VisualElement _pauseRoot;
        bool _isPaused;

        private void Awake()
        {
            _raceManager = GetComponent<RaceManager>();
        }

        private void Start()
        {
            var doc = FindAnyObjectByType<UIDocument>();
            _pauseRoot = doc != null ? doc.rootVisualElement.Q<VisualElement>("pause") : null;
        }

        private void OnDestroy()
        {
            // シーン遷移などでこのオブジェクトが消えても timeScale / 音の停止を残さない。
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        private void Update()
        {
            if (_raceManager.IsFinished)
            {
                SetPaused(false);
                return;
            }

            if (TogglePressed())
                SetPaused(!_isPaused);

            if (_isPaused && QuitPressed())
            {
                SetPaused(false);
                SceneManager.LoadScene(titleScene);
            }
        }

        void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            // timeScale = 0 でも音は止まらないので、BGM はここで明示的に止める。
            AudioListener.pause = paused;

            if (_pauseRoot == null)
                return;

            if (paused)
                _pauseRoot.RemoveFromClassList("hidden");
            else
                _pauseRoot.AddToClassList("hidden");
        }

        bool TogglePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.startButton.wasPressedThisFrame)
                    return true;

            return false;
        }

        bool QuitPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.selectButton.wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
