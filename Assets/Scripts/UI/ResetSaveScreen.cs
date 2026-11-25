using FallingWizard.Core;
using FallingWizard.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ResetSaveScreen : MonoBehaviour
    {
        const string PrefabName = "Reset Save Screen";

        [Tooltip("The line saying how much Knowledge would be lost. The game writes it, from the " +
                 "'settings.resetSave.status' line in Game Text with {0} where the number goes. " +
                 "Every other word on this screen is a label with its own Localized Text.")]
        public TextMeshProUGUI status;

        [Tooltip("Wipes every spell, all the Knowledge and every heart, then goes to the main menu.")]
        public Button confirmButton;

        [Tooltip("Closes this and goes back to Settings. Escape and the controller's back button " +
                 "do the same. It is picked first, so a stray press never wipes a save.")]
        public Button cancelButton;

        static ResetSaveScreen openScreen;

        void Awake()
        {
            confirmButton.onClick.AddListener(ResetAndLeave);
            cancelButton.onClick.AddListener(Close);
        }

        void Update()
        {
            if (Core.Controls.WasPausePressed || Core.Controls.WasCancelPressed)
                Close();
        }

        void OnDestroy()
        {
            if (openScreen == this)
                MarkClosed();
        }

        public static ResetSaveScreen Open()
        {
            if (openScreen != null)
                return openScreen;

            openScreen = Ui.OpenScreen<ResetSaveScreen>(PrefabName);

            if (openScreen == null)
                return null;

            Ui.ClaimScreen();
            openScreen.ShowWhatIsLost();
            Ui.Focus(openScreen.cancelButton.gameObject);

            return openScreen;
        }

        void ShowWhatIsLost()
        {
            if (status != null)
                status.text = Loc.Format(Loc.Keys.SettingsResetSaveStatus, Progress.Scrap);
        }

        void ResetAndLeave()
        {
            Close();
            Progress.ResetSave();
            Game.LoadMainMenu();
        }

        void Close()
        {
            MarkClosed();
            Destroy(gameObject);
        }

        static void MarkClosed()
        {
            openScreen = null;
            Ui.ReleaseScreen();
        }
    }
}
