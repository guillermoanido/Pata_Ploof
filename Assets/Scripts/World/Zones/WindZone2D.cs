using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WindZone2D : Zone
    {
        const float ArrowLengthPerBox = 0.25f;
        const float ArrowLengthOfZone = 0.4f;
        const float ArrowSpreadOfZone = 0.3f;
        const float ArrowHeadLength = 0.4f;
        const float ArrowHeadWidth = 0.25f;
        const int ArrowRows = 1;

        static readonly Color ZoneColour = new Color(0.55f, 0.85f, 1f, 0.35f);
        static readonly Color ArrowColour = new Color(0.55f, 0.85f, 1f, 0.9f);

        [Header("Wind")]
        [Tooltip("Which way this blows, and how hard, as the SPEED in boxes per second it wants " +
                 "to carry the wizard at. It turns with the object: rotate the zone and the wind " +
                 "turns with it, which the arrows in the scene view show. Both numbers mean the " +
                 "same thing, so (-6,0) is a hard leftward gale next to a run of 4 and (0,6) " +
                 "lifts just as firmly. Sideways you can lean against it and partly win, because " +
                 "steering is added on top. Up and down it beats gravity outright, so (0,6) " +
                 "really does carry them up at 6 - it does not need to out-number gravity to be " +
                 "felt. For a shove that lands once rather than a wind that holds, use a Wind " +
                 "Trap's Kick.")]
        public Vector2 push = new Vector2(-4f, 0f);

        [Tooltip("How quickly the wind takes hold once you step in, in boxes per second squared.")]
        [Min(0f)] public float rampup = 20f;

        [Tooltip("How much of the SIDEWAYS push is felt with both feet on the ground. 0 means " +
                 "it only pushes you sideways while you are in the air. Up and down is always " +
                 "felt in full, so an updraft lifts a wizard who is standing in it.")]
        [Range(0f, 1f)] public float groundScale = 0.35f;

        [Header("Editor")]
        [Tooltip("Draw the arrows in the scene view without having to select the zone first, " +
                 "which is what you want while laying a level out.")]
        public bool alwaysShowArrows = true;

        protected Vector2 WorldPush => TurnWithThisObject(push);

        protected override void Reset()
        {
            base.Reset();
            affectsRagdolled = true;
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard) { }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!CanAffect(wizard))
                return;

            wizard.Logic.Push(WorldPush * Haste.WorldScale, rampup, groundScale);
        }

        protected Vector2 TurnWithThisObject(Vector2 direction) =>
            transform.rotation * direction;

        void OnDrawGizmos()
        {
            if (alwaysShowArrows)
                DrawArrows(UnselectedGizmo);
        }

        void OnDrawGizmosSelected() => DrawArrows(SelectedGizmo);

        void DrawArrows(float strength)
        {
            var shape = GetComponent<BoxCollider2D>();

            if (shape == null || push.sqrMagnitude < Epsilon)
                return;

            Bounds zone = shape.bounds;

            Gizmos.color = MakeFaded(ZoneColour, strength);
            Gizmos.DrawWireCube(zone.center, zone.size);

            DrawArrowRows(zone, strength);
        }

        void DrawArrowRows(Bounds zone, float strength)
        {
            Vector2 way = WorldPush.normalized;
            Vector2 across = new Vector2(-way.y, way.x);

            float narrow = Mathf.Min(zone.size.x, zone.size.y);

            float length = Mathf.Min(push.magnitude * ArrowLengthPerBox,
                                     narrow * ArrowLengthOfZone);
            float spread = narrow * ArrowSpreadOfZone;

            Gizmos.color = MakeFaded(ArrowColour, strength);

            for (int row = -ArrowRows; row <= ArrowRows; row++)
            {
                Vector2 middle = (Vector2)zone.center + across * (spread * row);
                DrawArrow(middle, way, across, length);
            }
        }

        static void DrawArrow(Vector2 middle, Vector2 way, Vector2 across, float length)
        {
            Vector2 tip = middle + way * length;

            Gizmos.DrawLine(middle - way * length, tip);
            Vector2 barb = tip - way * (length * ArrowHeadLength);
            Vector2 flare = across * (length * ArrowHeadWidth);

            Gizmos.DrawLine(tip, barb + flare);
            Gizmos.DrawLine(tip, barb - flare);
        }
    }
}
