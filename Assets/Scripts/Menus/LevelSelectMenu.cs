using System;
using System.Collections;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class LevelSelectMenu : MonoBehaviour
    {
        [Tooltip("The worlds on the tower map, from the top of the tower to the bottom. The first " +
                 "level of the first world is always open, and each level after it opens once " +
                 "the level before it has been completed, so finishing a world's last level " +
                 "opens the next world. With cheats on, every level is open. The dotted line runs " +
                 "in this same order: a world's name, its levels, then the next world's name.")]
        public World[] worlds = new World[0];

        [Header("Dotted Line")]
        [Tooltip("One dot of the line: a switched-off child of the map holding your own art. The " +
                 "line is made of copies of this object, so restyle it and every dot follows. " +
                 "Leave it empty and no line is drawn.")]
        public RectTransform dot;

        [Tooltip("The distance from one dot to the next, in canvas pixels. Move a level or a " +
                 "world's name on the map and the line follows it by itself.")]
        [Min(1f)] public float dotSpacing = 15f;

        [Header("Travelling")]
        [Tooltip("One dot of the journey: a switched-off child of the map holding your own art. " +
                 "Picking a level lays copies of this along the line, from the top of the tower " +
                 "down to that level, and then the level loads. Leave it empty and the level " +
                 "loads straight away.")]
        public RectTransform travelDot;

        [Tooltip("Seconds between one travel dot and the next. Each level further down the tower " +
                 "is a longer journey.")]
        [Min(0f)] public float secondsPerDot = 0.03f;

        [NonSerialized] bool isTravelling;

        void OnEnable() => Cheats.Toggled += OpenTheLevels;

        void Start()
        {
            DrawTheDottedLine();
            WireThePips();
            OpenTheLevels();
        }

        void OnDisable() => Cheats.Toggled -= OpenTheLevels;

        void DrawTheDottedLine()
        {
            if (dot == null)
                return;

            List<RectTransform> stops = StopsInOrder();

            for (int i = 1; i < stops.Count; i++)
            {
                foreach (Vector3 spot in SpotsBetween(stops[i - 1], stops[i]))
                    PlaceCopyOf(dot, spot);
            }
        }

        List<RectTransform> StopsInOrder()
        {
            var stops = new List<RectTransform>();

            foreach (World world in worlds)
            {
                stops.Add(world.heading);

                foreach (Level level in world.levels)
                    stops.Add((RectTransform)level.pip.transform);
            }

            return stops;
        }

        List<Vector3> SpotsBetween(RectTransform from, RectTransform to)
        {
            var spots = new List<Vector3>();
            float spacing = dotSpacing * transform.lossyScale.x;
            int steps = Mathf.RoundToInt(Vector3.Distance(from.position, to.position) / spacing);

            for (int i = 1; i < steps; i++)
            {
                Vector3 spot = Vector3.Lerp(from.position, to.position, (float)i / steps);

                if (!IsCovering(from, spot) && !IsCovering(to, spot))
                    spots.Add(spot);
            }

            return spots;
        }

        static bool IsCovering(RectTransform stop, Vector3 spot) =>
            stop.rect.Contains(stop.InverseTransformPoint(spot));

        static void PlaceCopyOf(RectTransform template, Vector3 spot)
        {
            RectTransform copy = Instantiate(template, template.parent);

            copy.position = spot;
            copy.SetSiblingIndex(template.GetSiblingIndex());
            copy.gameObject.SetActive(true);
        }

        void WireThePips()
        {
            foreach (World world in worlds)
            {
                foreach (Level level in world.levels)
                    level.pip.onClick.AddListener(() => StartTravellingTo(level));
            }
        }

        void StartTravellingTo(Level level)
        {
            if (isTravelling)
                return;

            isTravelling = true;

            if (travelDot == null)
                Game.LoadLevel(level.sceneName);
            else
                StartCoroutine(TravelTo(level));
        }

        IEnumerator TravelTo(Level level)
        {
            List<RectTransform> stops = StopsInOrder();
            int destination = stops.IndexOf((RectTransform)level.pip.transform);

            for (int i = 1; i <= destination; i++)
            {
                foreach (Vector3 spot in SpotsBetween(stops[i - 1], stops[i]))
                {
                    PlaceCopyOf(travelDot, spot);
                    yield return new WaitForSeconds(secondsPerDot);
                }
            }

            Game.LoadLevel(level.sceneName);
        }

        void OpenTheLevels()
        {
            GameObject deepestOpenPip = null;
            string sceneBefore = null;

            foreach (World world in worlds)
            {
                foreach (Level level in world.levels)
                {
                    if (TryOpen(level, sceneBefore))
                        deepestOpenPip = level.pip.gameObject;

                    sceneBefore = level.sceneName;
                }
            }

            Ui.Focus(deepestOpenPip);
        }

        static bool TryOpen(Level level, string sceneBefore)
        {
            bool isOpen = IsUnlocked(sceneBefore) && IsInTheBuild(level);

            level.pip.interactable = isOpen;

            return isOpen;
        }

        static bool IsUnlocked(string sceneBefore) =>
            Cheats.IsActive || sceneBefore == null || Progress.HasCompletedLevel(sceneBefore);

        static bool IsInTheBuild(Level level)
        {
            bool isBuilt = Application.CanStreamedLevelBeLoaded(level.sceneName);

            if (!isBuilt)
                Debug.LogWarning($"The '{level.sceneName}' scene is not in File > Build Settings, " +
                                 "so its pip stays locked.");

            return isBuilt;
        }

        [Serializable]
        public class World
        {
            [Tooltip("The world's name on the map. The dotted line arrives here from the world " +
                     "above and carries on through this world's levels.")]
            public RectTransform heading;

            [Tooltip("This world's levels, in the order they are played.")]
            public Level[] levels = new Level[0];
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
