#if UNITY_EDITOR
using FallingWizard.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.EditorTools
{
    public static class BuildRagdollBumpStars
    {
        const string ParentFolder = "Assets/Prefabs";
        const string FolderName = "Effects";
        const string Folder = ParentFolder + "/" + FolderName;
        const string EffectName = "Ragdoll Bump Stars";
        const string PrefabPath = Folder + "/" + EffectName + ".prefab";
        const string MaterialPath = Folder + "/" + EffectName + ".mat";
        const string StarSpritePath = "Assets/Art/Placeholders/Bump Star.png";
        const string PlayerPrefabPath = "Assets/Prefabs/PLAYER.prefab";

        const string UnlitSpriteShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        const string LegacySpriteShader = "Sprites/Default";

        const float BurstSeconds = 0.5f;
        const float ShortestLife = 0.35f;
        const float LongestLife = 0.6f;
        const float SlowestStar = 2f;
        const float FastestStar = 4.5f;
        const float SmallestStar = 0.18f;
        const float BiggestStar = 0.32f;
        const float StarFall = 0.6f;
        const float BurstRadius = 0.1f;
        const float FullTurn = 360f;
        const float SpinPerSecond = 360f;

        const short StarsPerBump = 7;
        const int MostStars = 30;
        const int AboveTheWizard = 20;

        static readonly Color PaleYellow = new Color(1f, 0.95f, 0.45f);
        static readonly Color DeepYellow = new Color(1f, 0.8f, 0.15f);

        [MenuItem("Falling Wizard/Build Ragdoll Bump Stars")]
        static void Build()
        {
            GameObject stars = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            bool wasThere = stars != null;

            if (!wasThere)
                stars = TryMakeStars();

            if (stars == null)
                return;

            bool wired = TryWireIntoThePlayer(stars);

            Debug.Log(DescribeWhatHappened(wasThere, wired));
        }

        static GameObject TryMakeStars()
        {
            Sprite star = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);

            if (star == null)
            {
                Debug.LogError($"No star sprite at {StarSpritePath}, so the bump stars were not " +
                               "made. Put a sprite there, or change StarSpritePath.");
                return null;
            }

            EnsureFolder();
            Material material = LoadOrMakeMaterial();

            Scene stage = EditorSceneManager.NewPreviewScene();

            try
            {
                return SaveStars(stage, star, material);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(ParentFolder, FolderName);
        }

        static Material LoadOrMakeMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (material != null)
                return material;

            Shader shader = Shader.Find(UnlitSpriteShader);

            if (shader == null)
                shader = Shader.Find(LegacySpriteShader);

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static GameObject SaveStars(Scene stage, Sprite star, Material material)
        {
            GameObject root = ObjectFactory.CreateGameObject(stage, HideFlags.None, EffectName);
            var particles = ObjectFactory.AddComponent<ParticleSystem>(root);

            SetUpTheBurst(particles);
            SetUpTheMotion(particles);
            SetUpTheLook(particles, star, material);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        static void SetUpTheBurst(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.duration = BurstSeconds;
            main.loop = false;
            main.playOnAwake = true;
            main.maxParticles = MostStars;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, StarsPerBump) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = BurstRadius;
        }

        static void SetUpTheMotion(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(ShortestLife, LongestLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(SlowestStar, FastestStar);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, FullTurn * Mathf.Deg2Rad);
            main.gravityModifier = StarFall;

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-SpinPerSecond * Mathf.Deg2Rad,
                SpinPerSecond * Mathf.Deg2Rad);
        }

        static void SetUpTheLook(ParticleSystem particles, Sprite star, Material material)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(SmallestStar, BiggestStar);
            main.startColor = new ParticleSystem.MinMaxGradient(PaleYellow, DeepYellow);

            ParticleSystem.SizeOverLifetimeModule shrink = particles.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;

            if (sheet.spriteCount > 0)
                sheet.SetSprite(0, star);
            else
                sheet.AddSprite(star);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = AboveTheWizard;
        }

        static bool TryWireIntoThePlayer(GameObject stars)
        {
            GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);

            try
            {
                var bumps = player.GetComponent<RagdollBumps>();

                if (bumps == null)
                    bumps = player.AddComponent<RagdollBumps>();

                if (bumps.bumpEffect != null)
                    return false;

                bumps.bumpEffect = stars;
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }
        }

        static string DescribeWhatHappened(bool wasThere, bool wired)
        {
            string prefab = wasThere
                ? $"{PrefabPath} was already there, so it was left alone."
                : $"Made {PrefabPath}.";

            string player = wired
                ? "The PLAYER prefab's Ragdoll Bumps now spawns it."
                : "The PLAYER prefab's Ragdoll Bumps already had an effect, so it was left alone.";

            return $"{prefab} {player} Open the prefab to change the stars: colour and size are " +
                   "under Start Color and Start Size, how many under Emission, and the star art " +
                   "under Texture Sheet Animation.";
        }
    }
}
#endif
