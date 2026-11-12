using FallingWizard.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public abstract class MenuScreen : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject panel;
        [SerializeField] SettingsPanel settingsPanel;

        protected abstract Selectable DefaultSelection { get; }

        bool IsSettingsOpen => settingsPanel.gameObject.activeSelf;

        void Awake()
        {
            settingsPanel.Closed += CloseSettings;
            WireButtons();
        }

        void Update()
        {
            if (UI.Screens.IsModalOpen)
                return;

            if (Controls.WasPausePressed)
                GoBack();
        }

        void OnDestroy() => settingsPanel.Closed -= CloseSettings;

        protected abstract void WireButtons();

        protected virtual void OnBackPressed()
        {
        }

        protected void ShowPanel()
        {
            settingsPanel.gameObject.SetActive(false);
            panel.SetActive(true);
            FocusDefaultSelection();
        }

        protected void HidePanel()
        {
            settingsPanel.gameObject.SetActive(false);
            panel.SetActive(false);
        }

        protected void OpenSettings()
        {
            panel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        void CloseSettings() => ShowPanel();

        void GoBack()
        {
            if (IsSettingsOpen)
                CloseSettings();
            else
                OnBackPressed();
        }

        void FocusDefaultSelection()
        {
            if (DefaultSelection == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(DefaultSelection.gameObject);
        }
    }
}
