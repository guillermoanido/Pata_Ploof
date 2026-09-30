using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ControlsScreen : MonoBehaviour
    {
        const int DefaultSortingOrder = 260;

        const float PanelWidth = 900f;
        const float PanelPadding = 36f;
        const float PanelSpacing = 8f;

        const float TitleSize = 52f;
        const float TitleHeight = 68f;

        const float HeadingSize = 24f;
        const float HeadingHeight = 38f;

        const float SectionSize = 26f;
        const float SectionHeight = 46f;

        const float RowSize = 26f;
        const float RowHeight = 42f;

        const float NameColumn = 356f;
        const float GlyphColumn = 226f;

        const float ButtonWidth = 260f;
        const float ButtonHeight = 62f;

        static ControlsScreen open;

        RectTransform column;

        public static bool IsOpen => open != null;

        public static ControlsScreen Open(int sortingOrder = DefaultSortingOrder)
        {
            if (open != null)
                return open;

            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Controls", sortingOrder);
            open = canvas.gameObject.AddComponent<ControlsScreen>();
            open.Build();

            return open;
        }

        void Build()
        {
            Ui.Shroud(transform);

            column = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth, PanelPadding, PanelSpacing);

            Ui.Label(Loc.Get("controls.title"), column, TitleSize, Inner, TitleHeight);

            Heading();

            Section("controls.playing");
            Line(Loc.Get("controls.move"), "Player/Move");
            Line(Loc.Get("controls.walk"), "Player/Walk");

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
                Line(Loc.Format("controls.spell", i + 1),
                    "Player/" + PlayerLogic.Spellbook.SlotActions[i]);

            Line(Loc.Get("controls.loadout"), "UI/Loadout");
            Line(Loc.Get("controls.pause"), "UI/Pause");
            Line(Loc.Get("controls.skip"), "UI/Skip");

            Section("controls.menus");
            Line(Loc.Get("controls.menuMove"), "UI/Navigate");
            Line(Loc.Get("controls.menuPick"), "UI/Submit");
            Line(Loc.Get("controls.menuBack"), "UI/Cancel");

            Button back = Ui.CreateButton(Loc.Get("controls.close"), column,
                ButtonWidth, ButtonHeight);

            back.onClick.AddListener(Close);
            Ui.Focus(back.gameObject);
        }

        void Heading()
        {
            RectTransform row = Ui.Row("Heading", column, Inner, HeadingHeight);

            Ui.Label(string.Empty, row, HeadingSize, NameColumn, HeadingHeight,
                TextAlignmentOptions.Left);

            Cell(row, Loc.Get("controls.keyboard"), HeadingSize).color = Ui.Wisp;
            Cell(row, Loc.Get("controls.controller"), HeadingSize).color = Ui.Wisp;
        }

        void Section(string key)
        {
            TextMeshProUGUI label = Ui.Label(Loc.Get(key), column, SectionSize, Inner,
                SectionHeight, TextAlignmentOptions.Left);

            label.color = Ui.Warning;
        }

        void Line(string name, string path)
        {
            InputAction action = Core.Controls.Lookup(path);

            RectTransform row = Ui.Row("Row", column, Inner, RowHeight);

            Ui.Label(name, row, RowSize, NameColumn, RowHeight, TextAlignmentOptions.Left);

            Cell(row, Bound(action, Core.Controls.KeyboardScheme), RowSize);
            Cell(row, Bound(action, Core.Controls.GamepadScheme), RowSize);
        }

        TextMeshProUGUI Cell(RectTransform row, string text, float size) =>
            Ui.Label(text, row, size, GlyphColumn, RowHeight);

        static string Bound(InputAction action, string scheme)
        {
            string glyph = Core.Controls.GlyphFor(action, scheme);

            return string.IsNullOrEmpty(glyph) ? Loc.Get("controls.none") : glyph;
        }

        float Inner => PanelWidth - PanelPadding * 2f;

        void Update()
        {
            if (Core.Controls.PausePressed || Core.Controls.CancelPressed)
                Close();
        }

        public void Close()
        {
            open = null;
            Screens.Release();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (open == this)
            {
                open = null;
                Screens.Release();
            }
        }
    }
}
