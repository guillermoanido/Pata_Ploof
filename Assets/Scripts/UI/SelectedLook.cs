using FallingWizard.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FallingWizard.UI
{
    public class SelectedLook : MonoBehaviour
    {
        [Tooltip("Shown while this button is selected, hidden otherwise. Usually a frame placed " +
                 "as a child of the button.")]
        public GameObject look;

        [Tooltip("Also show it while playing with keyboard and mouse. Off, it only shows while a " +
                 "controller is in use.")]
        public bool showWithKeyboard = false;

        bool IsSelected => EventSystem.current != null &&
                           EventSystem.current.currentSelectedGameObject == gameObject;

        bool IsWanted => showWithKeyboard || Controls.Scheme == Controls.GamepadScheme;

        void Awake()
        {
            if (look != null)
                look.SetActive(false);
        }

        void Update()
        {
            if (look != null)
                look.SetActive(IsSelected && IsWanted);
        }

        void OnDisable()
        {
            if (look != null)
                look.SetActive(false);
        }
    }
}
