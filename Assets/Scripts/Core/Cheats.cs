using System;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    [DefaultExecutionOrder(RunBeforeEverythingElse)]
    public class Cheats : MonoBehaviour
    {
        const int RunBeforeEverythingElse = -200;

        const int LabelFontSize = 18;
        const float LabelWidth = 300f;
        const float LabelHeight = 30f;
        const float LabelTopMargin = 8f;
        const float LabelRightMargin = 20f;

        static readonly Color LabelColour = new Color(1f, 0.82f, 0.25f);

        static Cheats instance;
        static bool isActive;

        public static event Action Toggled;

        public static bool IsActive => isActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            var cheatsObject = new GameObject("Cheats") { hideFlags = HideFlags.HideAndDontSave };
            instance = cheatsObject.AddComponent<Cheats>();
            DontDestroyOnLoad(cheatsObject);
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
                Toggle();

            if (!isActive)
                return;

            SetWizardInvulnerable(true);

            if (keyboard != null && keyboard.lKey.wasPressedThisFrame && !Screens.IsModalOpen)
                Game.LoadLevelSelect();
        }

        void OnGUI()
        {
            if (!isActive)
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = LabelFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
            };

            style.normal.textColor = LabelColour;

            var area = new Rect(Screen.width - (LabelWidth + LabelRightMargin), LabelTopMargin,
                                LabelWidth, LabelHeight);

            GUI.Label(area, "CHEATS ON (M)  LEVELS (L)", style);
        }

        void Toggle()
        {
            isActive = !isActive;
            Progress.IsSpendingFree = isActive;

            SetWizardInvulnerable(isActive);

            Debug.Log(isActive
                ? "CHEATS ON - god mode, everything costs no Magical Scrap, and every level is open. Press L for the Level Select, M to turn cheats off."
                : "CHEATS OFF - damage, Magical Scrap costs and locked levels are back to normal.");

            Toggled?.Invoke();
        }

        void SetWizardInvulnerable(bool invulnerable)
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.Invulnerable = invulnerable;
        }
    }
}
