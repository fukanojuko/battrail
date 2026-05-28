using UnityEngine;
using UnityEngine.InputSystem;

namespace Battrail
{
    /// プレイヤー機の移動基盤。Input System の Move アクション (Vector2) を読み、
    /// 前後 (Y) は速度限界まで徐々に加減速、左右 (X) は簡易な平行移動として速度を出す。
    /// ブースト・壁衝突・トレイル等は後段で追加する想定。
    [RequireComponent(typeof(Rigidbody))]
    public class Racer : MonoBehaviour
    {
        [Header("Forward")]
        [SerializeField] float maxSpeed = 18f;
        [SerializeField] float acceleration = 14f;
        [SerializeField] float brakeDeceleration = 26f;
        [SerializeField] float coastDeceleration = 7f;

        [Header("Lateral (簡易な左右平行移動)")]
        [SerializeField] float strafeSpeed = 9f;

        public float ForwardSpeed { get; private set; }
        public float LateralSpeed { get; private set; }
        public float MaxSpeed => maxSpeed;

        Rigidbody _rigidbody;
        InputSystem_Actions _input;
        InputAction _moveAction;

        void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            _input = new InputSystem_Actions();
            _moveAction = _input.Player.Move;
        }

        void OnEnable() => _input.Player.Enable();
        void OnDisable() => _input.Player.Disable();
        void OnDestroy() => _input?.Dispose();

        void FixedUpdate()
        {
            var move = _moveAction.ReadValue<Vector2>();
            var dt = Time.fixedDeltaTime;

            ForwardSpeed = StepForward(ForwardSpeed, move.y, dt);
            LateralSpeed = move.x * strafeSpeed;

            _rigidbody.linearVelocity = new Vector3(LateralSpeed, 0f, ForwardSpeed);
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
