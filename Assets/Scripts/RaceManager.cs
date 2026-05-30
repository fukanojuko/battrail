using System.Collections.Generic;
using UnityEngine;

namespace Battrail
{
    /// 試合の終了判定（先にゴールした Racer が勝ち）。
    /// Awake でシーン内 Racer を集める。順位や時間制限などは後付け予定。
    public class RaceManager : MonoBehaviour
    {
        readonly List<Racer> _racers = new();
        Racer _winner;

        public Racer Winner => _winner;
        public bool IsFinished => _winner != null;
        public IReadOnlyList<Racer> Racers => _racers;

        void Awake()
        {
            _racers.AddRange(FindObjectsByType<Racer>(FindObjectsSortMode.None));
        }

        void Update()
        {
            if (_winner != null) return;
            for (int i = 0; i < _racers.Count; i++)
            {
                var racer = _racers[i];
                if (racer != null && racer.HasFinished)
                {
                    _winner = racer;
                    Debug.Log($"[RaceManager] Winner: {racer.name}");
                    return;
                }
            }
        }
    }
}
