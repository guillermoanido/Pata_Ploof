using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        const string CanopyName = "Glide Canopy";

        const float SlowestFallSpeed = 0.2f;

        const float LeastAirControl = 0.5f;

        [Header("Steering")]
        [Tooltip("Top sideways speed in the air with the canopy out, against a run. The same at " +
                 "every rank: the better control is what learning Glide gives you.")]
        [Min(1f)] public float airSpeed = 1.9f;

        [Tooltip("How hard the stick bites in the air with the canopy out. The same at every rank.")]
        [Min(LeastAirControl)] public float airControl = 1.6f;

        [Tooltip("How fast you slow down in the air with NO stick held, against normal. Below 1 " +
                 "you coast, which is the point of a wing: a canopy that braked harder than bare " +
                 "air would drop you straight down the moment you stopped steering, which is the " +
                 "opposite of gliding.")]
        [Range(0.1f, 1f)] public float drag = 0.45f;

        [Header("Folding")]
        [Tooltip("Touching down folds it and starts the cooldown.")]
        public bool foldsOnLanding = true;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you. At every rank, a " +
                 "wind catching the open canopy starts the fall over from that height, so the " +
                 "drop is measured from where the wizard leaves the wind.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) => wizard.State == PlayerState.Normal;

        public override string WhyNot(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal ? null : $"you are {wizard.State}";

        public override bool OnCast(PlayerLogic wizard)
        {
            wizard.spellbook.StateOf<Wing>(this).hasFlown = false;
            ShowCanopy(wizard, true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            stats.FallSpeedMultiplier *= PickTier(tiers, wizard).fallSpeed;
            stats.AirSpeedMultiplier *= airSpeed;
            stats.AirControlMultiplier *= airControl;
            stats.AirDragMultiplier *= drag;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (wizard.IsInWind)
                wizard.RestartFall();

            if (!wizard.movement.IsGrounded)
            {
                wing.hasFlown = true;
                return;
            }

            if (foldsOnLanding && wing.hasFlown)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) => ShowCanopy(wizard, false);

        public override void OnEquipped(PlayerLogic wizard) => ShowCanopy(wizard, false);

        protected override void Validate()
        {
            airSpeed = Mathf.Max(1f, airSpeed);
            airControl = Mathf.Max(LeastAirControl, airControl);
            ValidateTiers(tiers);
        }

        protected override void CleanUp(PlayerLogic wizard) => ShowCanopy(wizard, false);

        void ShowCanopy(PlayerLogic wizard, bool isShown) =>
            wizard.spellbook.StateOf<Wing>(this).canopy.Show(wizard, this, isShown);

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("Fall speed with the canopy out at this rank, as a fraction of normal. It " +
                     "slows both how fast the fall picks up speed and how fast it can get: 1 does " +
                     "not slow the fall at all, 0.2 is a slow, steady sink. The fall is still " +
                     "measured by height, so a long drop hurts however slowly it is taken.")]
            [Range(SlowestFallSpeed, 1f)] public float fallSpeed = 1f;

            public void Validate() => fallSpeed = Mathf.Clamp(fallSpeed, SlowestFallSpeed, 1f);
        }

        class Wing
        {
            public readonly WizardEffect canopy = new WizardEffect(CanopyName);
            public bool hasFlown;
        }
    }
}
