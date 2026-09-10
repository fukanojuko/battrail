using Battrail.Racing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Battrail.UI
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
            return MenuInput.KeyPressed(k => k.escapeKey) || MenuInput.GamepadPressed(g => g.startButton);
        }

        /// ポーズ解除に ESC を使うため、タイトルへ戻す側は Q を割り当てている。
        bool QuitPressed()
        {
            return MenuInput.KeyPressed(k => k.qKey) || MenuInput.GamepadPressed(g => g.selectButton);
        }
    }
}
