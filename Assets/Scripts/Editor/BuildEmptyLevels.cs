#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using FallingWizard.Menus;
using FallingWizard.Player;
using FallingWizard.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace FallingWizard.EditorTools
{
    public static class BuildEmptyLevels
    {
        const string ScenesFolder = "Assets/Scenes";
        const string TemplateLevel = "Level 1";
        const string GlobalLightType = "Light2D";

        const int FloorTilesEachSide = 15;
        const int ExitTilesAhead = 10;
        const int DeepestFloorSearch = 40;
        const int NotInTheBuild = -1;

        static readonly string[] EmptyLevels = { "Level 2", "Level 3" };

        [MenuItem("Falling Wizard/Build Empty Levels 2 and 3")]
        static void Build()
        {
            var made = new List<string>();
            var leftAlone = new List<string>();

            foreach (string level in EmptyLevels)
            {
                if (File.Exists(PathOf(level)))
                    leftAlone.Add(level);
                else if (TryMakeEmptyLevel(level))
                    made.Add(level);
            }

            AddLevelsToTheBuild();

            Debug.Log($"Empty levels in {ScenesFolder}. Made: {NamesOrNone(made)}. Left alone " +
                      $"because they were already there: {NamesOrNone(leftAlone)}. Each one is " +
                      $"{TemplateLevel} with the level taken out: the wizard, camera, light, HUD " +
                      "and menus stay, with a strip of floor under the wizard and a Level Exit " +
                      "ahead of them. They are in File > Build Settings, so the level select " +
                      "can open them.");
        }

        static string PathOf(string level) => $"{ScenesFolder}/{level}.unity";

        static string NamesOrNone(List<string> names) =>
            names.Count == 0 ? "none" : string.Join(", ", names);

        static bool TryMakeEmptyLevel(string level)
        {
            if (!AssetDatabase.CopyAsset(PathOf(TemplateLevel), PathOf(level)))
            {
                Debug.LogError($"Could not copy {PathOf(TemplateLevel)} to make {level}.");
                return false;
            }

            Scene scene = EditorSceneManager.OpenScene(PathOf(level), OpenSceneMode.Additive);

            EmptyOut(scene);

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
            return true;
        }

        static void EmptyOut(Scene scene)
        {
            PlayerCharacter wizard = FindFirst<PlayerCharacter>(scene);
            LevelExit exit = FindFirst<LevelExit>(scene);

            RemoveTheLevel(scene, exit);
            LayAFloorUnder(scene, wizard);
            PlaceTheExitAhead(exit, wizard);
        }

        static T FindFirst<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);

                if (found != null)
                    return found;
            }

            return null;
        }

        static void RemoveTheLevel(Scene scene, LevelExit keptExit)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool isTheKeptExit = keptExit != null && root == keptExit.gameObject;

                if (!isTheKeptExit && !IsPartOfTheGame(root))
                    Object.DestroyImmediate(root);
            }
        }

        static bool IsPartOfTheGame(GameObject root) =>
            root.GetComponentInChildren<PlayerCharacter>(true) != null ||
            root.GetComponentInChildren<Camera>(true) != null ||
            root.GetComponentInChildren<EventSystem>(true) != null ||
            root.GetComponentInChildren<Grid>(true) != null ||
            root.GetComponentInChildren<Canvas>(true) != null ||
            root.GetComponentInChildren<PauseMenuController>(true) != null ||
            HasTheGlobalLight(root);

        static bool HasTheGlobalLight(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && component.GetType().Name == GlobalLightType)
                    return true;
            }

            return false;
        }

        static void LayAFloorUnder(Scene scene, PlayerCharacter wizard)
        {
            Tilemap ground = FindTheGround(scene);

            if (ground == null || wizard == null)
            {
                Debug.LogWarning("There is no ground tilemap or no wizard to lay a floor under, " +
                                 "so the new level has no floor yet.");
                return;
            }

            Vector3Int underTheWizard = ground.WorldToCell(wizard.transform.position) + Vector3Int.down;
            TileBase floorTile = FindTheTileBelow(ground, underTheWizard);

            ClearEveryTilemap(scene);

            if (floorTile == null)
                return;

            for (int step = -FloorTilesEachSide; step <= FloorTilesEachSide; step++)
                ground.SetTile(underTheWizard + Vector3Int.right * step, floorTile);
        }

        static Tilemap FindTheGround(Scene scene)
        {
            Tilemap anyTilemap = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                {
                    if (map.GetComponent<TilemapCollider2D>() != null)
                        return map;

                    if (anyTilemap == null)
                        anyTilemap = map;
                }
            }

            return anyTilemap;
        }

        static TileBase FindTheTileBelow(Tilemap ground, Vector3Int start)
        {
            for (int depth = 0; depth < DeepestFloorSearch; depth++)
            {
                TileBase tile = ground.GetTile(start + Vector3Int.down * depth);

                if (tile != null)
                    return tile;
            }

            return FindAnyTile(ground);
        }

        static TileBase FindAnyTile(Tilemap ground)
        {
            foreach (Vector3Int cell in ground.cellBounds.allPositionsWithin)
            {
                TileBase tile = ground.GetTile(cell);

                if (tile != null)
                    return tile;
            }

            return null;
        }

        static void ClearEveryTilemap(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Tilemap map in root.GetComponentsInChildren<Tilemap>(true))
                    map.ClearAllTiles();
            }
        }

        static void PlaceTheExitAhead(LevelExit exit, PlayerCharacter wizard)
        {
            if (exit == null || wizard == null)
            {
                Debug.LogWarning("There was no Level Exit to keep, so the new level has no way " +
                                 "out yet. Drag the Level Exit prefab in.");
                return;
            }

            Vector3 start = wizard.transform.position;
            exit.transform.position = new Vector3(start.x + ExitTilesAhead, start.y,
                exit.transform.position.z);
        }

        static void AddLevelsToTheBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int insertAt = IndexInTheBuild(scenes, PathOf(TemplateLevel)) + 1;

            foreach (string level in EmptyLevels)
            {
                if (!File.Exists(PathOf(level)) || IndexInTheBuild(scenes, PathOf(level)) != NotInTheBuild)
                    continue;

                scenes.Insert(insertAt, new EditorBuildSettingsScene(PathOf(level), true));
                insertAt++;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static int IndexInTheBuild(List<EditorBuildSettingsScene> scenes, string path)
        {
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                    return i;
            }

            return NotInTheBuild;
        }
    }
}
#endif
