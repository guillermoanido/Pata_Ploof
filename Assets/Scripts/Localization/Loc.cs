using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Localization
{
    public enum Language
    {
        English,
        Spanish,
    }

    public static class Loc
    {
        public const string AbilityPrefix = "ability.";

        const string LanguageNamePrefix = "language.";

        const string EnglishCode = "en";
        const string SpanishCode = "es";

        public static event Action Changed;

        static readonly HashSet<string> Warned = new HashSet<string>();

        public static Language Language { get; private set; } = Language.English;

        public static string NameOf(Language language) =>
            TextBook.TryFind(LanguageNamePrefix + language.ToString().ToLowerInvariant(),
                             language, out string name)
                ? name
                : language.ToString();

        public static void Set(Language language)
        {
            if (Language == language)
                return;

            Language = language;
            Warned.Clear();

            GameSettings.Language = CodeFor(language);
            GameSettings.Save();

            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (Find(key, out string text))
                return text;

            Warn(key, $"Nothing in {TextBook.AssetPath} is keyed '{key}', so the key itself is " +
                      "being shown. Either it is misspelt where it is asked for, or it needs a " +
                      "line of its own in that file.");

            return key;
        }

        public static string Text(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key) && Find(key, out string text))
                return text;

            return fallback;
        }

        public static string Format(string key, params object[] values)
        {
            string pattern = Get(key);
            string filled = Fill(key, pattern, values);

            if (filled != null)
                return filled;

            if (!TextBook.TryFind(key, Language.English, out string english))
                return pattern;

            return Fill(key, english, values) ?? english;
        }

        static bool Find(string key, out string text) =>
            TextBook.TryFind(key, Language, out text) ||
            TextBook.TryFind(key, Language.English, out text);

        static string Fill(string key, string pattern, object[] values)
        {
            try
            {
                return string.Format(pattern, values);
            }
            catch (FormatException)
            {
                Warn(key, $"'{key}' is written with a placeholder the game does not fill in: " +
                          $"\"{pattern}\". It is given {values.Length} value(s), so the highest " +
                          $"number it may use is {{{values.Length - 1}}}.");
                return null;
            }
        }

        static void Warn(string key, string message)
        {
            if (Warned.Add(key))
                Debug.LogWarning(message);
        }

        static string CodeFor(Language language) =>
            language == Language.Spanish ? SpanishCode : EnglishCode;

        static Language FromCode(string code) =>
            code == SpanishCode ? Language.Spanish : Language.English;

        static Language FromSystem() =>
            Application.systemLanguage == SystemLanguage.Spanish
                ? Language.Spanish
                : Language.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            if (!GameSettings.Loaded)
                GameSettings.Load();

            string saved = GameSettings.Language;

            Language = string.IsNullOrEmpty(saved) ? FromSystem() : FromCode(saved);

            TextBook.Load();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Changed = null;
            Warned.Clear();
        }

        public static class Keys
        {
            public const string SettingsResetSave = "settings.resetSave";
            public const string SettingsResetSaveTitle = "settings.resetSave.title";
            public const string SettingsResetSaveBlurb = "settings.resetSave.blurb";
            public const string SettingsResetSaveStatus = "settings.resetSave.status";
            public const string SettingsResetSaveConfirm = "settings.resetSave.confirm";
            public const string SettingsResetSaveCancel = "settings.resetSave.cancel";

            public const string SkillTitle = "skill.title";
            public const string SkillPurse = "skill.purse";
            public const string SkillPrice = "skill.price";
            public const string SkillNoBook = "skill.noBook";
            public const string SkillDive = "skill.dive";
            public const string SkillBack = "skill.back";
            public const string SkillHintPick = "skill.hint.pick";
            public const string SkillHintMove = "skill.hint.move";
            public const string SkillHintLocked = "skill.hint.locked";
            public const string SkillOn = "skill.on";
            public const string SkillBench = "skill.bench";
            public const string SkillNext = "skill.next";
            public const string SkillLearn = "skill.learn";
            public const string SkillMastered = "skill.mastered";
            public const string SkillLearned = "skill.learned";

            public const string DeathTitle = "death.title";
            public const string DeathBlurb = "death.blurb";
            public const string DeathStatus = "death.status";
            public const string DeathContinue = "death.continue";
            public const string DeathGiveUp = "death.giveUp";

            public const string RestTitle = "rest.title";
            public const string RestBlurb = "rest.blurb";
            public const string RestStatus = "rest.status";
            public const string RestPressOn = "rest.pressOn";
            public const string RestTurnBack = "rest.turnBack";

            public const string HudWisps = "hud.wisps";
        }
    }
}
