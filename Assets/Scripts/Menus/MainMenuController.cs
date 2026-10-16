using FallingWizard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class MainMenuController : MenuScreen
    {
        [Header("Buttons")]
        [SerializeField] Button playButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        protected override Selectable DefaultSelection => playButton;

        void Start() => ShowPanel();

        protected override void WireButtons()
        {
            playButton.onClick.AddListener(OpenSkillScreen);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(Game.Quit);
        }

        static void OpenSkillScreen() => UI.SkillScreen.Open(Game.StartRun);
    }
}
