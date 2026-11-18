using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public static class Ui
    {
        public static bool IsModalOpen { get; private set; }

        public static void ClaimScreen() => IsModalOpen = true;

        public static void ReleaseScreen() => IsModalOpen = false;

        public static T OpenScreen<T>(string prefabName) where T : Component
        {
            var prefab = Resources.Load<T>(prefabName);

            if (prefab == null)
            {
                Debug.LogError($"There is no '{prefabName}' prefab in a Resources folder. The " +
                               "screens are prefabs in Assets/Resources - put it back from git.");
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

    [System.Serializable]
    public class BlipRow
    {
        [Tooltip("One blip, copied once per blip needed and placed where it sits. It stays " +
                 "switched off itself, so restyle it and every copy follows.")]
        public GameObject template;

        [Tooltip("A child of the blip, shown while that blip is filled and hidden while it is " +
                 "empty. Put the filled art here and the empty art on the blip itself.")]
        public GameObject filledLook;

        readonly List<GameObject> copies = new List<GameObject>();
        readonly List<GameObject> fills = new List<GameObject>();

        public void Show(int count, int filled)
        {
            if (template == null)
                return;

            template.SetActive(false);

            if (copies.Count != count)
                Rebuild(count);

            for (int i = 0; i < fills.Count; i++)
            {
                if (fills[i] != null)
                    fills[i].SetActive(i < filled);
            }
        }

        public BlipRow CopyInto(GameObject copy, GameObject original) => new BlipRow
        {
            template = SameObjectIn(copy, original, template),
            filledLook = SameObjectIn(copy, original, filledLook),
        };

        static GameObject SameObjectIn(GameObject copy, GameObject original, GameObject part)
        {
            if (part == null)
                return null;

            Transform match = Ui.SameChildIn(copy, original, part.transform);
            return match != null ? match.gameObject : null;
        }

        void Rebuild(int count)
        {
            foreach (GameObject copy in copies)
                Object.Destroy(copy);

            copies.Clear();
            fills.Clear();

            for (int i = 0; i < count; i++)
            {
                GameObject copy = Ui.CopyOf(template);
                copies.Add(copy);
                fills.Add(SameObjectIn(copy, template, filledLook));
            }
        }
    }

    [System.Serializable]
    public class DigitRow
    {
        const int NothingShownYet = -1;

        [Tooltip("One digit: an object whose children are the faces 0 to 9, in that order, " +
                 "each holding your digit art. It is copied once per digit the number needs, " +
                 "and the copies sit where it sits. The script only switches the faces on and " +
                 "off - the same as the timed reward clock - so the art is yours to redraw. The " +
                 "template itself stays switched off while the game runs.")]
        public GameObject template;

        readonly List<GameObject> copies = new List<GameObject>();

        int shown = NothingShownYet;

        public void Show(int number)
        {
            if (template == null || number == shown)
                return;

            shown = number;
            template.SetActive(false);

            string digits = Mathf.Max(0, number).ToString();

            if (copies.Count != digits.Length)
                Rebuild(digits.Length);

            for (int i = 0; i < digits.Length; i++)
                ShowFace(copies[i], digits[i] - '0');
        }

        static void ShowFace(GameObject digit, int face)
        {
            Transform faces = digit.transform;

            for (int i = 0; i < faces.childCount; i++)
                faces.GetChild(i).gameObject.SetActive(i == face);
        }

        void Rebuild(int count)
        {
            foreach (GameObject copy in copies)
                Object.Destroy(copy);

            copies.Clear();

            for (int i = 0; i < count; i++)
                copies.Add(Ui.CopyOf(template));
        }
    }
}
