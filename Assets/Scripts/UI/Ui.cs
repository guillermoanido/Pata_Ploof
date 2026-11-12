using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.93f, 0.92f, 0.98f);
        public static readonly Color FadedInk = new Color(0.62f, 0.60f, 0.72f);

        public static readonly Color Panel = new Color(0.07f, 0.06f, 0.11f, 0.97f);
        public static readonly Color Card = new Color(0.13f, 0.12f, 0.19f, 0.95f);
        public static readonly Color CardLit = new Color(0.20f, 0.19f, 0.30f, 0.98f);

        public static readonly Color Accent = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Warning = new Color(0.95f, 0.72f, 0.36f);

        public static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.16f);

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
