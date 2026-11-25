using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Localization
{
    public static class TextBook
    {
        public const string AssetPath = "Assets/Resources/" + GameText.ResourcePath + ".asset";

        static readonly Dictionary<string, GameText.Line> Lines = new Dictionary<string, GameText.Line>();

        static bool isLoaded;

        public static void Load()
        {
            Lines.Clear();
            isLoaded = true;

            var book = Resources.Load<GameText>(GameText.ResourcePath);

            if (book == null)
            {
                Debug.LogWarning($"There is no Game Text at {AssetPath}, so every label shows " +
                                 "whatever is typed into it and no translation is possible. " +
                                 "Put it back from git.");
                return;
            }

            foreach (GameText.Line line in book.lines)
                Add(line);
        }

        public static bool TryFind(string key, Language language, out string text)
        {
            text = null;

            if (string.IsNullOrEmpty(key))
                return false;

            if (!isLoaded)
                Load();

            if (!Lines.TryGetValue(key, out GameText.Line line))
                return false;

            text = language == Language.Spanish ? line.spanish : line.english;
            return !string.IsNullOrEmpty(text);
        }

        static void Add(GameText.Line line)
        {
            if (line == null || string.IsNullOrEmpty(line.key))
                return;

            if (Lines.ContainsKey(line.key))
                Debug.LogWarning($"Two lines in {AssetPath} are keyed '{line.key}'. The first one " +
                                 "is used; delete or rename the other.");
            else
                Lines.Add(line.key, line);
        }
    }
}
