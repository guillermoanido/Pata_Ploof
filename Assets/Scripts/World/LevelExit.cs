using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : PlayerTrigger
    {
        [Header("Exit")]
        [Tooltip("Bank the wisps being carried when the level is finished, the way turning back " +
                 "at a rest site does.")]
        public bool banksWisps = false;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (UI.Screens.ModalOpen || !wizard.Logic.health.IsAlive)
                return;

            if (banksWisps)
                Progress.BankCarried();

            Progress.CompleteLevel(SceneManager.GetActiveScene().name);
            Progress.ClearCheckpoint();
            Game.LoadLevelSelect();
        }
    }
}
