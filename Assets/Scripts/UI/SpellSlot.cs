using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class SpellSlot : MonoBehaviour
    {
        [Tooltip("The slot's button. Pressing it puts the picked spell in this slot. The " +
                 "slots are numbered in the order they sit under Slots, first one first.")]
        public Button plate;

        [Tooltip("The spell in the slot. Hidden while the slot is empty, and filled with the " +
                 "spell's own Icon while the game runs.")]
        public Image icon;

        [Tooltip("Which button this slot is, as a picture.")]
        public ButtonPrompt prompt;

        [Tooltip("The name of the spell in the slot.")]
        public TextMeshProUGUI caption;
    }
}
