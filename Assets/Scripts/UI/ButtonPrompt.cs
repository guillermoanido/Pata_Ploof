using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ButtonPrompt : MonoBehaviour
    {
        [Tooltip("The input action to show, like Player/Spell1 or UI/Pause. Leave it empty where " +
                 "a script picks the action - the HUD, the skill screen and the controls sheet do.")]
        public string action = "";

        [Tooltip("Whose button to show. Current follows whatever the player last touched, so it " +
                 "shows the key on a keyboard and the pad button on a controller.")]
        public Device device = Device.Current;

        [Tooltip("Shows the button's picture. The pictures live in Resources/Button Icons: swap " +
                 "them there and every prompt in the game follows.")]
        public Image icon;

        [Tooltip("Shown instead, as the button's name in words, for a button Button Icons has no " +
                 "picture for yet. Optional.")]
        public TextMeshProUGUI fallback;

        InputAction shown;

        void OnEnable()
        {
            Core.Controls.SchemeChanged += Refresh;
            Refresh();
        }

        void OnDisable() => Core.Controls.SchemeChanged -= Refresh;

        public void Show(InputAction input)
        {
            shown = input;
            Refresh();
        }

        void Refresh()
        {
            InputAction input = FindAction();
            string scheme = PickScheme();
            Sprite picture = ButtonIcons.For(input, scheme);

            ShowPicture(picture);
            ShowWords(picture == null ? Core.Controls.GlyphFor(input, scheme) : string.Empty);
        }

        InputAction FindAction()
        {
            if (shown != null)
                return shown;

            return string.IsNullOrEmpty(action) ? null : Core.Controls.Lookup(action);
        }

        string PickScheme()
        {
            switch (device)
            {
                case Device.Keyboard:
                    return Core.Controls.KeyboardScheme;
                case Device.Controller:
                    return Core.Controls.GamepadScheme;
                default:
                    return Core.Controls.Scheme;
            }
        }

        void ShowPicture(Sprite picture)
        {
            if (icon == null)
                return;

            icon.sprite = picture;
            icon.enabled = picture != null;
        }

        void ShowWords(string words)
        {
            if (fallback == null)
                return;

            fallback.text = words;
            fallback.enabled = !string.IsNullOrEmpty(words);
        }

        public enum Device
        {
            Current,
            Keyboard,
            Controller,
        }
    }
}
