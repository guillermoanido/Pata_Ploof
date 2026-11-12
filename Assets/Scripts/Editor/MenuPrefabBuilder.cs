using System.Collections.Generic;
using FallingWizard.Localization;
using FallingWizard.Menus;
using FallingWizard.Player;
using FallingWizard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallingWizard.EditorTools
{
    public static class MenuPrefabBuilder
    {
        const string ResourcesFolder = "Assets/Resources";
        const string ChoicePath = ResourcesFolder + "/Choice Screen.prefab";
        const string ControlsPath = ResourcesFolder + "/Controls Screen.prefab";
        const string SkillPath = ResourcesFolder + "/Skill Screen.prefab";
        const string HudPath = "Assets/Prefabs/HUD.prefab";
        const string PauseMenuPath = "Assets/Prefabs/Pause Menu.prefab";
        const string MainMenuPath = "Assets/Scenes/Main Menu.unity";
        const string HighlightSpritePath = "Assets/Art/Placeholders/Menu Highlight.png";

        const int ChoiceSortingOrder = 200;
        const int SkillSortingOrder = 220;
        const int ControlsSortingOrder = 260;

        const float BothSides = 2f;
        const float HighlightPadding = 10f;
        const float HighlightPixelScale = 0.25f;
        const float ScalerBalance = 0.5f;
        const float TintFadeSeconds = 0.08f;
        const float CardTintMultiplier = 2f;
        const float PipSpacing = 4f;

        const float ChoiceWidth = 760f;
        const float ChoicePadding = 40f;
        const float ChoiceSpacing = 16f;
        const float ChoiceInner = ChoiceWidth - ChoicePadding * BothSides;

        const float ControlsWidth = 900f;
        const float ControlsPadding = 36f;
        const float ControlsSpacing = 8f;
        const float ControlsInner = ControlsWidth - ControlsPadding * BothSides;
        const float NameColumnWidth = 356f;
        const float GlyphColumnWidth = 226f;

        const float SkillWidth = 1180f;
        const float SkillPadding = 24f;
        const float SkillSpacing = 8f;
        const float SkillInner = SkillWidth - SkillPadding * BothSides;
        const float SlotSize = 92f;
        const float SlotCaptionHeight = 30f;
        const float SlotSpacing = 16f;
        const float CellSpacing = 4f;
        const float CardHeight = 84f;
        const float CardHorizontalPadding = 18f;
        const float CardVerticalPadding = 8f;
        const float CardSpacing = 20f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float ActionHeight = 46f;
        const float PipSize = 12f;
        const float RoomForPips = 90f;
        const float WordSpacing = 2f;
        const float HeadingSpacing = 10f;
        const float WordsWidth = SkillInner - (IconSize + ActionWidth +
                                 CardHorizontalPadding * BothSides + CardSpacing * BothSides);
        const float WordsHeight = CardHeight - CardVerticalPadding * BothSides;

        const float SettingsButtonWidth = 260f;
        const float SettingsButtonHeight = 56f;

        const float ScrapFontSize = 26f;

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        static readonly Vector2 ScrapCorner = new Vector2(-24f, -20f);
        static readonly Vector2 ScrapSize = new Vector2(360f, 40f);

        static readonly Color ButtonInk = new Color(0.11f, 0.10f, 0.14f);
        static readonly Color Shade = new Color(0.02f, 0.02f, 0.04f, 0.78f);

        static readonly TMP_DefaultControls.Resources PlainResources =
            new TMP_DefaultControls.Resources();

        static ColorBlock ButtonTints
        {
            get
            {
                ColorBlock block = ColorBlock.defaultColorBlock;
                block.highlightedColor = new Color(0.84f, 0.90f, 1f, 1f);
                block.selectedColor = new Color(0.72f, 0.84f, 1f, 1f);
                block.pressedColor = new Color(0.60f, 0.72f, 0.95f, 1f);
                block.disabledColor = new Color(1f, 1f, 1f, 0.35f);
                block.fadeDuration = TintFadeSeconds;
                return block;
            }
        }

        static ColorBlock CardTints
        {
            get
            {
                ColorBlock block = ColorBlock.defaultColorBlock;
                block.colorMultiplier = CardTintMultiplier;
                block.normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                block.highlightedColor = new Color(0.62f, 0.62f, 0.7f, 0.5f);
                block.selectedColor = new Color(0.78f, 0.78f, 0.92f, 0.52f);
                block.pressedColor = new Color(0.9f, 0.9f, 1f, 0.55f);
                block.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.4f);
                block.fadeDuration = TintFadeSeconds;
                return block;
            }
        }

        [MenuItem("Pata Ploof/Build Menu Prefabs")]
        static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var made = new List<string>();

            BuildIfMissing(ChoicePath, BuildChoiceScreen, made);
            BuildIfMissing(ControlsPath, BuildControlsScreen, made);
            BuildIfMissing(SkillPath, BuildSkillScreen, made);

            if (AddScrapLabelToHud())
                made.Add("Scrap Count on the HUD");

            if (AddSettingsButtonsToPauseMenu())
                made.Add("Controls and Reset Save buttons on the Pause Menu");

            if (AddSettingsButtonsToMainMenu())
                made.Add("Controls and Reset Save buttons on the Main Menu");

            AssetDatabase.SaveAssets();
            Debug.Log(made.Count > 0
                ? "Built: " + string.Join(", ", made) + "."
                : "Everything was already built. Delete a screen prefab to build it again.");
        }

        static void BuildIfMissing(string path, System.Func<GameObject> build, List<string> made)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            GameObject root = build();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            made.Add(path);
        }

        static GameObject BuildChoiceScreen()
        {
            Canvas canvas = CreateCanvas("Choice Screen", ChoiceSortingOrder);
            var screen = canvas.gameObject.AddComponent<ChoiceScreen>();

            CreateShroud(canvas.transform);
            RectTransform column = CreateSheet(canvas.transform, ChoiceWidth, ChoicePadding,
                ChoiceSpacing);

            screen.title = CreateLabel("Title", column, 56f, ChoiceInner, 70f);
            screen.blurb = CreateLabel("Blurb", column, 26f, ChoiceInner, 72f);
            screen.blurb.color = Ui.FadedInk;
            screen.status = CreateLabel("Status", column, 28f, ChoiceInner, 40f);
            screen.status.color = Ui.Accent;
            screen.choiceTemplate = CreateButton("Choice", column, 640f, 68f, 28f);

            return canvas.gameObject;
        }

        static GameObject BuildControlsScreen()
        {
            Canvas canvas = CreateCanvas("Controls Screen", ControlsSortingOrder);
            var screen = canvas.gameObject.AddComponent<ControlsScreen>();

            CreateShroud(canvas.transform);
            RectTransform column = CreateSheet(canvas.transform, ControlsWidth, ControlsPadding,
                ControlsSpacing);

            Translate(CreateLabel("Title", column, 52f, ControlsInner, 68f), "controls.title");
            BuildControlsHeading(column);

            var rows = new List<ControlsScreen.Row>();
            BuildPlayingRows(column, rows);
            BuildMenuRows(column, rows);
            screen.rows = rows.ToArray();

            screen.closeButton = CreateButton("Close", column, 260f, 62f, 30f);
            Translate(screen.closeButton.GetComponentInChildren<TextMeshProUGUI>(), "controls.close");

            return canvas.gameObject;
        }

        static void BuildControlsHeading(RectTransform column)
        {
            RectTransform row = CreateRow("Heading", column, ControlsInner, 38f);
            CreateLabel("Gap", row, 24f, NameColumnWidth, 38f);

            TextMeshProUGUI keyboard = CreateLabel("Keyboard", row, 24f, GlyphColumnWidth, 38f);
            keyboard.color = Ui.Accent;
            Translate(keyboard, "controls.keyboard");

            TextMeshProUGUI controller = CreateLabel("Controller", row, 24f, GlyphColumnWidth, 38f);
            controller.color = Ui.Accent;
            Translate(controller, "controls.controller");
        }

        static void BuildPlayingRows(RectTransform column, List<ControlsScreen.Row> rows)
        {
            BuildSectionTitle(column, "controls.playing");
            rows.Add(BuildBindingRow(column, "controls.move", 0, "Player/Move"));
            rows.Add(BuildBindingRow(column, "controls.walk", 0, "Player/Walk"));

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
                rows.Add(BuildBindingRow(column, "controls.spell", i + 1,
                    "Player/" + PlayerLogic.Spellbook.SlotActions[i]));

            rows.Add(BuildBindingRow(column, "controls.loadout", 0, "UI/Loadout"));
            rows.Add(BuildBindingRow(column, "controls.pause", 0, "UI/Pause"));
            rows.Add(BuildBindingRow(column, "controls.skip", 0, "UI/Skip"));
        }

        static void BuildMenuRows(RectTransform column, List<ControlsScreen.Row> rows)
        {
            BuildSectionTitle(column, "controls.menus");
            rows.Add(BuildBindingRow(column, "controls.menuMove", 0, "UI/Navigate"));
            rows.Add(BuildBindingRow(column, "controls.menuPick", 0, "UI/Submit"));
            rows.Add(BuildBindingRow(column, "controls.menuBack", 0, "UI/Cancel"));
        }

        static void BuildSectionTitle(RectTransform column, string key)
        {
            TextMeshProUGUI label = CreateLabel("Section", column, 26f, ControlsInner, 46f,
                TextAlignmentOptions.Left);
            label.color = Ui.Warning;
            Translate(label, key);
        }

        static ControlsScreen.Row BuildBindingRow(RectTransform column, string nameKey,
            int number, string action)
        {
            RectTransform row = CreateRow(action, column, ControlsInner, 42f);

            return new ControlsScreen.Row
            {
                nameKey = nameKey,
                number = number,
                action = action,
                nameLabel = CreateLabel("Name", row, 26f, NameColumnWidth, 42f,
                    TextAlignmentOptions.Left),
                keyboardLabel = CreateLabel("Keyboard", row, 26f, GlyphColumnWidth, 42f),
                controllerLabel = CreateLabel("Controller", row, 26f, GlyphColumnWidth, 42f),
            };
        }

        static GameObject BuildSkillScreen()
        {
            Canvas canvas = CreateCanvas("Skill Screen", SkillSortingOrder);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

            CreateShroud(canvas.transform);
            RectTransform body = CreateSheet(canvas.transform, SkillWidth, SkillPadding,
                SkillSpacing);

            Translate(CreateLabel("Title", body, 44f, SkillInner, 52f), Loc.Keys.SkillTitle);

            screen.purse = CreateLabel("Purse", body, 28f, SkillInner, 36f);
            screen.purse.color = Ui.Accent;

            RectTransform strip = CreateRow("Slots", body, SkillInner,
                SlotSize + SlotCaptionHeight, SlotSpacing, TextAnchor.MiddleCenter);
            screen.slotTemplate = BuildSlotTemplate(strip);

            screen.hint = CreateLabel("Hint", body, 20f, SkillInner, 28f);
            screen.hint.color = Ui.FadedInk;

            TextMeshProUGUI notice = CreateLabel("No Book", body, 26f, SkillInner, 44f);
            notice.color = Ui.Warning;
            Translate(notice, Loc.Keys.SkillNoBook);
            screen.noBookNotice = notice.gameObject;

            screen.cardTemplate = BuildCardTemplate(body);
            screen.diveButton = CreateButton("Dive", body, 420f, 58f, 28f);

            return canvas.gameObject;
        }

        static SkillScreen.SlotParts BuildSlotTemplate(RectTransform strip)
        {
            RectTransform cell = CreateColumn("Slot", strip, SlotSize, CellSpacing,
                TextAnchor.UpperCenter);
            SetSize(cell.gameObject, SlotSize, SlotSize + SlotCaptionHeight);

            Image plate = CreatePlate("Plate", cell, Ui.Card, SlotSize, SlotSize);
            Button plateButton = MakePressable(plate);

            TextMeshProUGUI glyph = CreateLabel("Glyph", plate.transform, 34f, SlotSize, SlotSize);
            glyph.color = Ui.FadedInk;

            return new SkillScreen.SlotParts
            {
                root = cell.gameObject,
                plate = plateButton,
                icon = CreateIcon(plate.transform),
                glyph = glyph,
                caption = CreateLabel("Caption", cell, 17f, SlotSize, SlotCaptionHeight),
            };
        }

        static SkillScreen.CardParts BuildCardTemplate(RectTransform body)
        {
            Image card = CreatePlate("Card", body, Ui.Card, SkillInner, CardHeight);
            Button cardButton = MakePressable(card);
            ArrangeCard(card.gameObject);

            Image icon = CreateIcon(card.transform);

            RectTransform words = CreateColumn("Words", card.transform, WordsWidth, WordSpacing,
                TextAnchor.MiddleLeft);
            SetSize(words.gameObject, WordsWidth, WordsHeight);

            RectTransform heading = CreateRow("Heading", words, WordsWidth, 28f, HeadingSpacing);
            TextMeshProUGUI nameLabel = CreateLabel("Name", heading, 24f, WordsWidth - RoomForPips,
                28f, TextAlignmentOptions.Left);

            RectTransform pips = CreateRow("Pips", heading, RoomForPips, PipSize, PipSpacing);
            Image pip = CreatePlate("Pip", pips, Color.white, PipSize, PipSize, false);

            TextMeshProUGUI blurb = CreateLabel("Blurb", words, 19f, WordsWidth, 30f,
                TextAlignmentOptions.TopLeft);
            blurb.color = Ui.FadedInk;

            TextMeshProUGUI status = CreateLabel("Status", card.transform, 22f, ActionWidth,
                ActionHeight);
            status.color = Ui.FadedInk;

            return new SkillScreen.CardParts
            {
                root = card.gameObject,
                button = cardButton,
                icon = icon,
                nameLabel = nameLabel,
                pipTemplate = pip,
                blurb = blurb,
                action = CreateButton("Action", card.transform, ActionWidth, ActionHeight, 22f),
                status = status,
            };
        }

        static void ArrangeCard(GameObject card)
        {
            var cardLayout = card.AddComponent<HorizontalLayoutGroup>();
            cardLayout.childAlignment = TextAnchor.MiddleLeft;
            cardLayout.spacing = CardSpacing;
            cardLayout.padding = new RectOffset(
                (int)CardHorizontalPadding, (int)CardHorizontalPadding,
                (int)CardVerticalPadding, (int)CardVerticalPadding);
            SizeChildrenWithoutStretching(cardLayout);
        }

        static bool AddScrapLabelToHud()
        {
            GameObject hud = PrefabUtility.LoadPrefabContents(HudPath);
            var hudScript = hud.GetComponent<PlayerHud>();
            bool isMissing = hudScript != null && hudScript.scrapLabel == null;

            if (isMissing)
            {
                hudScript.scrapLabel = BuildScrapLabel(hud.transform);
                PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
            }

            PrefabUtility.UnloadPrefabContents(hud);
            return isMissing;
        }

        static TextMeshProUGUI BuildScrapLabel(Transform hud)
        {
            TextMeshProUGUI label = CreateLabel("Scrap Count", hud, ScrapFontSize, ScrapSize.x,
                ScrapSize.y, TextAlignmentOptions.Right);
            label.color = Ui.Accent;

            var rectTransform = (RectTransform)label.transform;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = Vector2.one;
            rectTransform.anchoredPosition = ScrapCorner;

            return label;
        }

        static bool AddSettingsButtonsToPauseMenu()
        {
            GameObject pauseMenu = PrefabUtility.LoadPrefabContents(PauseMenuPath);
            bool isChanged = AddSettingsButtons(pauseMenu);

            if (isChanged)
                PrefabUtility.SaveAsPrefabAsset(pauseMenu, PauseMenuPath);

            PrefabUtility.UnloadPrefabContents(pauseMenu);
            return isChanged;
        }

        static bool AddSettingsButtonsToMainMenu()
        {
            Scene scene = SceneManager.GetSceneByPath(MainMenuPath);
            bool wasOpen = scene.isLoaded;

            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Additive);

            bool isChanged = false;

            foreach (GameObject root in scene.GetRootGameObjects())
                isChanged |= AddSettingsButtons(root);

            if (isChanged)
                EditorSceneManager.SaveScene(scene);

            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);

            return isChanged;
        }

        static bool AddSettingsButtons(GameObject root)
        {
            bool isChanged = false;

            foreach (SettingsPanel panel in root.GetComponentsInChildren<SettingsPanel>(true))
            {
                var fields = new SerializedObject(panel);
                isChanged |= AddButtonBesideBack(fields, "controlsButton", "Controls",
                    Loc.Keys.SettingsControls);
                isChanged |= AddButtonBesideBack(fields, "resetSaveButton", "Reset Save",
                    Loc.Keys.SettingsResetSave);
                fields.ApplyModifiedPropertiesWithoutUndo();
            }

            return isChanged;
        }

        static bool AddButtonBesideBack(SerializedObject fields, string field, string name,
            string key)
        {
            SerializedProperty slot = fields.FindProperty(field);
            var back = fields.FindProperty("backButton").objectReferenceValue as Button;

            if (slot.objectReferenceValue != null || back == null)
                return false;

            Button button = CreateButton(name, back.transform.parent, SettingsButtonWidth,
                SettingsButtonHeight, 30f);
            button.transform.SetSiblingIndex(back.transform.GetSiblingIndex());
            Translate(button.GetComponentInChildren<TextMeshProUGUI>(), key);

            slot.objectReferenceValue = button;
            return true;
        }

        static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.layer = LayerMask.NameToLayer("UI");

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScalerBalance;

            return canvas;
        }

        static void CreateShroud(Transform parent)
        {
            var shroudObject = new GameObject("Shroud", typeof(Image));
            Attach(shroudObject, parent);
            Stretch(shroudObject);
            shroudObject.GetComponent<Image>().color = Shade;
        }

        static RectTransform CreateSheet(Transform parent, float width, float padding,
            float spacing)
        {
            var sheetObject = new GameObject("Panel", typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            Attach(sheetObject, parent);
            sheetObject.GetComponent<Image>().color = Ui.Panel;

            var rectTransform = (RectTransform)sheetObject.transform;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = Centre;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(width, 0f);

            var layout = sheetObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            SizeChildrenWithoutStretching(layout);

            sheetObject.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            return rectTransform;
        }

        static RectTransform CreateColumn(string name, Transform parent, float width,
            float spacing, TextAnchor align)
        {
            var columnObject = new GameObject(name, typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            Attach(columnObject, parent);

            var layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            SizeChildrenWithoutStretching(layout);

            ((RectTransform)columnObject.transform).sizeDelta = new Vector2(width, 0f);
            return (RectTransform)columnObject.transform;
        }

        static RectTransform CreateRow(string name, Transform parent, float width, float height,
            float spacing = CardSpacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rowObject = new GameObject(name, typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            Attach(rowObject, parent);
            SetSize(rowObject, width, height);

            var layout = rowObject.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            SizeChildrenWithoutStretching(layout);

            return (RectTransform)rowObject.transform;
        }

        static Image CreatePlate(string name, Transform parent, Color colour, float width,
            float height, bool isClickable = true)
        {
            var plateObject = new GameObject(name, typeof(Image));
            Attach(plateObject, parent);
            SetSize(plateObject, width, height);

            Image plate = plateObject.GetComponent<Image>();
            plate.color = colour;
            plate.raycastTarget = isClickable;
            return plate;
        }

        static Button MakePressable(Image plate)
        {
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CardTints;
            AddHighlight(button);
            return button;
        }

        static Image CreateIcon(Transform parent)
        {
            var iconObject = new GameObject("Icon", typeof(Image));
            Attach(iconObject, parent);
            SetSize(iconObject, IconSize, IconSize);

            Image icon = iconObject.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        static TextMeshProUGUI CreateLabel(string name, Transform parent, float fontSize,
            float width, float height, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject labelObject = TMP_DefaultControls.CreateText(PlainResources);
            labelObject.name = name;
            Attach(labelObject, parent);
            SetSize(labelObject, width, height);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = string.Empty;
            label.fontSize = fontSize;
            label.color = Ui.Ink;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;

            return label;
        }

        static Button CreateButton(string name, Transform parent, float width, float height,
            float fontSize)
        {
            GameObject buttonObject = TMP_DefaultControls.CreateButton(PlainResources);
            buttonObject.name = name + " Button";
            Attach(buttonObject, parent);
            SetSize(buttonObject, width, height);

            var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            label.text = name;
            label.fontSize = fontSize;
            label.color = ButtonInk;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = buttonObject.GetComponent<Button>();
            button.colors = ButtonTints;
            AddHighlight(button);

            return button;
        }

        static void AddHighlight(Selectable button)
        {
            var frameObject = new GameObject("Highlight", typeof(Image));
            Attach(frameObject, button.transform);
            Stretch(frameObject);
            ((RectTransform)frameObject.transform).sizeDelta =
                Vector2.one * HighlightPadding * BothSides;

            var frame = frameObject.GetComponent<Image>();
            frame.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HighlightSpritePath);
            frame.type = Image.Type.Sliced;
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = HighlightPixelScale;
            frame.raycastTarget = false;

            frameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            frameObject.SetActive(false);

            button.gameObject.AddComponent<SelectedLook>().look = frameObject;
        }

        static void Translate(TextMeshProUGUI label, string key)
        {
            label.text = Loc.Text(key, key);
            label.gameObject.AddComponent<LocalizedText>().key = key;
        }

        static void SetSize(GameObject target, float width, float height)
        {
            ((RectTransform)target.transform).sizeDelta = new Vector2(width, height);

            var element = target.GetComponent<LayoutElement>();

            if (element == null)
                element = target.AddComponent<LayoutElement>();

            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = element.minHeight = height;
        }

        static void SizeChildrenWithoutStretching(HorizontalOrVerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        static void Attach(GameObject child, Transform parent) =>
            child.transform.SetParent(parent, false);

        static void Stretch(GameObject target)
        {
            var rectTransform = (RectTransform)target.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
