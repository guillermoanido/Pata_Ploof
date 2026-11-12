using System;
using FallingWizard.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ControlsScreen : MonoBehaviour
    {
        const string PrefabName = "Controls Screen";

        [Header("Rig")]
        public Button closeButton;

        [Tooltip("One entry per line of the sheet. Each names the input action it explains and " +
                 "the three labels that show it. Add a row in the prefab, then add it here.")]
        public Row[] rows = new Row[0];

        static ControlsScreen openScreen;

        void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            Loc.Changed += ShowRows;
            ShowRows();
        }

        void Update()
        {
            if (Core.Controls.WasPausePressed || Core.Controls.WasCancelPressed)
                Close();
        }

        void OnDisable() => Loc.Changed -= ShowRows;

        void OnDestroy()
        {
            if (openScreen == this)
                MarkClosed();
        }

        public static ControlsScreen Open()
        {
            if (openScreen != null)
                return openScreen;

            openScreen = Ui.OpenScreen<ControlsScreen>(PrefabName);

            if (openScreen == null)
                return null;

            Ui.ClaimScreen();

            if (openScreen.closeButton != null)
                Ui.Focus(openScreen.closeButton.gameObject);

            return openScreen;
        }

        void ShowRows()
        {
            foreach (Row row in rows)
                row.Show();
        }

        void Close()
        {
            MarkClosed();
            Destroy(gameObject);
        }

        void MarkClosed()
        {
            openScreen = null;
            Ui.ReleaseScreen();
        }

        [Serializable]
        public class Row
        {
            [Tooltip("Translation key of the row's name, for example 'controls.move'.")]
            public string nameKey;

            [Tooltip("For a name with {0} in it, like 'controls.spell': the number put there. " +
                     "0 for a name without one.")]
            [Min(0)] public int number;

            [Tooltip("The input action this row explains, for example 'Player/Move'.")]
            public string action;

            public TextMeshProUGUI nameLabel;
            public TextMeshProUGUI keyboardLabel;
            public TextMeshProUGUI controllerLabel;

            public void Show()
            {
                InputAction input = Core.Controls.Lookup(action);

                if (nameLabel != null)
                    nameLabel.text = number > 0 ? Loc.Format(nameKey, number) : Loc.Get(nameKey);

                if (keyboardLabel != null)
                    keyboardLabel.text = DescribeBinding(input, Core.Controls.KeyboardScheme);

                if (controllerLabel != null)
                    controllerLabel.text = DescribeBinding(input, Core.Controls.GamepadScheme);
            }

            static string DescribeBinding(InputAction input, string scheme)
            {
                string glyph = Core.Controls.GlyphFor(input, scheme);

                return string.IsNullOrEmpty(glyph) ? Loc.Get("controls.none") : glyph;
            }
        }
    }
}
