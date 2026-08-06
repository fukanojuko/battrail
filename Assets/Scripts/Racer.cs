using System;
using UnityEngine;
using UnityEngine.VFX;

namespace Battrail
{
    /// プレイヤー機の移動本体。スプライン相対の (s, t) を内部状態として持ち、
    /// 入力に応じて s を加減速、t を左右移動させる。ワールド変換はコース（CourseSpline）から計算する。
    /// 物理体は kinematic Rigidbody。プレイヤー同士・トレイルとの当たり判定は (s, t) 空間で
    /// CombatManager がまとめて行い、結果は ApplyKnockback / RecoverGauge で受け取る。
    ///
    /// 入力読み取りは RacerInput に分離（オンライン対応時はそちらだけ差し替える想定）。
    [RequireComponent(typeof(Rigidbody))]
    public class Racer : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] int playerIndex = 0;

        [Header("Forward (s)")]
        [SerializeField] float maxSpeed = 18f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float brakeDeceleration = 26f;
        [SerializeField] float coastDeceleration = 7f;

        [Header("Corner")]
        [Tooltip("最も急なカーブで最高速が何割になるか（1 で減速なし）")]
        [SerializeField] float cornerSpeedFactorMin = 0.85f;
        [Tooltip("上の係数に到達する曲率 [rad/unit]。0.05 は回転半径 20 相当")]
        [SerializeField] float cornerFullEffectCurvature = 0.05f;

        [Header("Boost")]
        [SerializeField] float boostSpeed = 28f;
        [SerializeField] float boostAcceleration = 24f;
        [Tooltip("ブースト終了後、boostSpeed から maxSpeed まで戻る減速")]
        [SerializeField] float overspeedDecay = 12f;
        [SerializeField] float maxGauge = 100f;
        [SerializeField] float gaugeDrainPerSecond = 35f;
        [SerializeField] float gaugeRegenPerSecond = 12f;
        [Tooltip("ゲージが空になった後、再びブーストできるようになるゲージ量。押しっぱなしでの再点火を防ぐ")]
        [SerializeField] float boostRestartGauge = 25f;

        [Header("Lateral (t)")]
        [Tooltip("左右入力による横加速度（速度ではなく加速度で反応させ、慣性で切り返しにブレを出す）")]
        [SerializeField] float strafeAcceleration = 26f;
        [Tooltip("横速度の摩擦減衰。無入力時にブレを収束させる")]
        [SerializeField] float lateralDamping = 10f;
        [Tooltip("コース中央 (t=0) へ戻ろうとする引力の強さ（オフセットに比例した加速度）")]
        [SerializeField] float centerPullStrength = 1.2f;
        [SerializeField] float maxLateralSpeed = 9f;
        [Tooltip("スタート時の横オフセット。2 機が重ならないよう P1/P2 で符号を変える")]
        [SerializeField] float startLateralOffset = 0f;

        [Header("Start Dash")]
        [Tooltip("スタン解除直後、自動でこの速度まで加速する時間（秒）。被弾で離された側の追い上げを助ける")]
        [SerializeField] float startDashDuration = 1.5f;
        [SerializeField] float startDashSpeed = 28f;
        [SerializeField] float startDashAcceleration = 30f;

        [Header("Wall")]
        [Tooltip("壁接触時に発生する横方向の跳ね返り初速")]
        [SerializeField] float wallBounce = 6f;
        [Tooltip("壁接触時に残す前進速度の割合（0.8 = 20% 減速）")]
        [SerializeField] float wallSpeedRetain = 0.8f;

        [Header("Course")]
        [SerializeField] CourseSpline course;

        [Header("Hit effect")]
        [Tooltip("被弾時に再生する VFX。Initial Event Name を空にしてあり、自動再生はしない")]
        [SerializeField] VisualEffect hitEffect;

        public int PlayerIndex => playerIndex;
        public float DistanceAlongCourse { get; private set; }
        public float LateralOffset { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool HasFinished { get; private set; }
        public bool IsBoosting { get; private set; }
        /// ブースト中は攻撃判定が有効。
        public bool IsAttacking => IsBoosting;
        public bool IsStunned => _stunTimer > 0f;
        public float Gauge { get; private set; }
        public float MaxGauge => maxGauge;
        public float GaugeRatio => maxGauge > 0f ? Gauge / maxGauge : 0f;
        public CourseSpline Course => course;

        /// ゴール到達の瞬間に一度だけ発火（RaceManager が毎フレーム HasFinished をポーリングしなくて済むように）。
        public event Action<Racer> Finished;

        /// シーン内から playerIndex 一致の Racer を引く。カメラ・演出側が自分の担当機を見つけるための入口。
        public static Racer Find(int playerIndex)
        {
            foreach (var racer in FindObjectsByType<Racer>())
                if (racer.playerIndex == playerIndex)
                    return racer;
            return null;
        }

        Rigidbody _rigidbody;
        RacerInput _input;
        float _lateralVelocity;
        float _stunTimer;
        float _startDashTimer;
        bool _boostDepleted;
        bool _raceOver;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            _input = new RacerInput(playerIndex);

            if (course == null)
                course = FindAnyObjectByType<CourseSpline>();

            Gauge = maxGauge;
        }

        private void Start()
        {
            // Spread starting positions so players don't overlap at the start line.
            LateralOffset = startLateralOffset;
            SnapToCourse();
        }

        private void FixedUpdate()
        {
            if (course == null || HasFinished || _raceOver)
                return;

            var move = _input.ReadMove();
            bool boostHeld = _input.ReadBoost();
            var dt = Time.fixedDeltaTime;

            // スタン中は操作不能（慣性・弾き・スプライン追従は継続）。解除の瞬間にスタートダッシュを付与する。
            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                move = Vector2.zero;
                boostHeld = false;
                if (_stunTimer <= 0f)
                    _startDashTimer = startDashDuration;
            }

            // 空になったら boostRestartGauge まで戻るまで再点火させない。
            // 「Gauge > 0」だけを条件にすると、空のまま押しっぱなしのとき
            // 「消費できず回復が入るフレーム → 次フレームで再点火」を毎フレーム繰り返し、
            // 実効 50% のデューティでブーストが永続してしまう（超過した消費量は 0 でクランプされて消える）。
            if (_boostDepleted && Gauge >= boostRestartGauge)
                _boostDepleted = false;

            IsBoosting = boostHeld && !_boostDepleted && Gauge > 0f;
            Gauge = Mathf.Clamp(
                Gauge + (IsBoosting ? -gaugeDrainPerSecond : gaugeRegenPerSecond) * dt,
                0f, maxGauge);

            if (IsBoosting && Gauge <= 0f)
                _boostDepleted = true;

            ForwardSpeed = StepForward(ForwardSpeed, move.y, IsBoosting, CornerSpeedFactor(), dt);

            // スタートダッシュ中は攻撃判定を持たせず（IsBoosting はそのまま）、速度だけ強制的に持ち上げる。
            if (_startDashTimer > 0f)
            {
                _startDashTimer -= dt;
                ForwardSpeed = Mathf.MoveTowards(ForwardSpeed, startDashSpeed, startDashAcceleration * dt);
            }

            DistanceAlongCourse += ForwardSpeed * dt;

            StepLateral(move.x, dt);

            if (DistanceAlongCourse >= course.Length)
            {
                DistanceAlongCourse = course.Length;
                HasFinished = true;
                ForwardSpeed = 0f;
                IsBoosting = false;
                Finished?.Invoke(this);
            }

            SnapToCourse();
        }

        void StepLateral(float input, float dt)
        {
            // 入力による加速 + コース中央への弱い引力。速度に摩擦をかけて自然に収束させる
            // （ばね＋減衰のような挙動。中央保持ではなく慣性でブレを出す）。
            float accel = input * strafeAcceleration - LateralOffset * centerPullStrength;
            _lateralVelocity += accel * dt;
            _lateralVelocity = Mathf.MoveTowards(_lateralVelocity, 0f, lateralDamping * dt);
            _lateralVelocity = Mathf.Clamp(_lateralVelocity, -maxLateralSpeed, maxLateralSpeed);

            LateralOffset += _lateralVelocity * dt;

            float halfWidth = course.HalfWidth;
            if (LateralOffset > halfWidth)
            {
                LateralOffset = halfWidth;
                if (_lateralVelocity > 0f) HitWall(-1f);
            }
            else if (LateralOffset < -halfWidth)
            {
                LateralOffset = -halfWidth;
                if (_lateralVelocity < 0f) HitWall(1f);
            }
        }

        void HitWall(float inwardSign)
        {
            _lateralVelocity = inwardSign * wallBounce;
            ForwardSpeed *= wallSpeedRetain;
        }

        /// CombatManager から攻撃ヒット結果を適用する。前進減速＋横方向の弾き。
        public void ApplyKnockback(float forwardSpeedFactor, float lateralImpulse)
        {
            ForwardSpeed *= forwardSpeedFactor;
            _lateralVelocity += lateralImpulse;
        }

        /// トレイル通過などでゲージを回復する。
        public void RecoverGauge(float amount)
        {
            Gauge = Mathf.Clamp(Gauge + amount, 0f, maxGauge);
        }

        /// 接触演出を再生する。CombatManager から呼ばれる。
        public void PlayHitEffect()
        {
            if (hitEffect != null)
                hitEffect.Play();
        }

        /// 一定時間操作不能にする（被弾時など）。複数回呼ばれたら長い方を採用。
        public void Stun(float seconds)
        {
            _stunTimer = Mathf.Max(_stunTimer, seconds);
        }

        /// 決着がついたら RaceManager から呼ばれ、以後の入力・移動を止める。
        public void EndRace()
        {
            _raceOver = true;
            // 以後 FixedUpdate が止まるので、押しっぱなしのブースト状態が残らないようここで落とす
            // （攻撃判定・ブースト演出が決着後も出たままになる）。
            IsBoosting = false;
        }

        void SnapToCourse()
        {
            if (course == null) return;
            var position = course.GetWorldPosition(DistanceAlongCourse, LateralOffset);
            var rotation = course.GetRotation(DistanceAlongCourse);
            _rigidbody.MovePosition(position);
            _rigidbody.MoveRotation(rotation);
        }

        /// カーブ中の最高速の倍率。急なほど 1 から cornerSpeedFactorMin へ近づく。
        /// 加速度そのものは変えず上限だけ下げるので、直線へ抜けると自然に速度が戻る。
        float CornerSpeedFactor()
        {
            if (cornerSpeedFactorMin >= 1f)
                return 1f;

            float curvature = course.GetCurvature(DistanceAlongCourse);
            float t = Mathf.Clamp01(curvature / Mathf.Max(0.0001f, cornerFullEffectCurvature));
            return Mathf.Lerp(1f, cornerSpeedFactorMin, t);
        }

        float StepForward(float current, float input, bool boosting, float cornerFactor, float dt)
        {
            float speedCap = maxSpeed * cornerFactor;

            if (boosting)
            {
                if (input > 0f)
                    current += boostAcceleration * input * dt;
                else if (input < 0f)
                    current += brakeDeceleration * input * dt;
                else
                    current = Mathf.MoveTowards(current, 0f, coastDeceleration * dt);
                return Mathf.Clamp(current, 0f, boostSpeed * cornerFactor);
            }

            // 非ブーストで上限超（ブースト余韻／カーブ進入で上限が下がった直後）は、
            // 加速入力で増やさず上限へ減衰させる。
            if (current > speedCap)
            {
                float decayed = Mathf.MoveTowards(current, speedCap, overspeedDecay * dt);
                if (input < 0f)
                    decayed += brakeDeceleration * input * dt;
                return Mathf.Clamp(decayed, 0f, current);
            }

            if (input > 0f)
                current += acceleration * input * dt;
            else if (input < 0f)
                current += brakeDeceleration * input * dt;
            else
                current = Mathf.MoveTowards(current, 0f, coastDeceleration * dt);
            return Mathf.Clamp(current, 0f, speedCap);
        }
    }
}
