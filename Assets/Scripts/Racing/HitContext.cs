namespace Battrail.Racing
{
    /// 攻撃ヒットの状況。被弾側の挙動を差し替えやすくするための値。
    public readonly struct HitContext
    {
        public readonly Racer Attacker;
        public readonly Racer Victim;
        /// s 方向の相対速度（attacker - victim）。
        public readonly float RelativeSpeed;
        /// 向かい合いの衝突か。現状の一方通行コースでは常に false（将来用）。
        public readonly bool HeadOn;

        public HitContext(Racer attacker, Racer victim, float relativeSpeed, bool headOn)
        {
            Attacker = attacker;
            Victim = victim;
            RelativeSpeed = relativeSpeed;
            HeadOn = headOn;
        }
    }
}
