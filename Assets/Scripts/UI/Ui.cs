using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.31f, 0.12f, 0.23f);
        public static readonly Color FadedInk = new Color(0.53f, 0.22f, 0.37f);

        public static readonly Color ClothInk = new Color(1f, 0.95f, 0.86f);
        public static readonly Color FadedClothInk = new Color(0.93f, 0.76f, 0.6f);

        public static readonly Color Panel = Color.white;
        public static readonly Color Card = new Color(0.82f, 0.78f, 0.82f, 1f);
        public static readonly Color CardLit = Color.white;

        public static readonly Color Accent = new Color(0.91f, 0.87f, 0.37f);
        public static readonly Color Warning = new Color(0.85f, 0.34f, 0.39f);

        public static readonly Color PipEmpty = new Color(0.53f, 0.22f, 0.37f);

        public static bool IsModalOpen { get; private set; }

        public static void ClaimScreen() => IsModalOpen = true;

        public static void ReleaseScreen() => IsModalOpen = false;

        public static T OpenScreen<T>(string prefabName) where T : Component
        {
            var prefab = Resources.Load<T>(prefabName);

            if (prefab == null)
            {
                Debug.LogError($"There is no '{prefabName}' prefab in a Resources folder. Run " +
                               "Pata Ploof > Build Menu Prefabs once to make it.");
                return null;
            }

            EnsureEventSystem();
            return Object.Instantiate(prefab);
        }

        public static GameObject CopyOf(GameObject template)
        {
            GameObject copy = Object.Instantiate(template, template.transform.parent);
            copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
            copy.SetActive(true);
            return copy;
        }

        public static T SameChildIn<T>(GameObject copy, GameObject template, T part)
            where T : Component
        {
            if (part == null)
                return null;

            if (part.gameObject == template)
                return copy.GetComponent<T>();

            Transform match = copy.transform.Find(PathBetween(template.transform, part.transform));
            return match != null ? match.GetComponent<T>() : null;
        }

        public static void Focus(GameObject target)
        {
            if (EventSystem.current == null || target == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target);
        }

        public static void Retext(Button button, string text)
        {
            if (button == null)
                return;

            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);

            if (label != null)
                label.text = text;
        }

        static string PathBetween(Transform root, Transform child)
        {
            string path = child.name;

            for (Transform step = child.parent; step != null && step != root; step = step.parent)
                path = step.name + "/" + path;

            return path;
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
