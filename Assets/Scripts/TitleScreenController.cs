using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Battrail
{
    /// タイトル画面。いずれかのキー／ゲームパッドボタンで次のシーン（レース）に進む。
    [RequireComponent(typeof(UIDocument))]
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] string nextScene = "Boot";
        [SerializeField] float promptPulseSpeed = 3f;

        Label _prompt;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _prompt = root.Q<Label>("prompt");
        }

        void Update()
        {
            if (_prompt != null)
                _prompt.style.opacity = 0.5f + 0.5f * Mathf.Sin(Time.time * promptPulseSpeed);

            if (StartPressed())
                SceneManager.LoadScene(nextScene);
        }

        bool StartPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            foreach (var gamepad in Gamepad.all)
                if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame)
                    return true;

            return false;
        }
    }
}
