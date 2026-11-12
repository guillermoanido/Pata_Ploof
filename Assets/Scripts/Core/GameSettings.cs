using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    public static class GameSettings
    {
        const string FileName = "settings.json";

        const int FormatVersion = 1;

        const string LegacyVolumeKey = "settings.volume";
        const string LegacyFullscreenKey = "settings.fullscreen";
        const string LegacyResolutionWidthKey = "settings.resolution.width";
        const string LegacyResolutionHeightKey = "settings.resolution.height";

        static float volume = 1f;
        static bool isFullscreen = true;
        static int resolutionIndex;
        static string language = string.Empty;
        static List<Resolution> resolutions;

        static bool isSettingsFileUnreadable;
        static bool hasWarnedAboutUnreadable;

        public static bool IsLoaded { get; private set; }

        public static bool DisplaySettingsSupported =>
            Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer;

        public static IReadOnlyList<Resolution> Resolutions
        {
            get
            {
                if (resolutions == null)
                    resolutions = BuildResolutionList();

                return resolutions;
            }
        }

        public static float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                AudioListener.volume = volume;
            }
        }

        public static bool IsFullscreen
        {
            get => isFullscreen;
            set
            {
                isFullscreen = value;
                ApplyResolution();
            }
        }

        public static int ResolutionIndex
        {
            get => resolutionIndex;
            set
            {
                resolutionIndex = Mathf.Clamp(value, 0, Resolutions.Count - 1);
                ApplyResolution();
            }
        }

        public static string Language
        {
            get => language;
            set => language = value ?? string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            isSettingsFileUnreadable = false;
            hasWarnedAboutUnreadable = false;

            SettingsData data = ReadStoredSettings();

            if (data == null)
                data = MakeDefaultSettings();

            ApplySettings(data);

            IsLoaded = true;
        }

        public static void Save()
        {
            if (isSettingsFileUnreadable)
            {
                WarnOnceAboutNotSaving();
                return;
            }

            Resolution chosen = Resolutions[resolutionIndex];

            SaveFile.Write(FileName, new SettingsData
            {
                version = FormatVersion,
                volume = volume,
                fullscreen = isFullscreen,
                resolutionWidth = chosen.width,
                resolutionHeight = chosen.height,
                language = language,
            });
        }

        static SettingsData ReadStoredSettings()
        {
            switch (SaveFile.Read(FileName, out SettingsData stored))
            {
                case SaveRead.Loaded:
                    return stored;

                case SaveRead.Unreadable:
                    isSettingsFileUnreadable = true;
                    return null;

                case SaveRead.Missing:
                    return ImportLegacyPlayerPrefs();
            }

            return null;
        }

        static SettingsData ImportLegacyPlayerPrefs()
        {
            if (!HasLegacyPlayerPrefs())
                return null;

            SettingsData data = ReadLegacyPlayerPrefs();

            if (SaveFile.Write(FileName, data))
                DeleteLegacyPlayerPrefs();

            return data;
        }

        static bool HasLegacyPlayerPrefs() =>
            PlayerPrefs.HasKey(LegacyVolumeKey) || PlayerPrefs.HasKey(LegacyFullscreenKey) ||
            PlayerPrefs.HasKey(LegacyResolutionWidthKey);

        static SettingsData ReadLegacyPlayerPrefs()
        {
            return new SettingsData
            {
                version = FormatVersion,
                volume = PlayerPrefs.GetFloat(LegacyVolumeKey, 1f),
                fullscreen = PlayerPrefs.GetInt(LegacyFullscreenKey, Screen.fullScreen ? 1 : 0) == 1,
                resolutionWidth = PlayerPrefs.GetInt(LegacyResolutionWidthKey, Screen.width),
                resolutionHeight = PlayerPrefs.GetInt(LegacyResolutionHeightKey, Screen.height),
                language = string.Empty,
            };
        }

        static void DeleteLegacyPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(LegacyVolumeKey);
            PlayerPrefs.DeleteKey(LegacyFullscreenKey);
            PlayerPrefs.DeleteKey(LegacyResolutionWidthKey);
            PlayerPrefs.DeleteKey(LegacyResolutionHeightKey);
            PlayerPrefs.Save();
        }

        static SettingsData MakeDefaultSettings()
        {
            return new SettingsData
            {
                version = FormatVersion,
                volume = 1f,
                fullscreen = Screen.fullScreen,
                resolutionWidth = Screen.width,
                resolutionHeight = Screen.height,
            };
        }

        static void ApplySettings(SettingsData data)
        {
            Volume = data.volume;
            isFullscreen = data.fullscreen;
            Language = data.language;

            resolutionIndex = FindResolutionIndex(
                data.resolutionWidth > 0 ? data.resolutionWidth : Screen.width,
                data.resolutionHeight > 0 ? data.resolutionHeight : Screen.height);

            ApplyResolution();
        }

        static void WarnOnceAboutNotSaving()
        {
            if (hasWarnedAboutUnreadable)
                return;

            hasWarnedAboutUnreadable = true;
            Debug.LogWarning($"Not saving settings: the file at {SaveFile.PathFor(FileName)} " +
                             "could not be read when the game started, and writing over it " +
                             "would throw away whatever is in there. Changes made this " +
                             "session will not be kept.");
        }

        static void ApplyResolution()
        {
            if (!DisplaySettingsSupported)
                return;

            Resolution chosen = Resolutions[resolutionIndex];
            FullScreenMode mode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(chosen.width, chosen.height, mode, chosen.refreshRateRatio);
        }

        static int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < Resolutions.Count; i++)
                if (Resolutions[i].width == width && Resolutions[i].height == height)
                    return i;

            return Resolutions.Count - 1;
        }

        static List<Resolution> BuildResolutionList()
        {
            var fastestPerSize = new Dictionary<Vector2Int, Resolution>();

            foreach (Resolution option in Screen.resolutions)
            {
                var size = new Vector2Int(option.width, option.height);

                if (!fastestPerSize.TryGetValue(size, out Resolution kept) ||
                    option.refreshRateRatio.value > kept.refreshRateRatio.value)
                    fastestPerSize[size] = option;
            }

            if (fastestPerSize.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                fastestPerSize[new Vector2Int(current.width, current.height)] = current;
            }

            var list = new List<Resolution>(fastestPerSize.Values);
            list.Sort(CompareBySize);
            return list;
        }

        static int CompareBySize(Resolution first, Resolution second)
        {
            if (first.width != second.width)
                return first.width.CompareTo(second.width);

            return first.height.CompareTo(second.height);
        }

        [Serializable]
        class SettingsData
        {
            public int version;
            public float volume = 1f;
            public bool fullscreen = true;
            public int resolutionWidth;
            public int resolutionHeight;
            public string language = string.Empty;
        }
    }
}
