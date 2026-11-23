using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.World
{
    public class LevelExit : Item
    {
        [Header("Ending")]
        [Tooltip("Tick this on the exit of the last level there is. Instead of going back to the " +
                 "Level Select, it shows the screen saying the wizard made it down the tower. " +
                 "When more levels are built, untick it here and tick it on the new last exit.")]
        public bool isFinalExit;

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Ui.IsModalOpen || !wizard.Logic.health.IsAlive)
                return;

            Progress.CompleteLevel(SceneManager.GetActiveScene().name);

            if (isFinalExit)
                ShowTheEnd();
            else
                Game.LoadLevelSelect();
        }

        static void ShowTheEnd()
        {
            ChoiceScreen screen = ChoiceScreen.Open(Loc.Get(Loc.Keys.EndTitle),
                Loc.Get(Loc.Keys.EndBlurb));

            if (screen == null)
            {
                Game.LoadLevelSelect();
                return;
            }

            screen.Choice(Loc.Get(Loc.Keys.EndMainMenu), () => screen.CloseThen(Game.LoadMainMenu));
        }
    }
}
