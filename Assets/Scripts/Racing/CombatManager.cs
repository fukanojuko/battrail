using System.Collections.Generic;
using UnityEngine;

namespace Battrail.Racing
{
    /// プレイヤー同士／トレイルの当たり判定を (s, t) 空間でまとめて解決する。
    /// 各 Racer の (s, t) 履歴をトレイルとして保持し、他機の近接通過でゲージを回復させる。
    /// トレイルの見た目は各 Racer に付いた VFX Graph（MasterTrail 等）が担当。ここでは判定用の位置履歴のみ扱う。
    [DefaultExecutionOrder(100)]
    public class CombatManager : MonoBehaviour
    {
        [Header("Player collision (s, t 空間)")]
        [Tooltip("見た目の接触距離（前後 1.09 / 横 0.68）の 1.3 倍程度。" +
                 "広げすぎると相手の占有ゾーンが増えてコースが狭く感じる")]
        [SerializeField] float hitRangeS = 1.4f;
        [SerializeField] float hitRangeT = 0.9f;
        [Tooltip("同一ペアを連続ヒットさせない再判定クールダウン")]
        [SerializeField] float hitCooldown = 0.4f;
        [SerializeField] float victimForwardSpeedFactor = 0.5f;
        [SerializeField] float victimLateralImpulse = 9f;
        [Tooltip("被弾側を操作不能にする時間")]
        [SerializeField] float victimStunSeconds = 0.35f;
        [Tooltip("攻撃でない接触時に左右へ押し離す基本の速さ")]
        [SerializeField] float separationSpeed = 6f;
        [Tooltip("相対速度に応じて跳ね返りを強める係数（速い者同士の接触ほど大きく弾く）")]
        [SerializeField] float separationSpeedFactor = 0.5f;
        [Tooltip("攻撃でない接触時に残す前進速度の割合（1 未満で軽い減速を出す）")]
        [SerializeField] float separationForwardFactor = 0.9f;

        [Header("Side boost hit (横ブーストの突進)")]
        [Tooltip("横ブーストで当てられた側が残す前進速度の割合。通常ヒット（0.5）より重い")]
        [SerializeField] float sideBoostForwardSpeedFactor = 0.25f;
        [Tooltip("横ブーストで当てられた側が横に弾かれる強さ。Racer.maxLateralSpeed（9）を超える分は" +
                 "sideBoostDecay で減衰しながら残る")]
        [SerializeField] float sideBoostLateralImpulse = 16f;
        [Tooltip("横ブーストで当てられた側の操作不能時間。通常ヒット（0.35）より長い")]
        [SerializeField] float sideBoostStunSeconds = 0.8f;

        [Header("Trail")]
        [SerializeField] float trailSeconds = 3f;
        [SerializeField] float trailHitRangeS = 1.0f;
        [SerializeField] float trailHitRangeT = 1.2f;
        [Tooltip("他機トレイル上を通過中のゲージ回復速度（毎秒）")]
        [SerializeField] float trailRecoverPerSecond = 60f;

        struct TrailPoint
        {
            public float S;
            public float T;
            public float ExpireTime;
        }

        sealed class RacerTrail
        {
            public Racer Racer;
            public readonly List<TrailPoint> Points = new();
        }

        readonly List<RacerTrail> _trails = new();
        readonly Dictionary<(EntityId, EntityId), float> _pairCooldown = new();
        IHitReaction _hitReaction;
        IHitReaction _sideBoostHitReaction;
        RaceManager _raceManager;

        private void Start()
        {
            _hitReaction = new DefaultHitReaction(victimForwardSpeedFactor, victimLateralImpulse, victimStunSeconds);
            _sideBoostHitReaction = new DefaultHitReaction(
                sideBoostForwardSpeedFactor, sideBoostLateralImpulse, sideBoostStunSeconds);
            _raceManager = FindAnyObjectByType<RaceManager>();

            foreach (var racer in FindObjectsByType<Racer>())
                _trails.Add(new RacerTrail { Racer = racer });
        }

        private void FixedUpdate()
        {
            // カウントダウン中と決着後は判定しない。特にスタート前は 2 機が静止したまま
            // 近接判定に入り続けるため、はじきの横速度が溜まって GO の瞬間に吹き飛ぶ
            // （Racer 側は Running 以外で速度を積分しないので、蓄積だけが残る）。
            if (_raceManager != null && _raceManager.Phase != RacePhase.Running)
                return;

            float now = Time.time;
            float dt = Time.fixedDeltaTime;

            RecordTrails(now, dt);
            ResolveTrailRecovery(dt);
            ResolvePlayerCollisions(now);
        }

        void RecordTrails(float now, float dt)
        {
            foreach (var trail in _trails)
            {
                var racer = trail.Racer;
                if (racer == null)
                    continue;

                trail.Points.Add(new TrailPoint
                {
                    S = racer.DistanceAlongCourse,
                    T = racer.LateralOffset,
                    ExpireTime = now + trailSeconds,
                });

                trail.Points.RemoveAll(p => p.ExpireTime <= now);
            }
        }

        void ResolveTrailRecovery(float dt)
        {
            foreach (var trail in _trails)
            {
                var racer = trail.Racer;
                if (racer == null)
                    continue;

                foreach (var other in _trails)
                {
                    if (other == trail || other.Racer == null)
                        continue;

                    if (OverlapsTrail(racer, other))
                    {
                        racer.RecoverGauge(trailRecoverPerSecond * dt);
                        break;
                    }
                }
            }
        }

        bool OverlapsTrail(Racer racer, RacerTrail otherTrail)
        {
            foreach (var p in otherTrail.Points)
            {
                if (Mathf.Abs(racer.DistanceAlongCourse - p.S) < trailHitRangeS &&
                    Mathf.Abs(racer.LateralOffset - p.T) < trailHitRangeT)
                    return true;
            }
            return false;
        }

        void ResolvePlayerCollisions(float now)
        {
            for (int i = 0; i < _trails.Count; i++)
            {
                var a = _trails[i].Racer;
                if (a == null) continue;

                for (int j = i + 1; j < _trails.Count; j++)
                {
                    var b = _trails[j].Racer;
                    if (b == null) continue;

                    bool overlapping =
                        Mathf.Abs(a.DistanceAlongCourse - b.DistanceAlongCourse) < hitRangeS &&
                        Mathf.Abs(a.LateralOffset - b.LateralOffset) < hitRangeT;

                    var key = PairKey(a, b);
                    if (!overlapping)
                    {
                        _pairCooldown.Remove(key);
                        continue;
                    }

                    if (_pairCooldown.TryGetValue(key, out var until) && now < until)
                        continue;
                    _pairCooldown[key] = now + hitCooldown;

                    Resolve(a, b);
                }
            }
        }

        void Resolve(Racer a, Racer b)
        {
            // 前方/後方は s の位置関係で決める（仕様: ブースト中の衝突は「前方プレイヤーが減速+弾き」）。
            // attacker は「後方にいてブースト中」の機体のみ。前方側のブースト有無は問わない
            // （XOR で判定すると両者ブースト中に何も起きなくなるバグがあったため）。
            var front = a.DistanceAlongCourse >= b.DistanceAlongCourse ? a : b;
            var rear = front == a ? b : a;

            // 横ブーストの突進だけは s の前後を見ない。横から当てに行く技なので、
            // 前後関係で攻守を決めると「当てに行った側が被弾する」ことが起きる。
            bool aSideRams = IsSideRamming(a, b);
            bool bSideRams = IsSideRamming(b, a);

            if (aSideRams != bSideRams)
            {
                var attacker = aSideRams ? a : b;
                var victim = aSideRams ? b : a;
                _sideBoostHitReaction.OnHit(new HitContext(attacker, victim,
                    attacker.ForwardSpeed - victim.ForwardSpeed, headOn: false));
            }
            else if (!aSideRams && rear.IsAttacking)
            {
                var ctx = new HitContext(rear, front,
                    rear.ForwardSpeed - front.ForwardSpeed, headOn: false);
                _hitReaction.OnHit(ctx);
            }
            else
            {
                // 攻撃でない接触（両者が横ブーストでぶつかり合った相打ちも含む）: スタンは無いが、
                // 相対速度が大きいほど強く弾かれる
                // （固定値だけだと「当たった感」が薄いため。ぶつかった者同士は少し前進速度も落ちる）。
                float relativeSpeed = Mathf.Abs(a.ForwardSpeed - b.ForwardSpeed);
                float bounce = separationSpeed + relativeSpeed * separationSpeedFactor;

                float dir = Mathf.Sign(a.LateralOffset - b.LateralOffset);
                if (Mathf.Approximately(dir, 0f))
                    dir = 1f;
                a.ApplyKnockback(separationForwardFactor, dir * bounce);
                b.ApplyKnockback(separationForwardFactor, -dir * bounce);

                // 攻撃でなくても接触した以上は「当たった感」を出す。攻撃ヒットと違い
                // 一方的な被弾ではないので、両者から鳴らす。
                a.PlayHitEffect();
                b.PlayHitEffect();
            }
        }

        /// attacker が victim の居る側へ横ブーストで突っ込んでいるか。
        /// 反対側へ吹かした（避けた先でたまたま触れた）場合は攻撃にしない。
        static bool IsSideRamming(Racer attacker, Racer victim)
        {
            return attacker.IsSideBoosting &&
                   attacker.SideBoostDirection * (victim.LateralOffset - attacker.LateralOffset) > 0f;
        }

        // 順序を揃えてから組にすることで、(a, b) と (b, a) が同じキーになる。
        // EntityId から int への暗黙変換は非推奨なので、EntityId のまま扱う。
        static (EntityId, EntityId) PairKey(Racer a, Racer b)
        {
            var ia = a.GetEntityId();
            var ib = b.GetEntityId();
            return ia.CompareTo(ib) <= 0 ? (ia, ib) : (ib, ia);
        }
    }
}
