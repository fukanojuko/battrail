using System.Collections.Generic;
using UnityEngine;

namespace Battrail
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

    /// 攻撃ヒット時の被弾側リアクション。演出強化や挙動変更はこの実装を差し替える。
    public interface IHitReaction
    {
        void OnHit(in HitContext ctx);
    }

    /// 既定リアクション: 被弾側を減速させ、攻撃側から離れる方向（近い壁側）へ弾く。
    public sealed class DefaultHitReaction : IHitReaction
    {
        readonly float _forwardSpeedFactor;
        readonly float _lateralImpulse;

        public DefaultHitReaction(float forwardSpeedFactor, float lateralImpulse)
        {
            _forwardSpeedFactor = forwardSpeedFactor;
            _lateralImpulse = lateralImpulse;
        }

        public void OnHit(in HitContext ctx)
        {
            float dir = Mathf.Sign(ctx.Victim.LateralOffset - ctx.Attacker.LateralOffset);
            if (Mathf.Approximately(dir, 0f))
                dir = 1f;
            ctx.Victim.ApplyKnockback(_forwardSpeedFactor, dir * _lateralImpulse);
        }
    }

    /// プレイヤー同士／トレイルの当たり判定を (s, t) 空間でまとめて解決する。
    /// 各 Racer の (s, t) 履歴をトレイルとして保持し、他機の近接通過でゲージを回復させる。
    /// トレイルの見た目は仮で LineRenderer を出す（後で VFX Graph に差し替える）。
    [DefaultExecutionOrder(100)]
    public class CombatManager : MonoBehaviour
    {
        [Header("Player collision (s, t 空間)")]
        [SerializeField] float hitRangeS = 1.2f;
        [SerializeField] float hitRangeT = 1.2f;
        [Tooltip("同一ペアを連続ヒットさせない再判定クールダウン")]
        [SerializeField] float hitCooldown = 0.4f;
        [SerializeField] float victimForwardSpeedFactor = 0.5f;
        [SerializeField] float victimLateralImpulse = 9f;
        [Tooltip("攻撃でない接触時に左右へ押し離す速さ")]
        [SerializeField] float separationSpeed = 4f;

        [Header("Trail")]
        [SerializeField] float trailSeconds = 3f;
        [SerializeField] float trailRecordInterval = 0.05f;
        [SerializeField] float trailHitRangeS = 1.0f;
        [SerializeField] float trailHitRangeT = 1.2f;
        [Tooltip("他機トレイル上を通過中のゲージ回復速度（毎秒）")]
        [SerializeField] float trailRecoverPerSecond = 60f;

        [Header("Trail visual (仮 / 後で VFX に差替)")]
        [SerializeField] float trailWidth = 0.6f;

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
            public float RecordTimer;
            public LineRenderer Line;
        }

        readonly List<RacerTrail> _trails = new();
        readonly Dictionary<long, float> _pairCooldown = new();
        IHitReaction _hitReaction;

        void Start()
        {
            _hitReaction = new DefaultHitReaction(victimForwardSpeedFactor, victimLateralImpulse);

            foreach (var racer in FindObjectsByType<Racer>(FindObjectsSortMode.None))
                _trails.Add(new RacerTrail { Racer = racer, Line = CreateTrailLine(racer) });
        }

        void FixedUpdate()
        {
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

                trail.RecordTimer -= dt;
                if (trail.RecordTimer <= 0f)
                {
                    trail.RecordTimer = trailRecordInterval;
                    trail.Points.Add(new TrailPoint
                    {
                        S = racer.DistanceAlongCourse,
                        T = racer.LateralOffset,
                        ExpireTime = now + trailSeconds,
                    });
                }

                trail.Points.RemoveAll(p => p.ExpireTime <= now);
                UpdateTrailLine(trail);
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

                    long key = PairKey(a, b);
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
            // ブースト中の機だけが攻撃判定を持つ。一方だけブースト中ならそれが attacker。
            if (a.IsAttacking ^ b.IsAttacking)
            {
                var attacker = a.IsAttacking ? a : b;
                var victim = a.IsAttacking ? b : a;
                var ctx = new HitContext(attacker, victim,
                    attacker.ForwardSpeed - victim.ForwardSpeed, headOn: false);
                _hitReaction.OnHit(ctx);
            }
            else
            {
                // 攻撃でない接触: 左右に軽く押し離すだけ（仕様: 通常衝突は軽い物理反応のみ）。
                float dir = Mathf.Sign(a.LateralOffset - b.LateralOffset);
                if (Mathf.Approximately(dir, 0f))
                    dir = 1f;
                a.ApplyKnockback(1f, dir * separationSpeed);
                b.ApplyKnockback(1f, -dir * separationSpeed);
            }
        }

        static long PairKey(Racer a, Racer b)
        {
            int ia = a.GetInstanceID();
            int ib = b.GetInstanceID();
            if (ia > ib) (ia, ib) = (ib, ia);
            return ((long)ia << 32) ^ (uint)ib;
        }

        LineRenderer CreateTrailLine(Racer racer)
        {
            var go = new GameObject($"Trail_{racer.name}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.widthMultiplier = trailWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.useWorldSpace = true;
            line.positionCount = 0;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            var color = ReadRacerColor(racer);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
            line.material = material;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            return line;
        }

        void UpdateTrailLine(RacerTrail trail)
        {
            if (trail.Line == null || trail.Racer == null)
                return;

            var course = trail.Racer.Course;
            if (course == null)
                return;

            trail.Line.positionCount = trail.Points.Count;
            for (int i = 0; i < trail.Points.Count; i++)
            {
                var p = trail.Points[i];
                trail.Line.SetPosition(i, course.GetWorldPosition(p.S, p.T));
            }
        }

        static Color ReadRacerColor(Racer racer)
        {
            var renderer = racer.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;
                if (mat.HasProperty("_BaseColor"))
                    return mat.GetColor("_BaseColor");
                return mat.color;
            }
            return Color.cyan;
        }
    }
}
