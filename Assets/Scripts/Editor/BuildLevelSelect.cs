#if UNITY_EDITOR
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Menus;
using FallingWizard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FallingWizard.EditorTools
{
    public static class BuildLevelSelect
    {
        const string ScenePath = "Assets/Scenes/" + Game.LevelSelectScene + ".unity";

        const string TopLevelScene = Game.FirstLevelScene;
        const string MidLevelScene = "Level 2";
        const string BottomLevelScene = "Level 3";

        const int CanvasSortingOrder = 0;

        static readonly Vector2 MapSize = new Vector2(1080f, 1080f);

        static readonly Vector2 PipSize = new Vector2(360f, 72f);
        const float PipFontSize = 32f;

        static readonly Vector2 TopOfTheMap = new Vector2(0.5f, 0.85f);
        static readonly Vector2 MiddleOfTheMap = new Vector2(0.5f, 0.5f);
        static readonly Vector2 BottomOfTheMap = new Vector2(0.5f, 0.15f);

        [MenuItem("Falling Wizard/Build Level Select Scene")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.LogWarning($"{ScenePath} already exists, so it was left alone. Delete it " +
                                 "first if you want a fresh one.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            Canvas canvas = Ui.CreateCanvas(Game.LevelSelectScene, CanvasSortingOrder);

            AddBlackScreen(canvas.transform);
            Transform map = AddTowerMap(canvas.transform);

            var menu = canvas.gameObject.AddComponent<LevelSelectMenu>();

            menu.levels = new[]
            {
                AddPip(map, "Top Pip", TopLevelScene, Loc.Keys.LevelsTop, TopOfTheMap),
                AddPip(map, "Mid Pip", MidLevelScene, Loc.Keys.LevelsMid, MiddleOfTheMap),
                AddPip(map, "Bottom Pip", BottomLevelScene, Loc.Keys.LevelsBottom, BottomOfTheMap),
            };

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log($"Level Select built at {ScenePath} and added to Build Settings. Drag your " +
                      "tower map into the Tower Map's Source Image, then move the pips onto it.");
        }

        static void AddBlackScreen(Transform canvas)
        {
            var black = new GameObject("Black Screen", typeof(Image));
            Ui.Attach(black, canvas);
            Ui.Stretch(black);

            black.GetComponent<Image>().color = Color.black;
        }

        static Transform AddTowerMap(Transform canvas)
        {
            var map = new GameObject("Tower Map", typeof(Image));
            Ui.Attach(map, canvas);

            ((RectTransform)map.transform).sizeDelta = MapSize;
            map.GetComponent<Image>().preserveAspect = true;

            return map.transform;
        }

        static LevelSelectMenu.Level AddPip(Transform map, string name, string sceneName,
            string key, Vector2 onTheMap)
        {
            Button pip = Ui.CreateButton(Loc.Get(key), map, PipSize.x, PipSize.y, PipFontSize);
            pip.name = name;

            var rect = (RectTransform)pip.transform;
            rect.anchorMin = onTheMap;
            rect.anchorMax = onTheMap;
            rect.anchoredPosition = Vector2.zero;

            var label = pip.GetComponentInChildren<TextMeshProUGUI>();
            label.gameObject.AddComponent<LocalizedText>().key = key;

            return new LevelSelectMenu.Level { sceneName = sceneName, pip = pip };
        }

        static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            if (scenes.Exists(entry => entry.path == ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
