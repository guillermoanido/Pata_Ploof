using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class SlipperyFloor : Zone
    {
        const int SkidMarks = 6;
        const int SkidRows = 2;
        const float SkidLengthOfZone = 0.3f;
        const float SkidLowRow = 0.25f;
        const float SkidHighRow = 0.6f;

        static readonly Color ZoneColour = new Color(0.6f, 0.9f, 1f, 0.35f);
        static readonly Color SkidColour = new Color(0.75f, 0.95f, 1f, 0.9f);

        [Header("Slippery")]
        [Tooltip("How much of their footing this floor gives the wizard back, from 0 to 1. It " +
                 "scales BOTH how hard they can push off and how hard they can dig in to stop, " +
                 "which together are the whole feel of ice: you keep going after you let go, and " +
                 "you cannot turn round in a hurry.\n\n" +
                 "1 is an ordinary floor and does nothing at all.\n" +
                 "0.5 is WATER or a wet flagstone - a full run takes twice as long to build and " +
                 "carries you about half a box past where you let go, which is a stumble rather " +
                 "than a hazard.\n" +
                 "0.15 is ICE - a run carries you more than a whole tile past where you let go " +
                 "and takes over two and a half seconds to turn around in.\n\n" +
                 "Below about 0.1 the wizard stops being able to steer at all, which players " +
                 "read as broken input rather than as ice. Walking is already four times safer " +
                 "than running here without any help - a walk tops out at half the speed and " +
                 "stopping distance goes as the square of it - so do not reach for a lower " +
                 "number to make a patch survivable.")]
        [Range(0f, 1f)] public float grip = 0.15f;

        [Header("Editor")]
        [Tooltip("Draw the skid marks in the scene view without having to select the patch " +
                 "first, which is what you want while laying a level out. Their length reads the " +
                 "grip, so ice draws long streaks and a damp flagstone draws stubs.")]
        public bool alwaysShowSkids = true;

        protected override void Reset()
        {
            base.Reset();
            affectsRagdolled = true;
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard) => SlickenIfAffected(wizard);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime) =>
            SlickenIfAffected(wizard);

        void SlickenIfAffected(PlayerCharacter wizard)
        {
            if (CanAffect(wizard))
                wizard.Logic.Slicken(grip);
        }

        void OnDrawGizmos()
        {
            if (alwaysShowSkids)
                DrawSkids(UnselectedGizmo);
        }

        void OnDrawGizmosSelected() => DrawSkids(SelectedGizmo);

        void DrawSkids(float strength)
        {
            var shape = GetComponent<BoxCollider2D>();

            if (shape == null)
                return;

            Bounds zone = shape.bounds;

            Gizmos.color = MakeFaded(ZoneColour, strength);
            Gizmos.DrawWireCube(zone.center, zone.size);

            float slide = 1f - Mathf.Clamp01(grip);

            if (slide <= Epsilon)
                return;

            DrawSkidMarks(zone, slide, strength);
        }

        static void DrawSkidMarks(Bounds zone, float slide, float strength)
        {
            float length = zone.size.x * SkidLengthOfZone * slide;
            float spacing = zone.size.x / (SkidMarks + 1);

            Gizmos.color = MakeFaded(SkidColour, strength);

            for (int i = 1; i <= SkidMarks; i++)
            {
                float lift = zone.size.y * (i % SkidRows == 0 ? SkidHighRow : SkidLowRow);

                var from = new Vector2(zone.min.x + spacing * i, zone.min.y + lift);
                Gizmos.DrawLine(from, from + Vector2.right * length);
            }
        }
    }
}
