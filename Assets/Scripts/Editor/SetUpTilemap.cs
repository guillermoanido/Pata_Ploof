#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FallingWizard.EditorTools
{
    public static class SetUpTilemap
    {
        const string GroundLayer = "Ground";

        [MenuItem("Falling Wizard/Set Up Tilemap Collision")]
        static void Run()
        {
            int ground = LayerMask.NameToLayer(GroundLayer);

            if (ground < 0)
            {
                Debug.LogError($"There is no '{GroundLayer}' layer in this project, so nothing " +
                               "the wizard walks on can be detected. Add it in Tags and Layers.");
                return;
            }

            Tilemap[] targets = ChooseTilemaps();

            if (targets.Length == 0)
            {
                Debug.LogWarning("No Tilemap found to set up. Open the level scene, or select the " +
                                 "Tilemap objects you want collision on, and run this again.");
                return;
            }

            foreach (Tilemap map in targets)
                Wire(map, ground);

            EditorSceneManager.MarkSceneDirty(targets[0].gameObject.scene);
            Debug.Log($"Tilemap collision set up on {targets.Length} tilemap(s). Save the scene to keep it.");
        }

        static Tilemap[] ChooseTilemaps()
        {
            var picked = new List<Tilemap>();

            foreach (GameObject selected in Selection.gameObjects)
                AddNotYetPicked(picked, selected.GetComponentsInChildren<Tilemap>(true));

            if (picked.Count > 0)
                return picked.ToArray();

            return Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        static void AddNotYetPicked(List<Tilemap> picked, Tilemap[] maps)
        {
            foreach (Tilemap map in maps)
            {
                if (!picked.Contains(map))
                    picked.Add(map);
            }
        }

        static void Wire(Tilemap map, int ground)
        {
            GameObject tilemapObject = map.gameObject;
            var changes = new List<string>();

            MoveToGroundLayer(tilemapObject, ground, changes);
            MakeBodyStatic(tilemapObject, changes);
            MergeTilesIntoComposite(tilemapObject, changes);
            UsePolygonComposite(tilemapObject, changes);

            EditorUtility.SetDirty(tilemapObject);

            Debug.Log(changes.Count == 0
                ? $"'{tilemapObject.name}' was already set up."
                : $"'{tilemapObject.name}': {string.Join(", ", changes)}.", tilemapObject);
        }

        static void MoveToGroundLayer(GameObject tilemapObject, int ground, List<string> changes)
        {
            if (tilemapObject.layer != ground)
            {
                Undo.RecordObject(tilemapObject, "Tilemap layer");
                tilemapObject.layer = ground;
                changes.Add($"layer -> {GroundLayer}");
            }
        }

        static void MakeBodyStatic(GameObject tilemapObject, List<string> changes)
        {
            var body = GetOrAdd<Rigidbody2D>(tilemapObject, changes);

            if (body.bodyType != RigidbodyType2D.Static)
            {
                Undo.RecordObject(body, "Tilemap body type");
                body.bodyType = RigidbodyType2D.Static;
                changes.Add("body -> Static");
            }
        }

        static void MergeTilesIntoComposite(GameObject tilemapObject, List<string> changes)
        {
            var tiles = GetOrAdd<TilemapCollider2D>(tilemapObject, changes);

            if (tiles.compositeOperation != Collider2D.CompositeOperation.Merge)
            {
                Undo.RecordObject(tiles, "Tilemap composite operation");
                tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
                changes.Add("collider -> merged into composite");
            }
        }

        static void UsePolygonComposite(GameObject tilemapObject, List<string> changes)
        {
            var composite = GetOrAdd<CompositeCollider2D>(tilemapObject, changes);

            if (composite.geometryType != CompositeCollider2D.GeometryType.Polygons)
            {
                Undo.RecordObject(composite, "Composite geometry");
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                changes.Add("composite -> Polygons");
            }
        }

        static T GetOrAdd<T>(GameObject tilemapObject, List<string> changes) where T : Component
        {
            var component = tilemapObject.GetComponent<T>();

            if (component == null)
            {
                component = Undo.AddComponent<T>(tilemapObject);
                changes.Add($"added {typeof(T).Name}");
            }

            return component;
        }
    }
}
#endif
