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

        const float WallSampleSpacing = 0.25f;
        const float IntoTheWall = 0.05f;
        const float StreakHitRadius = 0.1f;

        const float StreakTravel = 1.5f;
        const float StreaksPerSquareBox = 0.15f;
        const float StreakHeadroom = 2f;

        static readonly Color ZoneColour = new Color(0.55f, 0.85f, 1f, 0.35f);
        static readonly Color ArrowColour = new Color(0.55f, 0.85f, 1f, 0.9f);

        static readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>();
        static readonly List<Collider2D> Overlaps = new List<Collider2D>();

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
        [Tooltip("Walls and floors stop this wind: a wizard with a separate wall between them and " +
                 "the side it blows from is sheltered and not pushed, and the streaks stop at " +
                 "walls too. The wall or floor the wind comes OUT of never counts, however deep " +
                 "the zone reaches into it. Off blows straight through walls.")]
        public bool blockedByWalls = true;

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
            collision.radiusScale = StreakHitRadius;
        }

        bool IsSheltered(PlayerCharacter wizard)
        {
            var area = GetComponent<BoxCollider2D>();

            if (!blockedByWalls || area == null || push.sqrMagnitude < Epsilon)
                return false;

            Vector2 from = wizard.Logic.movement.Position;
            Vector2 source = FindTheSourceEdge(area, from);
            Vector2 toSource = source - from;
            float distance = toSource.magnitude;

            if (distance < Epsilon)
                return false;

            Vector2 upwind = toSource / distance;

            if (Physics2D.Raycast(from, upwind, SolidGround, Hits, distance) == 0)
                return false;

            return HasOpenAirBetween(Hits[0].point + upwind * IntoTheWall, source);
        }

        bool HasOpenAirBetween(Vector2 wallSide, Vector2 source)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(wallSide, source) / WallSampleSpacing);

            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = steps == 0 ? source : Vector2.Lerp(wallSide, source, (float)i / steps);

                if (Physics2D.OverlapPoint(point, SolidGround, Overlaps) == 0)
                    return true;
            }

            return false;
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
