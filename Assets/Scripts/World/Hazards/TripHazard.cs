using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class TripHazard : Hazard
    {
        const float TripCooldown = 1f;

        const float ReversalKick = 4f;

        const float OffCentre = 0.05f;

        const int Leftward = -1;
        const int Rightward = 1;

        [Header("Trip")]
        [Tooltip("Which way this trips the wizard. Way They Run trips them onward, the way they " +
                 "were already going, which is right for a wet floor or a staircase. Left and " +
                 "Right always throw them that way, and with Affects Ragdolled on they also turn " +
                 "round a wizard who is already tumbling. Way It Faces reads the prefab's own " +
                 "flip - the side its trigger sits on, then the art's mirroring - so flipping a " +
                 "copy is all it takes to make the mirrored version.")]
        public Aim aim = Aim.WayTheyRun;

        [Tooltip("Shove along the aim, in boxes per second, on top of the ordinary trip. This is " +
                 "what makes a rake a launcher rather than a stumble. 0 is a plain trip.")]
        [Min(0f)] public float extraKick = 0f;

        [Tooltip("Extra lift on top of the trip's own, in boxes per second. Raise it for a rake " +
                 "that should pop the wizard up and over something rather than skim them along.")]
        [Min(0f)] public float extraLift = 0f;

        protected override void Reset()
        {
            base.Reset();

            minimumSpeed = RunningOnly;
            rearmDelay = TripCooldown;
        }

        protected override void Affect(PlayerLogic wizard)
        {
            int way = ChooseTheWay(wizard);

            if (wizard.TryTrip(way))
                KickOnward(wizard, way);
            else if (aim != Aim.WayTheyRun && wizard.State == PlayerState.Ragdoll)
                TurnTheTumbleAround(wizard, way);
        }

        int ChooseTheWay(PlayerLogic wizard)
        {
            switch (aim)
            {
                case Aim.Left:
                    return Leftward;
                case Aim.Right:
                    return Rightward;
                case Aim.WayItFaces:
                    return FindTheWayItFaces();
                default:
                    return wizard.movement.TravelDirection;
            }
        }

        int FindTheWayItFaces()
        {
            var hitbox = GetComponent<Collider2D>();

            if (hitbox != null && Mathf.Abs(hitbox.offset.x) > OffCentre)
                return hitbox.offset.x < 0f ? Leftward : Rightward;

            return IsMirrored() ? Leftward : Rightward;
        }

        bool IsMirrored()
        {
            if (transform.lossyScale.x < 0f)
                return true;

            foreach (Transform child in transform)
                if (child.localScale.x < 0f)
                    return true;

            return false;
        }

        void KickOnward(PlayerLogic wizard, int way)
        {
            if (extraKick > 0f || extraLift > 0f)
                wizard.Shove(new Vector2(way * extraKick, extraLift), 0f);
        }

        void TurnTheTumbleAround(PlayerLogic wizard, int way)
        {
            float carried = wizard.movement.Velocity.x;
            float target = way * Mathf.Max(extraKick, ReversalKick);

            wizard.Shove(new Vector2(target - carried, extraLift), 0f);
        }

        public enum Aim
        {
            WayTheyRun,
            Left,
            Right,
            WayItFaces,
        }
    }
}
