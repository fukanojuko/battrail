using System;
using Battrail.Racing;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Battrail.UI
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

        // Awaitable は破棄されても自動では止まらない。ここ以外の経路でシーンを抜けたとき、
        // 遷移先のシーンで入力を拾い続けないよう destroyCancellationToken で打ち切る。
        async void OnRaceFinished(Racer winner)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(inputDelay, destroyCancellationToken);

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

                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        bool RetryPressed()
        {
            return MenuInput.KeyPressed(k => k.spaceKey) || MenuInput.KeyPressed(k => k.enterKey) ||
                   MenuInput.GamepadPressed(g => g.startButton);
        }

        bool TitlePressed()
        {
            return MenuInput.KeyPressed(k => k.escapeKey) || MenuInput.GamepadPressed(g => g.selectButton);
        }
    }
}
