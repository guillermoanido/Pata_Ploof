using FallingWizard.Core;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Fling", fileName = "Fling")]
    public class FlingAbility : Ability
    {
        const float StickDeadzone = 0.2f;

        const float SteepestAim = 45f;

        [Header("Aim")]
        [Tooltip("Where the arrow points when you start holding, in degrees above level. " +
                 "Holding up or down turns it from here.")]
        [Range(-SteepestAim, SteepestAim)] public float startAngle = 20f;

        [Tooltip("Lowest the arrow can point, in degrees. Below 0 points down; set it to 0 so " +
                 "the wizard is never thrown downward.")]
        [Range(-SteepestAim, SteepestAim)] public float lowestAngle = -45f;

        [Tooltip("Highest the arrow can point, in degrees. It stops at 45 so a fling is a throw " +
                 "across, never a jump.")]
        [Range(-SteepestAim, SteepestAim)] public float highestAngle = 45f;

        [Tooltip("How fast holding up or down turns the arrow, in degrees per second.")]
        [Min(0f)] public float aimSpeed = 90f;

        [Header("The Look Ahead")]
        [Tooltip("What the predicted throw notices. Ground so it knows where you land, Hazard " +
                 "so the arrow can warn you that the throw would carry you through one.")]
        public LayerMask seen = Layers.GroundMask | Layers.HazardMask;

        [Tooltip("Seconds per simulated step. Smaller is smoother and more accurate.")]
        [Min(PlayerLogic.Movement.MinArcStep)] public float step = 0.02f;

        [Tooltip("Most steps to simulate, whatever else happens.")]
        [Range(8, 400)] public int steps = 200;

        [Tooltip("How far ahead to look, in boxes. The warning only covers this much of the " +
                 "throw.")]
        [Min(1f)] public float lookAhead = 16f;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override string WhyNot(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal ? null : $"you are {wizard.State}";

        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            if (wizard.spellbook.StateOf<Aiming>(this).isAiming)
                stats.IsRooted = true;
        }

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            if (wizard.State != PlayerState.Normal)
            {
                StopAiming(wizard);
                return;
            }

            Aiming aiming = wizard.spellbook.StateOf<Aiming>(this);

            if (!aiming.isAiming)
                StartAiming(aiming);

            TurnTheAim(wizard, aiming, fixedDeltaTime);
            ShowArrow(wizard, aiming);
        }

        public override void OnChargeLost(PlayerLogic wizard) => StopAiming(wizard);

        public override void OnReleased(PlayerLogic wizard, float heldSeconds)
        {
            Aiming aiming = wizard.spellbook.StateOf<Aiming>(this);

            if (!aiming.isAiming)
                return;

            StopAiming(wizard);

            if (wizard.State != PlayerState.Normal || !wizard.spellbook.TryFire(this))
                return;

            wizard.TryFling(CalculateLaunch(wizard, aiming));
        }

        public override void OnEquipped(PlayerLogic wizard) =>
            HideArrow(wizard, wizard.spellbook.StateOf<Aiming>(this));

        public override void OnRunReset(PlayerLogic wizard) => StopAiming(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => StopAiming(wizard);

        protected override void Validate()
        {
            highestAngle = Mathf.Max(lowestAngle, highestAngle);
            startAngle = Mathf.Clamp(startAngle, lowestAngle, highestAngle);

            ValidateTiers(tiers);
        }

        void StartAiming(Aiming aiming)
        {
            aiming.isAiming = true;
            aiming.angle = startAngle;
        }

        void TurnTheAim(PlayerLogic wizard, Aiming aiming, float fixedDeltaTime)
        {
            float upOrDown = wizard.Steering.Move.y;

            if (Mathf.Abs(upOrDown) < StickDeadzone)
                return;

            float turned = aiming.angle + upOrDown * aimSpeed * fixedDeltaTime;
            aiming.angle = Mathf.Clamp(turned, lowestAngle, highestAngle);
        }

        Vector2 CalculateLaunch(PlayerLogic wizard, Aiming aiming)
        {
            float radians = aiming.angle * Mathf.Deg2Rad;
            int facing = wizard.movement.Facing;

            var direction = new Vector2(facing * Mathf.Cos(radians), Mathf.Sin(radians));
            return direction * PickTier(tiers, wizard).speed;
        }

        PlayerLogic.Movement.ArcSettings DescribeLookAhead() =>
            new PlayerLogic.Movement.ArcSettings
            {
                Layers = seen,
                Step = step,
                Steps = steps,
                Distance = lookAhead,
            };

        void ShowArrow(PlayerLogic wizard, Aiming aiming)
        {
            FlingArrow arrow = FindArrow(wizard, aiming);

            if (arrow == null)
                return;

            Vector2 launch = CalculateLaunch(wizard, aiming);
            bool isDangerous = wizard.WouldFlingThroughHazard(launch, DescribeLookAhead());

            arrow.Show(wizard.movement.Position, launch, isDangerous);
        }

        void HideArrow(PlayerLogic wizard, Aiming aiming)
        {
            FlingArrow arrow = FindArrow(wizard, aiming);

            if (arrow != null)
                arrow.Hide();
        }

        FlingArrow FindArrow(PlayerLogic wizard, Aiming aiming)
        {
            if (aiming.hasSearched || wizard.Rig == null)
                return aiming.arrow;

            aiming.hasSearched = true;
            aiming.arrow = wizard.Rig.GetComponentInChildren<FlingArrow>(true);

            if (aiming.arrow == null)
                Debug.LogWarning($"'{name}' found no Fling Arrow under the wizard, so aiming " +
                                 "shows no arrow. Add the Fling Arrow component to your arrow " +
                                 "object and place it under the wizard.", this);

            return aiming.arrow;
        }

        void StopAiming(PlayerLogic wizard)
        {
            Aiming aiming = wizard.spellbook.StateOf<Aiming>(this);

            aiming.isAiming = false;
            HideArrow(wizard, aiming);
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("How fast the throw sends the wizard at this rank, in boxes per second. A " +
                     "standing jump leaves at about 9, for scale.")]
            [Min(0f)] public float speed = 9f;

            public void Validate() => speed = Mathf.Max(0f, speed);
        }

        class Aiming
        {
            public FlingArrow arrow;
            public bool hasSearched;
            public bool isAiming;
            public float angle;
        }
    }
}
