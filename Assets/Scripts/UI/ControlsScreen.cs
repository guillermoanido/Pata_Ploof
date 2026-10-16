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

        const float BothSides = 2f;

        const float PanelWidth = 900f;
        const float PanelPadding = 36f;
        const float PanelSpacing = 8f;
        const float InnerWidth = PanelWidth - PanelPadding * BothSides;

        const float TitleFontSize = 52f;
        const float TitleHeight = 68f;

        const float HeadingFontSize = 24f;
        const float HeadingHeight = 38f;

        const float SectionFontSize = 26f;
        const float SectionHeight = 46f;

        const float RowFontSize = 26f;
        const float RowHeight = 42f;

        const float NameColumnWidth = 356f;
        const float GlyphColumnWidth = 226f;

        const float ButtonWidth = 260f;
        const float ButtonHeight = 62f;

        static ControlsScreen openScreen;

        RectTransform column;

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

        public static ControlsScreen Open(int sortingOrder = DefaultSortingOrder)
        {
            if (openScreen != null)
                return openScreen;

            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Controls", sortingOrder);
            openScreen = canvas.gameObject.AddComponent<ControlsScreen>();
            openScreen.Build();

            return openScreen;
        }

        void Close()
        {
            MarkClosed();
            Destroy(gameObject);
        }

        void Build()
        {
            Ui.CreateShroud(transform);

            column = Ui.CreateSheet("Panel", transform, Ui.Panel, PanelWidth, PanelPadding,
                PanelSpacing);

            Ui.CreateLabel(Loc.Get("controls.title"), column, TitleFontSize, InnerWidth,
                TitleHeight);

            DrawHeadingRow();
            DrawPlayingSection();
            DrawMenusSection();
            DrawCloseButton();
        }

        void DrawHeadingRow()
        {
            RectTransform row = Ui.CreateRow("Heading", column, InnerWidth, HeadingHeight);

            Ui.CreateLabel(string.Empty, row, HeadingFontSize, NameColumnWidth, HeadingHeight,
                TextAlignmentOptions.Left);

            DrawCell(row, Loc.Get("controls.keyboard"), HeadingFontSize).color = Ui.Wisp;
            DrawCell(row, Loc.Get("controls.controller"), HeadingFontSize).color = Ui.Wisp;
        }

        void DrawPlayingSection()
        {
            DrawSectionTitle("controls.playing");
            DrawBindingRow(Loc.Get("controls.move"), "Player/Move");
            DrawBindingRow(Loc.Get("controls.walk"), "Player/Walk");

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
                DrawBindingRow(Loc.Format("controls.spell", i + 1),
                    "Player/" + PlayerLogic.Spellbook.SlotActions[i]);

            DrawBindingRow(Loc.Get("controls.loadout"), "UI/Loadout");
            DrawBindingRow(Loc.Get("controls.pause"), "UI/Pause");
            DrawBindingRow(Loc.Get("controls.skip"), "UI/Skip");
        }

        void DrawMenusSection()
        {
            DrawSectionTitle("controls.menus");
            DrawBindingRow(Loc.Get("controls.menuMove"), "UI/Navigate");
            DrawBindingRow(Loc.Get("controls.menuPick"), "UI/Submit");
            DrawBindingRow(Loc.Get("controls.menuBack"), "UI/Cancel");
        }

        void DrawCloseButton()
        {
            Button closeButton = Ui.CreateButton(Loc.Get("controls.close"), column,
                ButtonWidth, ButtonHeight);

            closeButton.onClick.AddListener(Close);
            Ui.Focus(closeButton.gameObject);
        }

        void DrawSectionTitle(string key)
        {
            TextMeshProUGUI label = Ui.CreateLabel(Loc.Get(key), column, SectionFontSize,
                InnerWidth, SectionHeight, TextAlignmentOptions.Left);

            label.color = Ui.Warning;
        }

        void DrawBindingRow(string actionName, string path)
        {
            InputAction action = Core.Controls.Lookup(path);

            RectTransform row = Ui.CreateRow("Row", column, InnerWidth, RowHeight);

            Ui.CreateLabel(actionName, row, RowFontSize, NameColumnWidth, RowHeight,
                TextAlignmentOptions.Left);

            DrawCell(row, DescribeBinding(action, Core.Controls.KeyboardScheme), RowFontSize);
            DrawCell(row, DescribeBinding(action, Core.Controls.GamepadScheme), RowFontSize);
        }

        TextMeshProUGUI DrawCell(RectTransform row, string text, float fontSize) =>
            Ui.CreateLabel(text, row, fontSize, GlyphColumnWidth, RowHeight);

        static string DescribeBinding(InputAction action, string scheme)
        {
            string glyph = Core.Controls.GlyphFor(action, scheme);

            return string.IsNullOrEmpty(glyph) ? Loc.Get("controls.none") : glyph;
        }

        void MarkClosed()
        {
            openScreen = null;
            Screens.Release();
        }
    }
}
