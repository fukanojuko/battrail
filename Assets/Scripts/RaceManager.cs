using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battrail
{
    /// レース全体の進行フェーズ。Racer / CombatManager はこれを見て動作を止める。
    public enum RacePhase
    {
        Countdown,
        Running,
        Finished,
    }

    /// 試合の進行管理。スタートのカウントダウンと終了判定（先にゴールした Racer が勝ち）を持つ。
    /// Awake でシーン内 Racer を集め、各 Racer.Finished を購読するイベント駆動。
    /// 順位や時間制限などは後付け予定。
    public class RaceManager : MonoBehaviour
    {
        [Header("Countdown")]
        [Tooltip("カウント 1 つあたりの表示時間（秒）")]
        [SerializeField] float countdownInterval = 1f;
        [Tooltip("GO! を表示しておく時間（秒）。この間もレースは進行している")]
        [SerializeField] float goDisplaySeconds = 0.7f;

        static readonly string[] CountLabels = { "3", "2", "1" };
        const string GoLabel = "GO!";

        readonly List<Racer> _racers = new();
        Racer _winner;

        public Racer Winner => _winner;
        public bool IsFinished => Phase == RacePhase.Finished;
        public IReadOnlyList<Racer> Racers => _racers;
        public RacePhase Phase { get; private set; } = RacePhase.Countdown;

        /// カウントダウンの表示文字。null なら非表示。HUD が毎フレーム参照する。
        public string CountdownLabel { get; private set; }

        /// カウントダウンが明けて操作可能になった瞬間に一度だけ発火。BGM 開始などはここに乗せる。
        public event Action RaceStarted;

        /// 決着の瞬間に一度だけ発火。
        public event Action<Racer> RaceFinished;

        private void Awake()
        {
            _racers.AddRange(FindObjectsByType<Racer>());
            foreach (var racer in _racers)
                racer.Finished += OnRacerFinished;
        }

        // Awaitable は MonoBehaviour の破棄／シーン遷移で自動キャンセルされるため、
        // async void で起動しっぱなしにして問題ない（PostRaceController と同じパターン）。
        private async void Start()
        {
            foreach (var label in CountLabels)
            {
                CountdownLabel = label;
                await Awaitable.WaitForSecondsAsync(countdownInterval);
            }

            CountdownLabel = GoLabel;
            SetPhase(RacePhase.Running);
            RaceStarted?.Invoke();

            // GO! は操作開始と同時に出し、少し残してから消す。
            await Awaitable.WaitForSecondsAsync(goDisplaySeconds);
            CountdownLabel = null;
        }

        void OnRacerFinished(Racer racer)
        {
            if (_winner != null)
                return;

            _winner = racer;
            Debug.Log($"[RaceManager] Winner: {racer.name}");

            SetPhase(RacePhase.Finished);
            RaceFinished?.Invoke(racer);
        }

        void SetPhase(RacePhase phase)
        {
            Phase = phase;
            foreach (var racer in _racers)
                if (racer != null)
                    racer.SetPhase(phase);
        }
    }
}
