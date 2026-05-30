using UnityEngine;
using UnityEngine.InputSystem;

namespace Battrail
{
    /// プレイヤー機の移動本体。スプライン相対の (s, t) を内部状態として持ち、
    /// 入力に応じて s を加減速、t を左右移動させる。ワールド変換はコース（CourseSpline）から計算する。
    /// 物理体は kinematic Rigidbody。衝突は別系統（後続でトリガーコライダ等で取る想定）。
    [RequireComponent(typeof(Rigidbody))]
    public class Racer : MonoBehaviour
    {
        [Header("Forward (s)")]
        [SerializeField] float maxSpeed = 18f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float brakeDeceleration = 26f;
        [SerializeField] float coastDeceleration = 7f;

        [Header("Lateral (t)")]
        [SerializeField] float strafeSpeed = 9f;

        [Header("Course")]
        [SerializeField] CourseSpline course;

        public float DistanceAlongCourse { get; private set; }
        public float LateralOffset { get; private set; }
        public float ForwardSpeed { get; private set; }
        public bool HasFinished { get; private set; }
        public CourseSpline Course => course;

        Rigidbody _rigidbody;
        InputSystem_Actions _input;
        InputAction _moveAction;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.None;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            if (course == null)
                course = FindAnyObjectByType<CourseSpline>();

            _input = new InputSystem_Actions();
            _moveAction = _input.Player.Move;
        }

        void Start()
        {
            SnapToCourse();
        }

        void OnEnable() => _input?.Player.Enable();
        void OnDisable() => _input?.Player.Disable();
        void OnDestroy() => _input?.Dispose();

        void FixedUpdate()
        {
            if (course == null || HasFinished)
                return;

            var move = _moveAction.ReadValue<Vector2>();
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
