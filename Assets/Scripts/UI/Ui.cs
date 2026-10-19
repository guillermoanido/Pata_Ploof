using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public static class Ui
    {
        const float SheetPadding = 36f;
        const float SheetSpacing = 16f;
        const float ColumnSpacing = 18f;
        const float RowSpacing = 16f;
        const float PipSpacing = 4f;
        const float ButtonFontSize = 30f;

        const float ScalerBalance = 0.5f;

        const float TintFadeSeconds = 0.08f;
        const float CardTintMultiplier = 2f;

        public static readonly Color Ink = new Color(0.93f, 0.92f, 0.98f);
        public static readonly Color FadedInk = new Color(0.62f, 0.60f, 0.72f);

        public static readonly Color Panel = new Color(0.07f, 0.06f, 0.11f, 0.97f);
        public static readonly Color Card = new Color(0.13f, 0.12f, 0.19f, 0.95f);
        public static readonly Color CardLit = new Color(0.20f, 0.19f, 0.30f, 0.98f);

        public static readonly Color Accent = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Warning = new Color(0.95f, 0.72f, 0.36f);

        static readonly Color ButtonInk = new Color(0.11f, 0.10f, 0.14f);
        static readonly Color Shade = new Color(0.02f, 0.02f, 0.04f, 0.78f);
        static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.16f);

        static readonly Color ButtonTintNormal = new Color(1f, 1f, 1f, 1f);
        static readonly Color ButtonTintHighlighted = new Color(0.84f, 0.90f, 1f, 1f);
        static readonly Color ButtonTintSelected = new Color(0.72f, 0.84f, 1f, 1f);
        static readonly Color ButtonTintPressed = new Color(0.60f, 0.72f, 0.95f, 1f);
        static readonly Color ButtonTintDisabled = new Color(1f, 1f, 1f, 0.35f);

        static readonly Color CardTintNormal = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        static readonly Color CardTintHighlighted = new Color(0.62f, 0.62f, 0.7f, 0.5f);
        static readonly Color CardTintSelected = new Color(0.78f, 0.78f, 0.92f, 0.52f);
        static readonly Color CardTintPressed = new Color(0.9f, 0.9f, 1f, 0.55f);
        static readonly Color CardTintDisabled = new Color(0.4f, 0.4f, 0.45f, 0.4f);

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        static readonly TMP_DefaultControls.Resources PlainResources =
            new TMP_DefaultControls.Resources();

        static ColorBlock ButtonTints
        {
            get
            {
                ColorBlock block = ColorBlock.defaultColorBlock;
                block.normalColor = ButtonTintNormal;
                block.highlightedColor = ButtonTintHighlighted;
                block.selectedColor = ButtonTintSelected;
                block.pressedColor = ButtonTintPressed;
                block.disabledColor = ButtonTintDisabled;
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
                block.normalColor = CardTintNormal;
                block.highlightedColor = CardTintHighlighted;
                block.selectedColor = CardTintSelected;
                block.pressedColor = CardTintPressed;
                block.disabledColor = CardTintDisabled;
                block.fadeDuration = TintFadeSeconds;
                return block;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScalerBalance;

            EnsureEventSystem();

            return canvas;
        }

        public static Image CreateShroud(Transform parent)
        {
            var shroudObject = new GameObject("Shroud", typeof(Image));
            Attach(shroudObject, parent);
            Stretch(shroudObject);

            Image shroud = shroudObject.GetComponent<Image>();
            shroud.color = Shade;
            return shroud;
        }

        public static RectTransform CreateSheet(string name, Transform parent, Color colour,
            float width, float padding = SheetPadding, float spacing = SheetSpacing)
        {
            var sheetObject = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            Attach(sheetObject, parent);

            sheetObject.GetComponent<Image>().color = colour;

            var rectTransform = (RectTransform)sheetObject.transform;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = Centre;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(width, 0f);

            var layout = sheetObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            SizeChildrenWithoutStretching(layout);

            var fitter = sheetObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rectTransform;
        }

        public static RectTransform CreateColumn(string name, Transform parent, float width,
            float spacing = ColumnSpacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var columnObject = new GameObject(name, typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            Attach(columnObject, parent);

            var layout = columnObject.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            SizeChildrenWithoutStretching(layout);

            var rectTransform = (RectTransform)columnObject.transform;
            rectTransform.sizeDelta = new Vector2(width, 0f);

            return rectTransform;
        }

        public static RectTransform CreateRow(string name, Transform parent, float width,
            float height, float spacing = RowSpacing, TextAnchor align = TextAnchor.MiddleLeft)
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

        public static Image CreatePlate(string name, Transform parent, Color colour, float width,
            float height, bool clickable = true)
        {
            var plateObject = new GameObject(name, typeof(Image));
            Attach(plateObject, parent);
            SetSize(plateObject, width, height);

            Image plate = plateObject.GetComponent<Image>();
            plate.color = colour;
            plate.raycastTarget = clickable;
            return plate;
        }

        public static Button MakePressable(Image plate)
        {
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CardTints;
            return button;
        }

        public static void Focus(GameObject target)
        {
            if (EventSystem.current == null || target == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }

        public static RectTransform CreatePips(Transform parent, int filled, int total,
            float size, Color lit)
        {
            RectTransform strip = CreateRow("Pips", parent,
                (size + PipSpacing) * Mathf.Max(1, total), size, PipSpacing);

            for (int i = 0; i < total; i++)
                CreatePlate($"Pip {i + 1}", strip, i < filled ? lit : PipEmpty, size, size,
                    clickable: false);

            return strip;
        }

        public static Image CreateIcon(Transform parent, Sprite sprite, float size, Color tint)
        {
            var iconObject = new GameObject("Icon", typeof(Image));
            Attach(iconObject, parent);
            SetSize(iconObject, size, size);

            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.color = tint;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        public static TextMeshProUGUI CreateLabel(string text, Transform parent, float fontSize,
            float width, float height, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject labelObject = TMP_DefaultControls.CreateText(PlainResources);
            labelObject.name = "Label";
            Attach(labelObject, parent);
            SetSize(labelObject, width, height);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = Ink;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;

            return label;
        }

        public static Button CreateButton(string text, Transform parent, float width, float height,
            float fontSize = ButtonFontSize)
        {
            GameObject buttonObject = TMP_DefaultControls.CreateButton(PlainResources);
            buttonObject.name = text + " Button";
            Attach(buttonObject, parent);
            SetSize(buttonObject, width, height);

            var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = ButtonInk;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = buttonObject.GetComponent<Button>();
            button.colors = ButtonTints;

            return button;
        }

        public static void Retext(Button button, string text)
        {
            if (button == null)
                return;

            var label = button.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
                label.text = text;
        }

        public static void SetSize(GameObject target, float width, float height)
        {
            var rectTransform = (RectTransform)target.transform;
            rectTransform.sizeDelta = new Vector2(width, height);

            var element = target.GetComponent<LayoutElement>();

            if (element == null)
                element = target.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.preferredHeight = height;

            element.minWidth = width;
            element.minHeight = height;
        }

        public static void SizeChildrenWithoutStretching(HorizontalOrVerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public static void Attach(GameObject child, Transform parent) =>
            child.transform.SetParent(parent, false);

        public static void Stretch(GameObject target)
        {
            var rectTransform = (RectTransform)target.transform;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystemObject = new GameObject("Event System", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
    }
}
