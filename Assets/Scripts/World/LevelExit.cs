using FallingWizard.Core;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.World
{
    public class LevelExit : Item
    {
        [Header("Exit")]
        [Tooltip("Bank the wisps being carried when the level is finished, the way turning back " +
                 "at a rest site does.")]
        public bool banksWisps = false;

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.IsModalOpen || !wizard.Logic.health.IsAlive)
                return;

            if (banksWisps)
                Progress.BankCarried();

            Progress.CompleteLevel(SceneManager.GetActiveScene().name);
            Progress.ClearCheckpoint();
            Game.LoadLevelSelect();
        }
    }
}
