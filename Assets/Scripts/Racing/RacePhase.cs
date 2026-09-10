namespace Battrail.Racing
{
    /// レース全体の進行フェーズ。Racer / CombatManager はこれを見て動作を止める。
    public enum RacePhase
    {
        Countdown,
        Running,
        Finished,
    }
}
