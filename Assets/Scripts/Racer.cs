using UnityEngine;
using UnityEngine.InputSystem;

namespace Battrail
{
    /// プレイヤー機の移動本体。スプライン相対の (s, t) を内部状態として持ち、
    /// 入力に応じて s を加減速、t を左右移動させる。ワールド変換はコース（CourseSpline）から計算する。
    /// 物理体は kinematic Rigidbody。衝突は別系統（後続でトリガーコライダ等で取る想定）。
    ///
    /// 入力ソースは playerIndex で決定:
    ///   0 → Gamepad.all[0] があればそれ、無ければ Keyboard WASD
    ///   1 → Gamepad.all[1] があればそれ、無ければ Keyboard 矢印キー
    /// オンライン対応時はこのメソッドだけ差し替える想定。
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

        [Header("Lateral (t)")]
        [SerializeField] float strafeSpeed = 9f;
        [Tooltip("スタート時の横オフセット。2 機が重ならないよう P1/P2 で符号を変える")]
        [SerializeField] float startLateralOffset = 0f;

        [Header("Course")]
        [SerializeField] CourseSpline course;

        public int PlayerIndex => playerIndex;
        public float DistanceAlongCourse { get; private set; }
        public float LateralOffset { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool HasFinished { get; private set; }
        public CourseSpline Course => course;

        Rigidbody _rigidbody;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            if (course == null)
                course = FindAnyObjectByType<CourseSpline>();
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
            var dt = Time.fixedDeltaTime;

            ForwardSpeed = StepForward(ForwardSpeed, move.y, dt);
            DistanceAlongCourse += ForwardSpeed * dt;

            LateralOffset += move.x * strafeSpeed * dt;
            LateralOffset = Mathf.Clamp(LateralOffset, -course.HalfWidth, course.HalfWidth);

            if (DistanceAlongCourse >= course.Length)
            {
                DistanceAlongCourse = course.Length;
                HasFinished = true;
                ForwardSpeed = 0f;
            }

            SnapToCourse();
        }

        Vector2 ReadMove()
        {
            if (playerIndex < Gamepad.all.Count)
            {
                var gamepad = Gamepad.all[playerIndex];
                if (gamepad != null)
                    return gamepad.leftStick.ReadValue();
            }

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

        void SnapToCourse()
        {
            if (course == null) return;
            var position = course.GetWorldPosition(DistanceAlongCourse, LateralOffset);
            var rotation = course.GetRotation(DistanceAlongCourse);
            _rigidbody.MovePosition(position);
            _rigidbody.MoveRotation(rotation);
        }

        float StepForward(float current, float input, float dt)
        {
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
