using Unity.Cinemachine;
using UnityEngine;

namespace Battrail
{
    /// ブーストの吹き始めにカメラを一度後ろへ引き、徐々に元の位置へ戻す。
    /// CinemachineFollow の FollowOffset を動かすだけで、寄り引きの滑らかさは
    /// Cinemachine 側の PositionDamping に任せる。
    ///
    /// 更新は Update で行う（CinemachineBrain は LateUpdate 更新なので、同じフレーム内で反映される）。
    [RequireComponent(typeof(CinemachineFollow))]
    public class BoostCameraKick : MonoBehaviour
    {
        [SerializeField] int playerIndex = 0;

        [Header("Kick")]
        [Tooltip("引き切ったときに FollowOffset へ加算する量。z をマイナスにすると後ろへ引く")]
        [SerializeField] Vector3 kickOffset = new(0f, 0.5f, -3f);
        [Tooltip("引き切るまでの時間（秒）。CinemachineFollow の PositionDamping より長くしないと、" +
                 "引きの速さが damping 側に支配されて速く見える")]
        [SerializeField] float kickInDuration = 0.3f;
        [Tooltip("引いた位置から元へ戻るまでの時間（秒）")]
        [SerializeField] float recoverDuration = 1.2f;

        CinemachineFollow _follow;
        Racer _racer;
        Vector3 _baseOffset;
        float _kick;
        bool _kickingIn;
        bool _wasBoosting;

        private void Awake()
        {
            _follow = GetComponent<CinemachineFollow>();
            _baseOffset = _follow.FollowOffset;
        }

        private void Update()
        {
            if (_racer == null)
                _racer = Racer.Find(playerIndex);
            if (_racer == null)
                return;

            bool boosting = _racer.IsBoosting;
            if (boosting && !_wasBoosting)
                _kickingIn = true;
            _wasBoosting = boosting;

            if (_kickingIn)
            {
                _kick = Mathf.MoveTowards(_kick, 1f, Time.deltaTime / Mathf.Max(0.0001f, kickInDuration));
                if (_kick >= 1f)
                    _kickingIn = false;
            }
            else if (_kick > 0f)
            {
                _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime / Mathf.Max(0.0001f, recoverDuration));
            }
            else
            {
                return;
            }

            _follow.FollowOffset = _baseOffset + kickOffset * Mathf.SmoothStep(0f, 1f, _kick);
        }
    }
}
