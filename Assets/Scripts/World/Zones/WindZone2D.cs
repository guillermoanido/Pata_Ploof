using System.Collections.Generic;
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
        const float Half = 0.5f;

        const float StreakTravel = 1.5f;
        const float StreaksPerSquareBox = 0.15f;
        const float StreakHeadroom = 2f;

        static readonly Color ZoneColour = new Color(0.55f, 0.85f, 1f, 0.35f);
        static readonly Color ArrowColour = new Color(0.55f, 0.85f, 1f, 0.9f);

        static readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>();

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

        [Header("Shelter")]
        [Tooltip("Walls and floors stop this wind: a wizard with solid ground between them and " +
                 "the side it blows from is sheltered and not pushed, and the streaks stop at " +
                 "that ground too. Off blows straight through walls.")]
        public bool blockedByWalls = true;

        [Tooltip("Ground this close to the side the wind blows from, in boxes, does not count " +
                 "as a wall. A gust that starts inside the wall or floor it comes out of - a " +
                 "trap's mouth, an updraft rising out of the floor - still blows. Raise it if " +
                 "a wind stops working near its own source.")]
        [Min(0f)] public float sourceDepth = 1f;

        [Header("Streaks")]
        [Tooltip("The white streaks that show the wind: a particle system on a child of this " +
                 "zone. Its look is yours - sprite, colour, fade - but when the level starts this " +
                 "zone aims it along Push, sets its speed to Push's strength, and fills the " +
                 "trigger collider with it, so the streaks always tell the truth about the wind. " +
                 "A Wind Trap only plays them while it blows. Leave it empty for no streaks.")]
        public ParticleSystem streaks;

        [Header("Editor")]
        [Tooltip("Draw the arrows in the scene view without having to select the zone first, " +
                 "which is what you want while laying a level out.")]
        public bool alwaysShowArrows = true;

        protected Vector2 WorldPush => TurnWithThisObject(push);

        ContactFilter2D SolidGround => Layers.MakeSolidOnlyFilter(Layers.GroundMask);

        protected override void Reset()
        {
            base.Reset();
            affectsRagdolled = true;
        }

        protected virtual void Awake() => AimTheStreaks();

        protected override void OnPlayerEntered(PlayerCharacter wizard) { }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!CanBlowOn(wizard))
                return;

            wizard.Logic.Push(WorldPush, rampup, groundScale);
        }

        protected bool CanBlowOn(PlayerCharacter wizard) => CanAffect(wizard) && !IsSheltered(wizard);

        protected Vector2 TurnWithThisObject(Vector2 direction) =>
            transform.rotation * direction;

        protected virtual float CountStreaksPerSecond(float squareBoxes, float lifetime) =>
            squareBoxes * StreaksPerSquareBox;

        void AimTheStreaks()
        {
            var area = GetComponent<BoxCollider2D>();

            if (streaks == null || area == null || push.sqrMagnitude < Epsilon)
                return;

            Vector2 way = WorldPush;
            float lifetime = StreakTravel / way.magnitude;

            ParticleSystem.MainModule main = streaks.main;
            main.startLifetime = lifetime;
            main.startRotation = -Mathf.Atan2(way.y, way.x);

            ParticleSystem.VelocityOverLifetimeModule velocity = streaks.velocityOverLifetime;
            velocity.x = way.x;
            velocity.y = way.y;
            velocity.z = 0f;

            FillTheZone(area, lifetime);
            StopTheStreaksAtWalls();
            streaks.Play();
        }

        void FillTheZone(BoxCollider2D area, float lifetime)
        {
            ParticleSystem.ShapeModule shape = streaks.shape;
            shape.position = area.offset;
            shape.scale = new Vector3(area.size.x, area.size.y, 0f);

            Vector3 stretch = transform.lossyScale;
            float squareBoxes = Mathf.Abs(area.size.x * stretch.x * area.size.y * stretch.y);
            float perSecond = CountStreaksPerSecond(squareBoxes, lifetime);

            ParticleSystem.EmissionModule emission = streaks.emission;
            emission.rateOverTime = perSecond;

            ParticleSystem.MainModule main = streaks.main;
            main.maxParticles = Mathf.Max(main.maxParticles, Mathf.CeilToInt(perSecond * lifetime * StreakHeadroom));
        }

        void StopTheStreaksAtWalls()
        {
            ParticleSystem.CollisionModule collision = streaks.collision;
            collision.enabled = blockedByWalls;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = Layers.GroundMask;
            collision.lifetimeLoss = 1f;
        }

        bool IsSheltered(PlayerCharacter wizard)
        {
            var area = GetComponent<BoxCollider2D>();

            if (!blockedByWalls || area == null || push.sqrMagnitude < Epsilon)
                return false;

            Vector2 from = wizard.Logic.movement.Position;
            Vector2 toSource = FindTheSourceEdge(area, from) - from;
            float reach = toSource.magnitude - sourceDepth;

            return reach > 0f && Physics2D.Raycast(from, toSource.normalized, SolidGround, Hits, reach) > 0;
        }

        Vector2 FindTheSourceEdge(BoxCollider2D area, Vector2 worldPoint)
        {
            Vector2 local = transform.InverseTransformPoint(worldPoint);
            Vector2 upwind = transform.InverseTransformVector(-WorldPush);
            Vector2 half = area.size * Half;

            float distance = Mathf.Min(
                DistanceToSide(local.x, upwind.x, area.offset.x, half.x),
                DistanceToSide(local.y, upwind.y, area.offset.y, half.y));

            return transform.TransformPoint(local + upwind * Mathf.Max(0f, distance));
        }

        static float DistanceToSide(float point, float direction, float centre, float half)
        {
            if (Mathf.Abs(direction) < Epsilon)
                return float.MaxValue;

            float side = direction > 0f ? centre + half : centre - half;
            return (side - point) / direction;
        }

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
