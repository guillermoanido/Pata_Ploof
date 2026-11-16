using System.Collections.Generic;
using FallingWizard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallingWizard.EditorTools
{
    public static class MenuStyler
    {
        const string BuiltInUiArt = "Resources/unity_builtin_extra";
        const string PanelSpritePath = "Assets/Art/UI Kit/Panel Parchment.png";
        const string ButtonSpritePath = "Assets/Art/UI Kit/Button Cloth.png";
        const string CardSpritePath = "Assets/Art/UI Kit/Card Cloth.png";
        const string StarSpritePath = "Assets/Art/Sprites/Health Icon.png";

        const int LargestPixelScale = 4;
        const int SlicedSides = 2;
        const float TitleFontSize = 44f;
        const float ColourTolerance = 0.06f;
        const float TintFadeSeconds = 0.08f;

        static readonly string[] Prefabs =
        {
            "Assets/Resources/Choice Screen.prefab",
            "Assets/Resources/Controls Screen.prefab",
            "Assets/Resources/Skill Screen.prefab",
            "Assets/Prefabs/Pause Menu.prefab",
        };

        static readonly string[] Scenes =
        {
            "Assets/Scenes/Main Menu.unity",
            "Assets/Scenes/Level Select.unity",
        };

        static readonly Color OldInk = new Color(0.93f, 0.92f, 0.98f);
        static readonly Color OldFadedInk = new Color(0.62f, 0.60f, 0.72f);
        static readonly Color OldAccent = new Color(0.55f, 0.85f, 1f);
        static readonly Color OldWarning = new Color(0.95f, 0.72f, 0.36f);

        static readonly Color Title = new Color(0.61f, 0.21f, 0.47f);
        static readonly Color Wood = new Color(0.63f, 0.36f, 0.18f);
        static readonly Color Shroud = new Color(0.31f, 0.12f, 0.23f, 0.72f);

        static readonly Color Highlighted = new Color(1f, 0.94f, 0.86f);
        static readonly Color Selected = new Color(1f, 0.9f, 0.78f);
        static readonly Color Pressed = new Color(0.86f, 0.76f, 0.78f);
        static readonly Color Disabled = new Color(0.62f, 0.55f, 0.6f, 0.7f);

        static Sprite panelSprite;
        static Sprite buttonSprite;
        static Sprite cardSprite;
        static Sprite starSprite;

        static int changes;

        [MenuItem("Pata Ploof/Apply Storybook UI Style")]
        static void ApplyToEveryMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !TryLoadSprites())
                return;

            var report = new List<string>();

            foreach (string path in Prefabs)
                report.Add($"{path}: {StylePrefab(path)}");

            foreach (string path in Scenes)
                report.Add($"{path}: {StyleScene(path)}");

            AssetDatabase.SaveAssets();
            Debug.Log("Storybook UI style applied. Changes made -\n" + string.Join("\n", report));
        }

        static bool TryLoadSprites()
        {
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ButtonSpritePath);
            cardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CardSpritePath);
            starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);

            if (panelSprite != null && buttonSprite != null && cardSprite != null)
                return true;

            Debug.LogError("The UI Kit sprites are missing from Assets/Art/UI Kit, so nothing was styled.");
            return false;
        }

        static int StylePrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return 0;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            int made = Style(root);

            if (made > 0)
                PrefabUtility.SaveAsPrefabAsset(root, path);

            PrefabUtility.UnloadPrefabContents(root);
            return made;
        }

        static int StyleScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.isLoaded;

            if (!wasOpen)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            int made = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
                made += Style(root);

            if (made > 0)
                EditorSceneManager.SaveScene(scene);

            if (!wasOpen)
                EditorSceneManager.CloseScene(scene, true);

            return made;
        }

        static int Style(GameObject root)
        {
            changes = 0;

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                StyleImage(image);

            foreach (Selectable control in root.GetComponentsInChildren<Selectable>(true))
                StyleTints(control);

            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                StyleLabel(label);

            return changes;
        }

        static void StyleImage(Image image)
        {
            if (image.name == "Shroud")
            {
                Paint(image, Shroud);
                return;
            }

            if (!IsPlaceholderArt(image) || image.GetComponent<Mask>() != null)
                return;

            Sprite sprite = PickSprite(image);

            if (sprite == starSprite && sprite != null)
                PutStar(image);
            else if (sprite != null)
                PutSliced(image, sprite);
        }

        static bool IsPlaceholderArt(Image image) =>
            image.sprite == null || AssetDatabase.GetAssetPath(image.sprite) == BuiltInUiArt;

        static Sprite PickSprite(Image image)
        {
            string name = image.name;
            bool onSlider = image.GetComponentInParent<Slider>(true) != null;
            bool onToggle = image.GetComponentInParent<Toggle>(true) != null;

            if (name == "Checkmark" || name == "Item Checkmark" || (name == "Handle" && onSlider))
                return starSprite;

            if (name == "Fill" || name == "Handle")
                return buttonSprite;

            if ((name == "Background" && (onSlider || onToggle)) || name == "Item Background" ||
                name.Contains("Card") || name.Contains("Plate") || image.GetComponent<Scrollbar>() != null)
                return cardSprite;

            if (image.GetComponent<Selectable>() != null)
                return buttonSprite;

            if (name == "Template" || name.Contains("Panel") || image.GetComponent<LayoutGroup>() != null)
                return panelSprite;

            return null;
        }

        static void PutSliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = FindPixelsPerUnitMultiplier(image, sprite);
            Paint(image, Color.white);
        }

        static void PutStar(Image image)
        {
            image.sprite = starSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            Paint(image, Color.white);
        }

        static float FindPixelsPerUnitMultiplier(Image image, Sprite sprite)
        {
            var scaler = image.GetComponentInParent<CanvasScaler>(true);
            float referencePixelsPerUnit = scaler != null ? scaler.referencePixelsPerUnit : sprite.pixelsPerUnit;

            Rect box = ((RectTransform)image.transform).rect;
            float border = Mathf.Max(sprite.border.x, sprite.border.w, 1f);
            float shortestSide = Mathf.Min(box.width, box.height);

            int pixelScale = Mathf.Clamp(Mathf.FloorToInt(shortestSide / (border * SlicedSides)), 1,
                LargestPixelScale);

            return referencePixelsPerUnit / sprite.pixelsPerUnit / pixelScale;
        }

        static void StyleTints(Selectable control)
        {
            if (control.transition != Selectable.Transition.ColorTint)
                return;

            ColorBlock tints = control.colors;
            tints.colorMultiplier = 1f;
            tints.normalColor = Color.white;
            tints.highlightedColor = Highlighted;
            tints.selectedColor = Selected;
            tints.pressedColor = Pressed;
            tints.disabledColor = Disabled;
            tints.fadeDuration = TintFadeSeconds;

            control.colors = tints;
            changes++;
        }

        static void StyleLabel(TMP_Text label)
        {
            bool isFaded = IsNear(label.color, OldFadedInk);

            if (IsOnCloth(label))
                Paint(label, isFaded ? Ui.FadedClothInk : Ui.ClothInk);
            else if (IsOnParchment(label))
                Paint(label, PickParchmentInk(label, isFaded));
        }

        static Color PickParchmentInk(TMP_Text label, bool isFaded)
        {
            if (isFaded)
                return Ui.FadedInk;

            if (IsNear(label.color, OldAccent))
                return Wood;

            if (IsNear(label.color, OldWarning))
                return Ui.Warning;

            return label.fontSize >= TitleFontSize ? Title : Ui.Ink;
        }

        static bool IsOnCloth(TMP_Text label)
        {
            for (Transform step = label.transform.parent; step != null; step = step.parent)
            {
                var image = step.GetComponent<Image>();

                if (image != null && (image.sprite == buttonSprite || image.sprite == cardSprite))
                    return true;

                if (image != null && image.sprite == panelSprite)
                    return false;
            }

            return false;
        }

        static bool IsOnParchment(TMP_Text label)
        {
            for (Transform step = label.transform.parent; step != null; step = step.parent)
            {
                var image = step.GetComponent<Image>();

                if (image != null && image.sprite == panelSprite)
                    return true;
            }

            return false;
        }

        static bool IsNear(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < ColourTolerance && Mathf.Abs(a.g - b.g) < ColourTolerance &&
            Mathf.Abs(a.b - b.b) < ColourTolerance;

        static void Paint(Graphic graphic, Color colour)
        {
            graphic.color = colour;
            changes++;
        }
    }
}
