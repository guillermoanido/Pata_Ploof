using System.Collections.Generic;
using FallingWizard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.UI
{
    public class FontSwitcher : MonoBehaviour
    {
        static readonly Dictionary<TMP_Text, Authored> AuthoredLooks = new Dictionary<TMP_Text, Authored>();
        static readonly List<TMP_Text> Waiting = new List<TMP_Text>();
        static readonly List<TMP_Text> Gone = new List<TMP_Text>();

        static FontSwitcher instance;
        static GameFonts fonts;

        static TMP_FontAsset Chosen =>
            fonts == null ? null : GameSettings.UsesPixelFont ? fonts.pixelFont : fonts.plainFont;

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            GameSettings.FontChanged += RestyleEverything;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        void LateUpdate()
        {
            foreach (TMP_Text text in Waiting)
                Restyle(text);

            Waiting.Clear();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            GameSettings.FontChanged -= RestyleEverything;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        }

        public static void Restyle(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                Restyle(text);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            fonts = Resources.Load<GameFonts>(GameFonts.ResourcePath);

            if (fonts == null)
                Debug.LogWarning($"There is no '{GameFonts.ResourcePath}' asset in a Resources " +
                                 "folder, so the Pixel Font setting does nothing. Put it back from git.");

            var holder = new GameObject("Font Switcher", typeof(FontSwitcher));
            DontDestroyOnLoad(holder);
            instance = holder.GetComponent<FontSwitcher>();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ForgetDestroyedTexts();
            RestyleEverything();
        }

        static void RestyleEverything()
        {
            foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Restyle(text);
        }

        static void OnTextChanged(Object changed)
        {
            if (changed is TMP_Text text && Chosen != null && text.font != Chosen && !Waiting.Contains(text))
                Waiting.Add(text);
        }

        static void Restyle(TMP_Text text)
        {
            TMP_FontAsset chosen = Chosen;

            if (text == null || chosen == null || text.font == chosen)
                return;

            Authored authored = Remember(text);
            bool isAuthoredFont = authored.font == chosen;
            float scale = isAuthoredFont ? 1f : CapHeightOf(authored.font) / CapHeightOf(chosen);

            text.font = chosen;
            text.fontSharedMaterial = isAuthoredFont ? authored.material : chosen.material;
            text.fontSize = authored.size * scale;
            text.fontSizeMin = authored.sizeMin * scale;
            text.fontSizeMax = authored.sizeMax * scale;
        }

        static Authored Remember(TMP_Text text)
        {
            if (AuthoredLooks.TryGetValue(text, out Authored look))
                return look;

            look = new Authored
            {
                font = text.font,
                material = text.fontSharedMaterial,
                size = text.fontSize,
                sizeMin = text.fontSizeMin,
                sizeMax = text.fontSizeMax,
            };

            AuthoredLooks[text] = look;
            return look;
        }

        static float CapHeightOf(TMP_FontAsset font)
        {
            if (font == null || font.faceInfo.pointSize <= 0f)
                return 1f;

            float capLine = font.faceInfo.capLine > 0f ? font.faceInfo.capLine : font.faceInfo.ascentLine;
            return capLine / font.faceInfo.pointSize * font.faceInfo.scale;
        }

        static void ForgetDestroyedTexts()
        {
            Gone.Clear();

            foreach (TMP_Text text in AuthoredLooks.Keys)
                if (text == null)
                    Gone.Add(text);

            foreach (TMP_Text text in Gone)
                AuthoredLooks.Remove(text);
        }

        struct Authored
        {
            public TMP_FontAsset font;
            public Material material;
            public float size;
            public float sizeMin;
            public float sizeMax;
        }
    }
}
