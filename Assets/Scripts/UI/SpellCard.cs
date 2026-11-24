using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class SpellCard : MonoBehaviour
    {
        [Tooltip("The spell this card sells. Drag its asset in from Assets/Abilities. A card " +
                 "with no spell is hidden while the game runs.")]
        public Ability spell;

        [Tooltip("The card itself. Selecting it picks the spell to move between slots.")]
        public Button button;

        [Tooltip("The spell's picture. The sprite you put on this Image is the one the card " +
                 "shows, so each card can have its own art. Leave the sprite empty and the " +
                 "card shows the spell's own Icon instead.")]
        public Image icon;

        [Tooltip("Shown while the spell is not learned yet, for example a padlock over the " +
                 "icon. A child object, switched on and off.")]
        public GameObject lockedLook;

        [Tooltip("Shown while the spell sits in one of the slots. A child object, switched on " +
                 "and off.")]
        public GameObject equippedLook;

        [Tooltip("Inside the Equipped Look: which button the spell is on, as a picture.")]
        public ButtonPrompt equippedPrompt;

        [Tooltip("The spell's name. The words come from Resources/Language/Text.csv, so the " +
                 "text typed here is only a preview: style it here, change the words there.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("One pip per rank the spell can reach, filled up to the rank it has.")]
        public BlipRow pips = new BlipRow();

        [Tooltip("What the spell does and what its next upgrade gives. The words come from " +
                 "Text.csv, like the name.")]
        public TextMeshProUGUI blurb;

        [Tooltip("Learn or upgrade button.")]
        public Button action;

        [Tooltip("Shown instead of the button once there is nothing left to buy.")]
        public TextMeshProUGUI status;
    }
}
