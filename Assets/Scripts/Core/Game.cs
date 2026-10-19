using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public static class Game
    {
        public const string FirstLevelScene = "Level 1";
        public const string LevelSelectScene = "Level Select";

        const string MainMenuScene = "Main Menu";

        public static bool IsPaused { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ClearPause();

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused)
                return;

            ApplyPause(paused);
        }

        public static void LoadMainMenu() => Load(MainMenuScene);

        public static void LoadFirstLevel() => Load(FirstLevelScene);

        public static void LoadLevelSelect() => Load(LevelSelectScene);

        public static void LoadLevel(string sceneName) => Load(sceneName);

        public static void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string sceneName)
        {
            ClearPause();
            SceneManager.LoadScene(sceneName);
        }

        static void ClearPause() => ApplyPause(false);

        static void ApplyPause(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
        }
    }
}
