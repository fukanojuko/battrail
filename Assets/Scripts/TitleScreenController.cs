using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Battrail
{
    /// タイトル画面。対戦形式（1P vs NPC / 2P ローカル）を選んで次のシーン（レース）に進む。
    /// 選んだ結果は GameMode に載せて Boot へ持ち越す。
    [RequireComponent(typeof(UIDocument))]
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] string nextScene = "Boot";
        [SerializeField] float promptPulseSpeed = 3f;

        const string SelectedClass = "selected";

        readonly Label[] _options = new Label[2];
        int _selected;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _options[0] = root.Q<Label>("mode-1p");
            _options[1] = root.Q<Label>("mode-2p");
            _selected = 0;
            ApplySelection();
        }

        private void Update()
        {
            int step = ReadVerticalStep();
            if (step != 0)
            {
                _selected = Mathf.Clamp(_selected + step, 0, _options.Length - 1);
                ApplySelection();
            }

            // 点滅は選択中の項目だけに掛ける（従来の PRESS START と同じ見え方を残す）。
            var current = _options[_selected];
            if (current != null)
                current.style.opacity = 0.5f + 0.5f * Mathf.Sin(Time.time * promptPulseSpeed);

            if (ConfirmPressed())
            {
                GameMode.VsNpc = _selected == 0;
                SceneManager.LoadScene(nextScene);
            }
        }

        void ApplySelection()
        {
            for (int i = 0; i < _options.Length; i++)
            {
                if (_options[i] == null)
                    continue;

                if (i == _selected)
                    _options[i].AddToClassList(SelectedClass);
                else
                    _options[i].RemoveFromClassList(SelectedClass);

                // 非選択側に前回の点滅の opacity が残らないよう戻す。
                _options[i].style.opacity = 1f;
            }
        }

        /// 下方向で +1、上方向で -1。スティックは押した瞬間だけを拾う。
        int ReadVerticalStep()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                    return 1;
                if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                    return -1;
            }

            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.dpad.down.wasPressedThisFrame || gamepad.leftStick.down.wasPressedThisFrame)
                    return 1;
                if (gamepad.dpad.up.wasPressedThisFrame || gamepad.leftStick.up.wasPressedThisFrame)
                    return -1;
            }

            return 0;
        }

        bool ConfirmPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
