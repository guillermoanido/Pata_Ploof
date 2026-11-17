using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Mage Slap", fileName = "Mage Slap")]
    public class MageSlapAbility : Ability
    {
        const float ShortestReach = 0.5f;
        const float AimStep = 45f;
        const float LeastAimPart = 0.1f;
        const float SweepSkin = 0.05f;
        const float BothSides = 2f;

        static readonly ContactFilter2D SolidGround = Layers.MakeSolidOnlyFilter(Layers.GroundMask);
        static readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>();
        static readonly List<Furniture> InTheWay = new List<Furniture>();

        [Header("Shove")]
        [Tooltip("Sideways speed a slap sends the wizard off at, in boxes per second, away from " +
                 "the wall or floor the hand hit. A run is 4. It never changes how fast they are " +
                 "falling, so it moves them without ever saving them from a fall.")]
        [Min(0f)] public float sidewaysShove = 9f;

        [Tooltip("Downward speed a slap against a ceiling sends the wizard off at, in boxes per " +
                 "second. Nothing pushes them up: the wizard never jumps and never hovers.")]
        [Min(0f)] public float downwardShove = 6f;

        [Tooltip("Seconds the stick cannot steer after a shove, so the slap carries the wizard " +
                 "instead of being steered straight back out of.")]
        [Min(0f)] public float steeringLockout = 0.2f;

        [Header("Furniture")]
        [Tooltip("How hard the hand knocks every piece of furniture in its way, in boxes per " +
                 "second for a piece whose Rigidbody 2D has a Mass of 1. A Mass of 4 goes a " +
                 "quarter as fast. Furniture is the only thing a slap moves.")]
        [Min(0f)] public float furnitureKnock = 6f;

        [Header("Hand")]
        [Tooltip("Shown where the hand lands, turned to face the way it slapped, then removed. " +
                 "The Mage Slap Hand prefab, or your own: draw the hand pointing right, with the " +
                 "palm on the prefab's root. Empty slaps without showing anything.")]
        public GameObject handPrefab;

        [Tooltip("Seconds the hand stays before it is removed.")]
        [Min(0f)] public float handSeconds = 0.3f;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            IsFreeToSlap(wizard) && Aim(wizard).IsWorthIt;

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!IsFreeToSlap(wizard))
                return wizard.IsDiving ? "you are diving" : $"you are {wizard.State} and your hands are busy";

            return Aim(wizard).IsWorthIt
                ? null
                : $"nothing to slap within {PickTier(tiers, wizard).reach} boxes that way: no wall, " +
                  "ceiling or furniture, and slapping the floor straight down does nothing";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Swing swing = Aim(wizard);

            if (!swing.IsWorthIt)
                return false;

            foreach (Furniture piece in InTheWay)
                piece.TakeSlap(swing.direction * furnitureKnock);

            if (swing.shove != Vector2.zero)
                wizard.Shove(swing.shove, steeringLockout);

            ShowHand(swing);
            return true;
        }

        protected override void Validate() => ValidateTiers(tiers);

        static bool IsFreeToSlap(PlayerLogic wizard) =>
            (wizard.State == PlayerState.Normal || wizard.State == PlayerState.Ragdoll) &&
            !wizard.IsDiving;

        Swing Aim(PlayerLogic wizard)
        {
            var swing = new Swing { direction = PickDirection(wizard) };
            Bounds body = wizard.movement.HitboxBounds;
            float reach = PickTier(tiers, wizard).reach;

            if (TryFindSurface(body, swing.direction, reach, out RaycastHit2D surface))
            {
                reach = surface.distance;
                swing.handPoint = surface.point;
                swing.pushesOff = PushesOff(swing.direction);
                swing.shove = FindShove(wizard.movement.Velocity, swing.direction);
            }

            if (TryFindFurniture(body, swing.direction, reach, out Vector2 nearestPiece) &&
                !swing.pushesOff)
                swing.handPoint = nearestPiece;

            swing.hasFurniture = InTheWay.Count > 0;
            return swing;
        }

        static Vector2 PickDirection(PlayerLogic wizard)
        {
            Vector2 stick = wizard.Steering.Move;

            if (stick.magnitude <= wizard.movement.steerDeadzone)
                return new Vector2(wizard.movement.Facing, 0f);

            float degrees = Mathf.Round(Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg / AimStep) * AimStep;
            float radians = degrees * Mathf.Deg2Rad;

            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        static bool TryFindSurface(Bounds body, Vector2 direction, float reach,
            out RaycastHit2D nearest)
        {
            nearest = default;
            Physics2D.BoxCast(body.center, ShrinkForSweep(body.size), 0f, direction, SolidGround, Hits, reach);

            foreach (RaycastHit2D hit in Hits)
                if (nearest.collider == null || hit.distance < nearest.distance)
                    nearest = hit;

            return nearest.collider != null;
        }

        static Vector2 ShrinkForSweep(Vector2 size) =>
            new Vector2(Mathf.Max(0f, size.x - SweepSkin * BothSides),
                Mathf.Max(0f, size.y - SweepSkin * BothSides));

        static bool PushesOff(Vector2 direction) =>
            Mathf.Abs(direction.x) > LeastAimPart || direction.y > LeastAimPart;

        Vector2 FindShove(Vector2 velocity, Vector2 direction)
        {
            Vector2 shove = Vector2.zero;

            if (Mathf.Abs(direction.x) > LeastAimPart)
            {
                int away = -PlayerLogic.Movement.GetDirectionOf(direction.x);
                shove.x = away * Mathf.Max(0f, sidewaysShove - away * velocity.x);
            }

            if (direction.y > LeastAimPart)
                shove.y = Mathf.Min(0f, -downwardShove - velocity.y);

            return shove;
        }

        static bool TryFindFurniture(Bounds body, Vector2 direction, float reach,
            out Vector2 nearestPoint)
        {
            InTheWay.Clear();
            nearestPoint = body.center;
            float nearest = float.MaxValue;

            Physics2D.BoxCast(body.center, ShrinkForSweep(body.size), 0f, direction,
                ContactFilter2D.noFilter, Hits, reach);

            foreach (RaycastHit2D hit in Hits)
            {
                Furniture piece = hit.rigidbody != null ? hit.rigidbody.GetComponent<Furniture>() : null;

                if (piece == null || InTheWay.Contains(piece))
                    continue;

                InTheWay.Add(piece);

                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    nearestPoint = hit.point;
                }
            }

            return InTheWay.Count > 0;
        }

        void ShowHand(Swing swing)
        {
            if (handPrefab == null)
                return;

            bool isMirrored = swing.direction.x < -LeastAimPart;
            GameObject hand = Instantiate(handPrefab, swing.handPoint, TurnToward(swing.direction, isMirrored));
            hand.name = handPrefab.name;

            if (isMirrored)
            {
                Vector3 scale = hand.transform.localScale;
                hand.transform.localScale = new Vector3(-scale.x, scale.y, scale.z);
            }

            Destroy(hand, handSeconds);
        }

        static Quaternion TurnToward(Vector2 direction, bool isMirrored)
        {
            Vector2 drawn = isMirrored ? -direction : direction;
            return Quaternion.Euler(0f, 0f, Mathf.Atan2(drawn.y, drawn.x) * Mathf.Rad2Deg);
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("How far the hand reaches at this rank, in boxes, measured from the edge of " +
                     "the wizard. A wall, ceiling or piece of furniture further away than this " +
                     "cannot be slapped, so the button waits a moment for one to come into reach " +
                     "and the HUD greys while there is none.")]
            [Min(ShortestReach)] public float reach = 2f;

            public void Validate() => reach = Mathf.Max(ShortestReach, reach);
        }

        struct Swing
        {
            public Vector2 direction;
            public Vector2 handPoint;
            public Vector2 shove;
            public bool pushesOff;
            public bool hasFurniture;

            public bool IsWorthIt => pushesOff || hasFurniture;
        }
    }
}
