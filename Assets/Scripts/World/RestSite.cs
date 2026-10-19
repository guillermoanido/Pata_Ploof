using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.World
{
    public class RestSite : Checkpoint
    {
        [Header("Rest")]
        [Tooltip("What the screen is headed with. This is the English. It is only used when the " +
                 "key below is empty, or when no translation has that key yet.")]
        public string title = "A place to rest";

        [Tooltip("Which translated line the heading uses. Leave the standard key here for an " +
                 "ordinary rest site. CLEAR IT for a one-off rest site with words written for " +
                 "this spot, and the English typed above is shown exactly as typed, in every " +
                 "language - which is what you want for something nobody is going to translate.")]
        public string titleKey = Loc.Keys.RestTitle;

        [Tooltip("The line under the heading. Say where this leads. The English, as above.")]
        public string blurb = "Further down, or back the way you came.";

        [Tooltip("Which translated line the blurb uses. Clear it to use the English above verbatim.")]
        public string blurbKey = Loc.Keys.RestBlurb;

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.IsModalOpen)
                return;

            base.OnPlayerEntered(wizard);
            OfferChoices(wizard);
        }

        void OfferChoices(PlayerCharacter wizard)
        {
            ChoiceScreen screen = ChoiceScreen.Open(Loc.Text(titleKey, title),
                                                    Loc.Text(blurbKey, blurb));

            screen.Status(Loc.Format(Loc.Keys.RestStatus, Progress.CarriedWisps, Progress.Wisps));

            screen.Choice(Loc.Get(Loc.Keys.RestPressOn), () => RestAndPressOn(wizard, screen));

            screen.Choice(Loc.Format(Loc.Keys.RestTurnBack, Progress.CarriedWisps),
                          () => screen.CloseThen(TurnBack));
        }

        void RestAndPressOn(PlayerCharacter wizard, ChoiceScreen screen)
        {
            wizard.Logic.RestoreHealth();
            wizard.Logic.spellbook.ResetForRun();
            screen.Close();
        }

        void TurnBack()
        {
            Progress.EndRun();
            SkillScreen.Open(Game.LoadFirstLevel);
        }
    }
}
