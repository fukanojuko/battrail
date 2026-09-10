using UnityEngine;

namespace Battrail.Racing
{
    /// 既定リアクション: 被弾側を減速＋攻撃側から離れる方向（横）へ弾き、短時間スタンさせる。
    public sealed class DefaultHitReaction : IHitReaction
    {
        readonly float _forwardSpeedFactor;
        readonly float _lateralImpulse;
        readonly float _stunSeconds;

        public DefaultHitReaction(float forwardSpeedFactor, float lateralImpulse, float stunSeconds)
        {
            _forwardSpeedFactor = forwardSpeedFactor;
            _lateralImpulse = lateralImpulse;
            _stunSeconds = stunSeconds;
        }

        public void OnHit(in HitContext ctx)
        {
            float dir = Mathf.Sign(ctx.Victim.LateralOffset - ctx.Attacker.LateralOffset);
            if (Mathf.Approximately(dir, 0f))
                dir = 1f;
            ctx.Victim.ApplyKnockback(_forwardSpeedFactor, dir * _lateralImpulse);
            if (_stunSeconds > 0f)
                ctx.Victim.Stun(_stunSeconds);
            ctx.Victim.PlayHitEffect();
        }
    }
}
