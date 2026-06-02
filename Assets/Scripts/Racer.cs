using UnityEngine;
using UnityEngine.InputSystem;

namespace Battrail
{
    /// プレイヤー機の移動本体。スプライン相対の (s, t) を内部状態として持ち、
    /// 入力に応じて s を加減速、t を左右移動させる。ワールド変換はコース（CourseSpline）から計算する。
    /// 物理体は kinematic Rigidbody。プレイヤー同士・トレイルとの当たり判定は (s, t) 空間で
    /// CombatManager がまとめて行い、結果は ApplyKnockback / RecoverGauge で受け取る。
    ///
    /// 入力ソースは playerIndex で決定:
    ///   0 → Gamepad.all[0] があればそれ、無ければ Keyboard WASD + LeftShift
    ///   1 → Gamepad.all[1] があればそれ、無ければ Keyboard 矢印 + RightShift
    /// オンライン対応時は入力読み取りメソッドだけ差し替える想定。
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

        [Header("Boost")]
        [SerializeField] float boostSpeed = 28f;
        [SerializeField] float boostAcceleration = 24f;
        [Tooltip("ブースト終了後、boostSpeed から maxSpeed まで戻る減速")]
        [SerializeField] float overspeedDecay = 12f;
        [SerializeField] float maxGauge = 100f;
        [SerializeField] float gaugeDrainPerSecond = 35f;
        [SerializeField] float gaugeRegenPerSecond = 12f;

        [Header("Lateral (t)")]
        [SerializeField] float strafeSpeed = 9f;
        [Tooltip("スタート時の横オフセット。2 機が重ならないよう P1/P2 で符号を変える")]
        [SerializeField] float startLateralOffset = 0f;

        [Header("Wall")]
        [Tooltip("壁接触時に発生する横方向の跳ね返り初速")]
        [SerializeField] float wallBounce = 6f;
        [SerializeField] float wallBounceDecay = 20f;
        [Tooltip("壁接触時に残す前進速度の割合（0.8 = 20% 減速）")]
        [SerializeField] float wallSpeedRetain = 0.8f;

        [Header("Course")]
        [SerializeField] CourseSpline course;

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

        Rigidbody _rigidbody;
        float _lateralBounce;
        float _stunTimer;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            if (course == null)
                course = FindAnyObjectByType<CourseSpline>();

            Gauge = maxGauge;
        }

        void Start()
        {
            // Spread starting positions so players don't overlap at the start line.
            LateralOffset = startLateralOffset;
            SnapToCourse();
        }

        void FixedUpdate()
        {
            if (course == null || HasFinished)
                return;

            var move = ReadMove();
            bool boostHeld = ReadBoost();
            var dt = Time.fixedDeltaTime;

            // スタン中は操作不能（慣性・弾き・スプライン追従は継続）。
            if (_stunTimer > 0f)
            {
                _stunTimer -= dt;
                move = Vector2.zero;
                boostHeld = false;
            }

            IsBoosting = boostHeld && Gauge > 0f;
            Gauge = Mathf.Clamp(
                Gauge + (IsBoosting ? -gaugeDrainPerSecond : gaugeRegenPerSecond) * dt,
                0f, maxGauge);

            ForwardSpeed = StepForward(ForwardSpeed, move.y, IsBoosting, dt);
            DistanceAlongCourse += ForwardSpeed * dt;

            StepLateral(move.x, dt);

            if (DistanceAlongCourse >= course.Length)
            {
                DistanceAlongCourse = course.Length;
                HasFinished = true;
                ForwardSpeed = 0f;
            }

            SnapToCourse();
        }

        void StepLateral(float input, float dt)
        {
            float desired = input * strafeSpeed;
            _lateralBounce = Mathf.MoveTowards(_lateralBounce, 0f, wallBounceDecay * dt);
            float velocity = desired + _lateralBounce;
            LateralOffset += velocity * dt;

            float halfWidth = course.HalfWidth;
            if (LateralOffset > halfWidth)
            {
                LateralOffset = halfWidth;
                if (velocity > 0f) HitWall(-1f);
            }
            else if (LateralOffset < -halfWidth)
            {
                LateralOffset = -halfWidth;
                if (velocity < 0f) HitWall(1f);
            }
        }

        void HitWall(float inwardSign)
        {
            _lateralBounce = inwardSign * wallBounce;
            ForwardSpeed *= wallSpeedRetain;
        }

        /// CombatManager から攻撃ヒット結果を適用する。前進減速＋横方向の弾き。
        public void ApplyKnockback(float forwardSpeedFactor, float lateralImpulse)
        {
            ForwardSpeed *= forwardSpeedFactor;
            _lateralBounce += lateralImpulse;
        }

        /// トレイル通過などでゲージを回復する。
        public void RecoverGauge(float amount)
        {
            Gauge = Mathf.Clamp(Gauge + amount, 0f, maxGauge);
        }

        /// 一定時間操作不能にする（被弾時など）。複数回呼ばれたら長い方を採用。
        public void Stun(float seconds)
        {
            _stunTimer = Mathf.Max(_stunTimer, seconds);
        }

        Vector2 ReadMove()
        {
            var gamepad = GetGamepad();
            if (gamepad != null)
                return gamepad.leftStick.ReadValue();

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            if (playerIndex == 0)
            {
                return new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            }

            return new Vector2(
                (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f));
        }

        bool ReadBoost()
        {
            var gamepad = GetGamepad();
            if (gamepad != null)
                return gamepad.rightTrigger.isPressed || gamepad.buttonSouth.isPressed;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return playerIndex == 0 ? keyboard.leftShiftKey.isPressed : keyboard.rightShiftKey.isPressed;
        }

        Gamepad GetGamepad()
        {
            return playerIndex < Gamepad.all.Count ? Gamepad.all[playerIndex] : null;
        }

        void SnapToCourse()
        {
            if (course == null) return;
            var position = course.GetWorldPosition(DistanceAlongCourse, LateralOffset);
            var rotation = course.GetRotation(DistanceAlongCourse);
            _rigidbody.MovePosition(position);
            _rigidbody.MoveRotation(rotation);
        }

        float StepForward(float current, float input, bool boosting, float dt)
        {
            if (boosting)
            {
                if (input > 0f)
                    current += boostAcceleration * input * dt;
                else if (input < 0f)
                    current += brakeDeceleration * input * dt;
                else
                    current = Mathf.MoveTowards(current, 0f, coastDeceleration * dt);
                return Mathf.Clamp(current, 0f, boostSpeed);
            }

            // 非ブーストで maxSpeed 超（ブースト余韻）は、加速入力で増やさず maxSpeed へ減衰させる。
            if (current > maxSpeed)
            {
                float decayed = Mathf.MoveTowards(current, maxSpeed, overspeedDecay * dt);
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
            return Mathf.Clamp(current, 0f, maxSpeed);
        }
    }
}
