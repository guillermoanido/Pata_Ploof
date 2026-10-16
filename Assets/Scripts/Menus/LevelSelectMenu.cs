using System;
using FallingWizard.Core;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class LevelSelectMenu : MonoBehaviour
    {
        [Tooltip("The pips on the tower map, from the top of the tower to the bottom. The first " +
                 "level is always open, and each one after it opens once the level above it has " +
                 "been completed.")]
        public Level[] levels = new Level[0];

        void Start()
        {
            GameObject deepestOpenPip = null;

            for (int i = 0; i < levels.Length; i++)
            {
                Level level = levels[i];
                bool isOpen = IsUnlocked(i) && IsInTheBuild(level);

                level.pip.interactable = isOpen;

                if (!isOpen)
                    continue;

                level.pip.onClick.AddListener(() => Game.LoadLevel(level.sceneName));
                deepestOpenPip = level.pip.gameObject;
            }

            Ui.Focus(deepestOpenPip);
        }

        bool IsUnlocked(int index) =>
            index == 0 || Progress.HasCompletedLevel(levels[index - 1].sceneName);

        static bool IsInTheBuild(Level level)
        {
            bool isBuilt = Application.CanStreamedLevelBeLoaded(level.sceneName);

            if (!isBuilt)
                Debug.LogWarning($"The '{level.sceneName}' scene is not in File > Build Settings, " +
                                 "so its pip stays locked.");

            return isBuilt;
        }

        [Serializable]
        public class Level
        {
            [Tooltip("The scene this pip loads. It has to be in File > Build Settings.")]
            public string sceneName;

            [Tooltip("The button on the map that loads it.")]
            public Button pip;
        }
    }
}
