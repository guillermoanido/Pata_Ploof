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

        [Tooltip("The spell's picture, exactly as placed: each card can have its own art. A " +
                 "slot holding this spell shows the same picture.")]
        public Image icon;

        [Tooltip("Shown while the spell is not learned yet, for example a padlock over the " +
                 "icon. A child object, switched on and off.")]
        public GameObject lockedLook;

        [Tooltip("Shown while the spell sits in one of the slots. A child object, switched on " +
                 "and off.")]
        public GameObject equippedLook;

        [Tooltip("Inside the Equipped Look: which button the spell is on, as a picture.")]
        public ButtonPrompt equippedPrompt;

        [Tooltip("The spell's name. Its Localized Text names the line in Game Text and shows " +
                 "the English and Spanish to edit. The slots show this name too while the spell " +
                 "sits in one.")]
        public TextMeshProUGUI nameLabel;

        [Tooltip("One pip per rank the spell can reach, filled up to the rank it has.")]
        public BlipRow pips = new BlipRow();

        [Tooltip("The one line the game writes on the card: what the next upgrade gives. It is " +
                 "hidden while there is no upgrade to buy. Everything else on the card - the " +
                 "name and the description - shows exactly what you typed.")]
        public TextMeshProUGUI nextUpgrade;

        [Tooltip("Learn or upgrade button.")]
        public Button action;

        [Tooltip("Shown instead of the button once there is nothing left to buy.")]
        public TextMeshProUGUI status;
    }
}
