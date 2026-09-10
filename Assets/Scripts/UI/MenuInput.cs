using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Battrail.UI
{
    /// メニュー系画面の「押した瞬間」判定。キーボードが接続されていない環境への対処と、
    /// どのゲームパッドで操作されても効くようにする走査をここにまとめる。
    /// 何を割り当てるかは画面ごとに違うので、キー・ボタンの選択は呼び出し側に残す。
    public static class MenuInput
    {
        public static bool KeyPressed(Func<Keyboard, ButtonControl> key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && key(keyboard).wasPressedThisFrame;
        }

        public static bool GamepadPressed(Func<Gamepad, ButtonControl> button)
        {
            foreach (var gamepad in Gamepad.all)
                if (button(gamepad).wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
