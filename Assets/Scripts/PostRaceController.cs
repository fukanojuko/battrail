using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Battrail
{
    /// レース終了後、「もう一度」「タイトルへ戻る」の入力を受け付ける。
    /// 決着直後の残り入力での誤反応を避けるため、少し待ってから受け付ける。
    [RequireComponent(typeof(RaceManager))]
    public class PostRaceController : MonoBehaviour
    {
        [SerializeField] string titleScene = "Title";
        [SerializeField] float inputDelay = 1f;

        RaceManager _raceManager;
        float _finishedAt = -1f;

        void Awake()
        {
            _raceManager = GetComponent<RaceManager>();
        }

        void Update()
        {
            if (!_raceManager.IsFinished)
                return;

            if (_finishedAt < 0f)
                _finishedAt = Time.time;

            if (Time.time - _finishedAt < inputDelay)
                return;

            if (RetryPressed())
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            else if (TitlePressed())
                SceneManager.LoadScene(titleScene);
        }

        bool RetryPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.startButton.wasPressedThisFrame)
                    return true;

            return false;
        }

        bool TitlePressed()
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
