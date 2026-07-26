using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battrail
{
    /// 試合の終了判定（先にゴールした Racer が勝ち）。
    /// Awake でシーン内 Racer を集め、各 Racer.Finished を購読するイベント駆動。
    /// 順位や時間制限などは後付け予定。
    public class RaceManager : MonoBehaviour
    {
        readonly List<Racer> _racers = new();
        Racer _winner;

        public Racer Winner => _winner;
        public bool IsFinished => _winner != null;
        public IReadOnlyList<Racer> Racers => _racers;

        /// 決着の瞬間に一度だけ発火。
        public event Action<Racer> RaceFinished;

        private void Awake()
        {
            _racers.AddRange(FindObjectsByType<Racer>());
            foreach (var racer in _racers)
                racer.Finished += OnRacerFinished;
        }

        void OnRacerFinished(Racer racer)
        {
            if (_winner != null)
                return;

            _winner = racer;
            Debug.Log($"[RaceManager] Winner: {racer.name}");

            foreach (var r in _racers)
                if (r != null)
                    r.EndRace();

            RaceFinished?.Invoke(racer);
        }
    }
}
