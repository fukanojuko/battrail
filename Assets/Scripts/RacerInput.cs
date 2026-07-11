using UnityEngine;
using UnityEngine.InputSystem;

namespace Battrail
{
    /// プレイヤーのデバイス入力読み取り。playerIndex で使用デバイスを決定する。
    ///   0 → Gamepad.all[0] があればそれ、無ければ Keyboard WASD + LeftShift
    ///   1 → Gamepad.all[1] があればそれ、無ければ Keyboard 矢印 + RightShift
    /// オンライン対応時はこのクラスだけ差し替える想定。
    public sealed class RacerInput
    {
        readonly int _playerIndex;

        public RacerInput(int playerIndex)
        {
            _playerIndex = playerIndex;
        }

        public Vector2 ReadMove()
        {
            var gamepad = GetGamepad();
            if (gamepad != null)
                return gamepad.leftStick.ReadValue();

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            if (_playerIndex == 0)
            {
                return new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            }

            return new Vector2(
                (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f));
        }

        public bool ReadBoost()
        {
            var gamepad = GetGamepad();
            if (gamepad != null)
                return gamepad.rightTrigger.isPressed || gamepad.buttonSouth.isPressed;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return _playerIndex == 0 ? keyboard.leftShiftKey.isPressed : keyboard.rightShiftKey.isPressed;
        }

        Gamepad GetGamepad()
        {
            return _playerIndex < Gamepad.all.Count ? Gamepad.all[_playerIndex] : null;
        }
    }
}
