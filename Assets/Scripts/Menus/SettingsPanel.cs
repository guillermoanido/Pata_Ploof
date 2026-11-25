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

        static readonly Language[] Languages = (Language[])Enum.GetValues(typeof(Language));

        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

        [Tooltip("Wipes every spell, all the Knowledge and every heart and returns to the " +
                 "main menu, after asking.")]
        [SerializeField] Button resetSaveButton;

        [Tooltip("Opens a read-only sheet of every button, keyboard down one column and " +
                 "controller down the other.")]
        [SerializeField] Button controlsButton;

        [Tooltip("The language row's dropdown. This rig exists twice - once inside the Pause Menu " +
                 "prefab and once inside the Main Menu scene - so leaving it empty is allowed and " +
                 "the panel simply carries on with no language row, rather than throwing on Awake " +
                 "while the second copy is still being wired up.")]
        [SerializeField] TMP_Dropdown languageDropdown;

        [Tooltip("The Pixel Font row's toggle: ticked shows every text in the pixel font, " +
                 "unticked in the plain one. The two fonts are set in Resources/Game Fonts. " +
                 "Leave it empty and the panel simply has no font row.")]
        [SerializeField] Toggle pixelFontToggle;

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

            if (controlsButton != null)
                controlsButton.onClick.AddListener(ShowControls);

            if (resetSaveButton != null)
                resetSaveButton.onClick.AddListener(AskToResetSave);

            if (languageDropdown != null)
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            if (pixelFontToggle != null)
                pixelFontToggle.onValueChanged.AddListener(OnPixelFontChanged);

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
        }

        void OnEnable()
        {
            ShowCurrentSettings();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
        }

        void OnDisable()
        {
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

        static void OnPixelFontChanged(bool isOn) => GameSettings.UsesPixelFont = isOn;

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

        static void ShowControls() => UI.ControlsScreen.Open();

        static void AskToResetSave() => UI.ResetSaveScreen.Open();

        static void OnLanguageChanged(int index)
        {
            if (index < 0 || index >= Languages.Length)
                return;

            Loc.Set(Languages[index]);
        }

        void ShowCurrentSettings()
        {
            resolutionDropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.IsFullscreen);
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            UpdateVolumeLabel(GameSettings.Volume);

            if (languageDropdown != null)
                languageDropdown.SetValueWithoutNotify(Array.IndexOf(Languages, Loc.Language));

            if (pixelFontToggle != null)
                pixelFontToggle.SetIsOnWithoutNotify(GameSettings.UsesPixelFont);
        }
    }
}
