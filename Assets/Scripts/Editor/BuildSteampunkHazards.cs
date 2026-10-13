#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using FallingWizard.Core;
using FallingWizard.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.EditorTools
{
    public static class BuildSteampunkHazards
    {
        const string ParentFolder = "Assets/Prefabs";
        const string FolderName = "Steampunk";
        const string Folder = ParentFolder + "/" + FolderName;

        const string SteamVentName = "Steam Vent";
        const string WaxDropName = "Wax Drop";
        const string WaxCandleName = "Wax Candle";
        const string MovingPlatformName = "Moving Platform";
        const string PressurePlateName = "Pressure Plate";
        const string BrokenGlassName = "Broken Glass";

        const string ArtName = "Art";
        const string StandInSprite = "UI/Skin/UISprite.psd";
        const int ArtSortingOrder = 1;
        const int DefaultLayer = 0;

        static readonly Vector2 SteamColumnSize = new Vector2(1f, 3f);
        static readonly Vector2 SteamColumnCentre = new Vector2(0f, 1.5f);
        static readonly Vector2 PipeSize = new Vector2(1.2f, 0.4f);
        static readonly Vector2 PipeCentre = new Vector2(0f, 0.2f);
        static readonly Vector2 PuffSize = new Vector2(0.6f, 0.5f);
        static readonly Vector2 PuffCentre = new Vector2(0f, 0.65f);
        static readonly Color PipeColour = new Color(0.71f, 0.55f, 0.25f);
        static readonly Color PuffColour = new Color(0.85f, 0.85f, 0.85f, 0.7f);
        static readonly Color SteamColour = new Color(0.95f, 0.97f, 1f, 0.35f);

        const float DropRadius = 0.15f;
        static readonly Vector2 DropSize = new Vector2(0.3f, 0.3f);
        static readonly Color DropColour = new Color(1f, 0.65f, 0.2f);

        static readonly Vector2 CandleSize = new Vector2(0.4f, 1f);
        static readonly Color CandleColour = new Color(0.95f, 0.9f, 0.75f);

        static readonly Vector2 PlatformSize = new Vector2(3f, 0.5f);
        static readonly Color PlatformColour = new Color(0.35f, 0.4f, 0.48f);

        static readonly Vector2 PlateSize = new Vector2(1f, 0.3f);
        static readonly Vector2 PlateCentre = new Vector2(0f, 0.15f);
        static readonly Vector2 PressedSize = new Vector2(1f, 0.1f);
        static readonly Vector2 PressedCentre = new Vector2(0f, 0.05f);
        static readonly Color PlateColour = new Color(0.72f, 0.38f, 0.22f);
        static readonly Color PressedColour = new Color(0.85f, 0.2f, 0.15f);

        static readonly Vector2 GlassZoneSize = new Vector2(2f, 0.4f);
        static readonly Vector2 GlassZoneCentre = new Vector2(0f, 0.2f);
        static readonly Vector2 ShardsSize = new Vector2(2f, 0.15f);
        static readonly Vector2 ShardsCentre = new Vector2(0f, 0.075f);
        static readonly Color ShardsColour = new Color(0.5f, 0.8f, 0.6f, 0.85f);

        static readonly (string Name, int Layer, System.Action<GameObject> SetUp)[] Prefabs =
        {
            (SteamVentName, Layers.Hazard, SetUpSteamVent),
            (WaxDropName, Layers.Hazard, SetUpWaxDrop),
            (WaxCandleName, DefaultLayer, SetUpWaxCandle),
            (MovingPlatformName, Layers.Ground, SetUpMovingPlatform),
            (PressurePlateName, Layers.Hazard, SetUpPressurePlate),
            (BrokenGlassName, Layers.Hazard, SetUpBrokenGlass),
        };

        [MenuItem("Falling Wizard/Build Steampunk Hazards")]
        public static void Build()
        {
            EnsureFolder();

            var made = new List<string>();
            var leftAlone = new List<string>();

            Scene stage = EditorSceneManager.NewPreviewScene();

            try
            {
                foreach ((string name, int layer, System.Action<GameObject> setUp) in Prefabs)
                {
                    if (File.Exists(PathOf(name)))
                        leftAlone.Add(name);
                    else if (MakeAndSave(stage, name, layer, setUp))
                        made.Add(name);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(stage);
            }

            Debug.Log($"Steampunk hazards in {Folder}. Made: {NamesOrNone(made)}. Left alone " +
                      $"because they were already there: {NamesOrNone(leftAlone)}. The coloured " +
                      "boxes are stand-ins to draw over. This never overwrites a prefab, so " +
                      "delete one first if you want a fresh copy of it.");
        }

        static string PathOf(string name) => $"{Folder}/{name}.prefab";

        static string NamesOrNone(List<string> names) =>
            names.Count == 0 ? "none" : string.Join(", ", names);

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(ParentFolder, FolderName);
        }

        static bool MakeAndSave(Scene stage, string name, int layer, System.Action<GameObject> setUp)
        {
            GameObject root = ObjectFactory.CreateGameObject(stage, HideFlags.None, name);
            root.layer = layer;

            setUp(root);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PathOf(name));
            Object.DestroyImmediate(root);

            return saved != null;
        }

        static void SetUpSteamVent(GameObject root)
        {
            AddTriggerBox(root, SteamColumnSize, SteamColumnCentre);

            AddArt(root, "Pipe", PipeSize, PipeCentre, PipeColour);
            GameObject warning = AddArt(root, "Warning Look", PuffSize, PuffCentre, PuffColour);
            GameObject steam = AddArt(root, "Steam Look", SteamColumnSize, SteamColumnCentre, SteamColour);

            warning.SetActive(false);
            steam.SetActive(false);

            var vent = ObjectFactory.AddComponent<SteamVent>(root);
            vent.warningLook = warning;
            vent.steamLook = steam;
        }

        static void SetUpWaxDrop(GameObject root)
        {
            var drop = ObjectFactory.AddComponent<CircleCollider2D>(root);
            drop.radius = DropRadius;
            drop.isTrigger = true;

            AddKinematicBody(root);
            AddArt(root, ArtName, DropSize, Vector2.zero, DropColour);

            ObjectFactory.AddComponent<WaxDrop>(root);
        }

        static void SetUpWaxCandle(GameObject root)
        {
            var candle = ObjectFactory.AddComponent<WaxCandle>(root);
            candle.dropPrefab = AssetDatabase.LoadAssetAtPath<WaxDrop>(PathOf(WaxDropName));

            Vector2 justAboveTheSpout = candle.spoutOffset + Vector2.up * (CandleSize.y * 0.5f);
            AddArt(root, ArtName, CandleSize, justAboveTheSpout, CandleColour);

            if (candle.dropPrefab == null)
                Debug.LogWarning($"No WaxDrop was found on {PathOf(WaxDropName)}, so the " +
                                 $"{WaxCandleName} was made with nothing to drip. Drag a drop " +
                                 "prefab into its Drop Prefab slot.");
        }

        static void SetUpMovingPlatform(GameObject root)
        {
            var deck = ObjectFactory.AddComponent<BoxCollider2D>(root);
            deck.size = PlatformSize;
            deck.isTrigger = false;

            Rigidbody2D body = AddKinematicBody(root);
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            AddArt(root, ArtName, PlatformSize, Vector2.zero, PlatformColour);

            ObjectFactory.AddComponent<MovingPlatform>(root);
        }

        static void SetUpPressurePlate(GameObject root)
        {
            AddTriggerBox(root, PlateSize, PlateCentre);

            GameObject idle = AddArt(root, "Idle Look", PlateSize, PlateCentre, PlateColour);
            GameObject pressed = AddArt(root, "Pressed Look", PressedSize, PressedCentre, PressedColour);

            pressed.SetActive(false);

            var plate = ObjectFactory.AddComponent<PressurePlate>(root);
            plate.idleLook = idle;
            plate.pressedLook = pressed;
        }

        static void SetUpBrokenGlass(GameObject root)
        {
            AddTriggerBox(root, GlassZoneSize, GlassZoneCentre);
            AddArt(root, ArtName, ShardsSize, ShardsCentre, ShardsColour);

            ObjectFactory.AddComponent<BrokenGlass>(root);
        }

        static void AddTriggerBox(GameObject root, Vector2 size, Vector2 offset)
        {
            var box = ObjectFactory.AddComponent<BoxCollider2D>(root);
            box.size = size;
            box.offset = offset;
            box.isTrigger = true;
        }

        static Rigidbody2D AddKinematicBody(GameObject root)
        {
            var body = ObjectFactory.AddComponent<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Kinematic;
            return body;
        }

        static GameObject AddArt(GameObject root, string name, Vector2 size, Vector2 centre, Color colour)
        {
            GameObject art = ObjectFactory.CreateGameObject(root.scene, HideFlags.None, name);
            art.layer = root.layer;
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = centre;

            var look = ObjectFactory.AddComponent<SpriteRenderer>(art);
            look.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(StandInSprite);
            look.drawMode = SpriteDrawMode.Sliced;
            look.size = size;
            look.sortingOrder = ArtSortingOrder;
            look.color = colour;

            return art;
        }
    }
}
#endif
