using TMPro;
using UnityEngine;

namespace FallingWizard.Localization
{
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Which line of Assets/Resources/Language/Game Text this label shows: " +
                 "'menu.play', 'settings.volume', 'pause.title'. The words themselves live in " +
                 "Game Text, and this component shows them right here in English and Spanish, " +
                 "so you can change them without leaving the label.\n\nLeave it empty and this " +
                 "does nothing, which is what you want on a label the game writes into itself - " +
                 "the volume percentage, a price, a button glyph.")]
        public string key = "";

        TMP_Text label;

        void Awake()
        {
            label = GetComponent<TMP_Text>();

            if (label == null)
                Debug.LogWarning("Localized Text is on an object with no text on it, so there is " +
                                 "nothing here for it to translate. It goes on the same object " +
                                 "as the TextMeshPro - Text (UI) component, not on the row or " +
                                 "the button above it.", this);
        }

        void OnEnable()
        {
            Loc.Changed += Show;
            Show();
        }

        void OnDisable() => Loc.Changed -= Show;

        void Show()
        {
            if (label == null || string.IsNullOrEmpty(key))
                return;

            label.text = Loc.Get(key);
        }
    }
}
