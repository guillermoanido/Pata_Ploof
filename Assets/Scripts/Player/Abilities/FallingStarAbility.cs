using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Falling Star", fileName = "Falling Star")]
    public class FallingStarAbility : Ability
    {
        const string TrailName = "Falling Star Trail";

        const float ShallowestDive = 10f;
        const float SteepestDive = 80f;
        const float SlowestDive = 1f;
        const float ShallowestDepth = 0.5f;

        [Header("Dive")]
        [Tooltip("How steeply the dive goes down, in degrees below level. 45 is a true diagonal: " +
                 "a two tile dive also travels two tiles sideways.")]
        [Range(ShallowestDive, SteepestDive)] public float diveAngle = 45f;

        [Tooltip("Speed along the dive, in boxes per second. At 12 a two tile dive is over in " +
                 "about a quarter of a second, so the ground has to be close when you cast it. " +
                 "Faster is harder to land and hurts more when it misses.")]
        [Min(SlowestDive)] public float diveSpeed = 12f;

        [Tooltip("How far down the dive reaches, in boxes. This IS the window: the ground has to " +
                 "be within this many tiles below you when you cast, or the dive gives up there " +
                 "and you go on tumbling, and the landing hurts as normal. Smaller is a tighter, " +
                 "harder save. It is the same at every rank: upgrades add charges instead, set " +
                 "with Uses Per Level and Uses Added Per Rank.")]
        [Min(ShallowestDepth)] public float diveDepth = 1.25f;

        [Header("Bounce")]
        [Tooltip("How high a dive that reaches the ground throws the wizard back up, in boxes. " +
                 "The fall is counted again from the top of the bounce, so keep this at or under " +
                 "the wizard's Safe Fall Distance (3) and coming back down to the same floor is " +
                 "free.")]
        [Min(0f)] public float bounceHeight = 2f;

        [Tooltip("Sideways speed the bounce carries on with, in boxes per second, the way the " +
                 "dive was going.")]
        [Min(0f)] public float bounceOnward = 2f;

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Ragdoll && !wizard.IsDiving;

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.IsDiving)
                return "you are already diving";

            return wizard.State == PlayerState.Ragdoll
                ? null
                : "it only works while you are tumbling";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            int way = PickTheWay(wizard);
            var bounce = new PlayerLogic.Ragdoll.Bounce(bounceHeight, way * bounceOnward);

            if (!wizard.TryDive(FindDiveVelocity(way), diveDepth, bounce))
                return false;

            ShowTrail(wizard, true);
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            if (!wizard.IsDiving)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) => ShowTrail(wizard, false);

        public override void OnEquipped(PlayerLogic wizard) => ShowTrail(wizard, false);

        protected override void Validate() => diveDepth = Mathf.Max(ShallowestDepth, diveDepth);

        protected override void CleanUp(PlayerLogic wizard) => ShowTrail(wizard, false);

        static int PickTheWay(PlayerLogic wizard)
        {
            PlayerLogic.Movement movement = wizard.movement;
            float steer = wizard.Steering.Move.x;

            if (Mathf.Abs(steer) > movement.steerDeadzone)
                return PlayerLogic.Movement.GetDirectionOf(steer);

            return movement.HorizontalSpeed > movement.steerDeadzone
                ? PlayerLogic.Movement.GetDirectionOf(movement.Velocity.x)
                : movement.Facing;
        }

        Vector2 FindDiveVelocity(int way)
        {
            float radians = diveAngle * Mathf.Deg2Rad;

            return new Vector2(way * Mathf.Cos(radians), -Mathf.Sin(radians)) * diveSpeed;
        }

        void ShowTrail(PlayerLogic wizard, bool isShown) =>
            wizard.spellbook.StateOf<Trail>(this).effect.Show(wizard, this, isShown);

        class Trail
        {
            public readonly WizardEffect effect = new WizardEffect(TrailName);
        }
    }
}
