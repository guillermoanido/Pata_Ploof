using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public class MusicPlayer : MonoBehaviour
    {
        const string PrefabName = "Music Player";

        [Tooltip("The song played in the levels. It loops, and keeps playing from level to level " +
                 "without starting over.")]
        public AudioClip song;

        [Tooltip("How loud the song is next to everything else. The Volume setting in the " +
                 "Settings menu still turns the whole game up and down on top of this.")]
        [Range(0f, 1f)] public float loudness = 0.6f;

        [Tooltip("Scenes where the music stops. Every other scene - the levels and the Level " +
                 "Select between them - keeps it playing. Coming back from one of these starts " +
                 "the song from the beginning.")]
        public string[] silentScenes = { "Main Menu", "Cutscene" };

        static MusicPlayer instance;

        AudioSource speaker;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            var prefab = Resources.Load<MusicPlayer>(PrefabName);

            if (prefab == null)
            {
                Debug.LogWarning($"There is no '{PrefabName}' prefab in a Resources folder, so " +
                                 "the game plays no music.");
                return;
            }

            instance = Instantiate(prefab);
            DontDestroyOnLoad(instance.gameObject);
        }

        void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.clip = song;
            speaker.loop = true;
            speaker.volume = loudness;
            speaker.playOnAwake = false;
            speaker.ignoreListenerPause = true;
        }

        void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsSilent(scene.name))
                speaker.Stop();
            else if (!speaker.isPlaying && song != null)
                speaker.Play();
        }

        bool IsSilent(string sceneName)
        {
            foreach (string silent in silentScenes)
            {
                if (silent == sceneName)
                    return true;
            }

            return false;
        }
    }
}
