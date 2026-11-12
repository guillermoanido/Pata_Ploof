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

        static readonly HashSet<string> WarnedKeys = new HashSet<string>();

        public static event Action Changed;

        public static Language Language { get; private set; } = Language.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            Changed = null;
            WarnedKeys.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            if (!GameSettings.IsLoaded)
                GameSettings.Load();

            string saved = GameSettings.Language;

            Language = string.IsNullOrEmpty(saved) ? ReadSystemLanguage() : ParseLanguageCode(saved);

            TextBook.Load();
        }

        public static string NameOf(Language language)
        {
            string key = LanguageNamePrefix + language.ToString().ToLowerInvariant();

            if (TextBook.TryFind(key, language, out string name))
                return name;

            return language.ToString();
        }

        public static void Set(Language language)
        {
            if (Language == language)
                return;

            Language = language;
            WarnedKeys.Clear();

            GameSettings.Language = GetLanguageCode(language);
            GameSettings.Save();

            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (TryFind(key, out string text))
                return text;

            Warn(key, $"Nothing in {TextBook.AssetPath} is keyed '{key}', so the key itself is " +
                      "being shown. Either it is misspelt where it is asked for, or it needs a " +
                      "line of its own in that file.");

            return key;
        }

        public static string Text(string key, string fallback)
        {
            if (!string.IsNullOrEmpty(key) && TryFind(key, out string text))
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

        static Language ReadSystemLanguage() =>
            Application.systemLanguage == SystemLanguage.Spanish
                ? Language.Spanish
                : Language.English;

        static Language ParseLanguageCode(string code) =>
            code == SpanishCode ? Language.Spanish : Language.English;

        static string GetLanguageCode(Language language) =>
            language == Language.Spanish ? SpanishCode : EnglishCode;

        static bool TryFind(string key, out string text) =>
            TextBook.TryFind(key, Language, out text) ||
            TextBook.TryFind(key, Language.English, out text);

        static void Warn(string key, string message)
        {
            if (WarnedKeys.Add(key))
                Debug.LogWarning(message);
        }

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

        public static class Keys
        {
            public const string SettingsControls = "settings.controls";
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

            public const string HudScrap = "hud.scrap";

            public const string DeathTitle = "death.title";
            public const string DeathBlurb = "death.blurb";
            public const string DeathStartAgain = "death.startAgain";
            public const string DeathMainMenu = "death.mainMenu";
        }
    }
}
