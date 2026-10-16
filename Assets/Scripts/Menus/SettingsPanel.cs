using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class SettingsPanel : MonoBehaviour
    {
        const float AsPercent = 100f;

        const float BuiltButtonWidth = 260f;
        const float BuiltButtonHeight = 56f;

        static readonly Language[] Languages = (Language[])Enum.GetValues(typeof(Language));

        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

        [Tooltip("Wipes every spell, wisp and heart and returns to the main menu, after asking. " +
                 "Leave it empty and the panel builds its own beside the Back button, so the " +
                 "Pause Menu prefab and the Main Menu scene both get one without being rewired.")]
        [SerializeField] Button resetSaveButton;

        [Tooltip("Opens a read-only sheet of every button, keyboard down one column and " +
                 "controller down the other. Leave it empty and the panel builds its own beside " +
                 "the Back button, so both rigs get one without being rewired.")]
        [SerializeField] Button controlsButton;

        [Tooltip("The language row's dropdown. This rig exists twice - once inside the Pause Menu " +
                 "prefab and once inside the Main Menu scene - so leaving it empty is allowed and " +
                 "the panel simply carries on with no language row, rather than throwing on Awake " +
                 "while the second copy is still being wired up.")]
        [SerializeField] TMP_Dropdown languageDropdown;

        [Tooltip("Rows that only make sense on desktop. Hidden on consoles, which pick their own " +
                 "output mode. The language row does NOT belong in here - a console player picks " +
                 "their language too.")]
        [SerializeField] GameObject[] desktopOnlyRows;

        public event Action Closed;

        void Awake()
        {
            FillResolutionDropdown();
            FillLanguageDropdown();

            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            backButton.onClick.AddListener(Close);

            EnsureControlsButton();
            controlsButton.onClick.AddListener(ShowControls);

            EnsureResetButton();
            resetSaveButton.onClick.AddListener(AskToResetSave);

            if (languageDropdown != null)
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
        }

        void OnEnable()
        {
            Loc.Changed += Retranslate;

            ShowCurrentSettings();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
        }

        void OnDisable()
        {
            Loc.Changed -= Retranslate;
            GameSettings.Save();
        }

        void FillResolutionDropdown()
        {
            var names = new List<string>();
            foreach (Resolution option in GameSettings.Resolutions)
                names.Add($"{option.width} x {option.height}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(names);
        }

        void FillLanguageDropdown()
        {
            if (languageDropdown == null)
                return;

            var names = new List<string>();
            foreach (Language option in Languages)
                names.Add(Loc.NameOf(option));

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(names);
        }

        static void OnResolutionChanged(int index) => GameSettings.ResolutionIndex = index;

        static void OnFullscreenChanged(bool isOn) => GameSettings.IsFullscreen = isOn;

        void OnVolumeChanged(float value)
        {
            GameSettings.Volume = value;
            UpdateVolumeLabel(value);
        }

        void UpdateVolumeLabel(float value)
        {
            if (volumeValueLabel != null)
                volumeValueLabel.text = $"{Mathf.RoundToInt(value * AsPercent)}%";
        }

        void Close() => Closed?.Invoke();

        void EnsureControlsButton()
        {
            if (controlsButton == null)
                controlsButton = BuildButtonBesideBack(Loc.Keys.SettingsControls);
        }

        Button BuildButtonBesideBack(string labelKey)
        {
            Button button = UI.Ui.CreateButton(Loc.Get(labelKey), backButton.transform.parent,
                BuiltButtonWidth, BuiltButtonHeight);

            button.transform.SetSiblingIndex(backButton.transform.GetSiblingIndex());
            return button;
        }

        static void ShowControls() => UI.ControlsScreen.Open();

        void EnsureResetButton()
        {
            if (resetSaveButton == null)
                resetSaveButton = BuildButtonBesideBack(Loc.Keys.SettingsResetSave);
        }

        void AskToResetSave()
        {
            UI.ChoiceScreen screen = UI.ChoiceScreen.Open(
                Loc.Get(Loc.Keys.SettingsResetSaveTitle),
                Loc.Get(Loc.Keys.SettingsResetSaveBlurb));

            screen.Status(Loc.Format(Loc.Keys.SettingsResetSaveStatus, Progress.Wisps));

            screen.Choice(Loc.Get(Loc.Keys.SettingsResetSaveConfirm),
                () => screen.CloseThen(ResetSaveAndLoadMainMenu));

            screen.Choice(Loc.Get(Loc.Keys.SettingsResetSaveCancel), screen.Close);
        }

        static void ResetSaveAndLoadMainMenu()
        {
            Progress.ResetSave();
            Game.LoadMainMenu();
        }

        static void OnLanguageChanged(int index)
        {
            if (index < 0 || index >= Languages.Length)
                return;

            Loc.Set(Languages[index]);
        }

        void Retranslate()
        {
            UI.Ui.Retext(controlsButton, Loc.Get(Loc.Keys.SettingsControls));
            UI.Ui.Retext(resetSaveButton, Loc.Get(Loc.Keys.SettingsResetSave));
        }

        void ShowCurrentSettings()
        {
            resolutionDropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.IsFullscreen);
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            UpdateVolumeLabel(GameSettings.Volume);
            Retranslate();

            if (languageDropdown != null)
                languageDropdown.SetValueWithoutNotify(Array.IndexOf(Languages, Loc.Language));
        }
    }
}
