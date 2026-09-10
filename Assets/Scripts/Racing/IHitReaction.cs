namespace Battrail.Racing
{
    /// 攻撃ヒット時の被弾側リアクション。演出強化や挙動変更はこの実装を差し替える。
    public interface IHitReaction
    {
        void OnHit(in HitContext ctx);
    }
}
