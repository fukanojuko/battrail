using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Battrail
{
    /// レース終了後、「もう一度」「タイトルへ戻る」の入力を受け付ける。
    /// RaceManager.RaceFinished をトリガーに一度だけ起動する Awaitable フロー（Update() ポーリングはしない）。
    /// 決着直後の残り入力での誤反応を避けるため、少し待ってから受け付ける。
    [RequireComponent(typeof(RaceManager))]
    public class PostRaceController : MonoBehaviour
    {
        [SerializeField] string titleScene = "Title";
        [SerializeField] float inputDelay = 1f;

        RaceManager _raceManager;

        private void Awake()
        {
            _raceManager = GetComponent<RaceManager>();
            _raceManager.RaceFinished += OnRaceFinished;
        }

        // Awaitable は MonoBehaviour の破棄／シーン遷移で自動キャンセルされるため、
        // async void で起動しっぱなしにして問題ない（Unity 推奨パターン）。
        async void OnRaceFinished(Racer winner)
        {
            await Awaitable.WaitForSecondsAsync(inputDelay);

            while (true)
            {
                if (RetryPressed())
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    return;
                }

                if (TitlePressed())
                {
                    SceneManager.LoadScene(titleScene);
                    return;
                }

                await Awaitable.NextFrameAsync();
            }
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
