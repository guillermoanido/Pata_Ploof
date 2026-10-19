using FallingWizard.Core;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine.SceneManagement;

namespace FallingWizard.World
{
    public class LevelExit : Item
    {
        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.IsModalOpen || !wizard.Logic.health.IsAlive)
                return;

            Progress.CompleteLevel(SceneManager.GetActiveScene().name);
            Game.LoadLevelSelect();
        }
    }
}
