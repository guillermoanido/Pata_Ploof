using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        const string CanopyName = "Glide Canopy";

        const float SlowestFallSpeed = 0.2f;

        const float LeastAirControl = 0.5f;

        [Header("Reach")]
        [Tooltip("How fast you slow down in the air with NO stick held, against normal. Below 1 " +
                 "you coast, which is the point of a wing: a canopy that braked harder than bare " +
                 "air would drop you straight down the moment you stopped steering, which is the " +
                 "opposite of gliding.")]
        [Range(0.1f, 1f)] public float drag = 0.45f;

        [Header("Folding")]
        [Tooltip("Touching down folds it and starts the cooldown.")]
        public bool foldsOnLanding = true;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
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
            Tier tier = PickTier(tiers, wizard);

            stats.FallSpeedMultiplier *= tier.fallSpeed;
            stats.AirSpeedMultiplier *= tier.airSpeed;
            stats.AirControlMultiplier *= tier.airControl;
            stats.AirDragMultiplier *= drag;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

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

        public override void OnRunReset(PlayerLogic wizard) => ShowCanopy(wizard, false);

        public override void OnUnequipped(PlayerLogic wizard) => ShowCanopy(wizard, false);

        protected override void Validate() => ValidateTiers(tiers);

        void ShowCanopy(PlayerLogic wizard, bool shown)
        {
            GameObject canopy = FindCanopy(wizard);

            if (canopy != null)
                canopy.SetActive(shown);
        }

        GameObject FindCanopy(PlayerLogic wizard)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (wing.hasSearched || wizard.Rig == null)
                return wing.canopy;

            wing.hasSearched = true;
            wing.canopy = FindChildNamed(wizard.Rig, CanopyName);

            if (wing.canopy == null)
                Debug.LogWarning($"'{name}' found nothing called '{CanopyName}' under the " +
                                 "wizard, so the canopy will not show. Place your canopy art " +
                                 "under the wizard's Visual object with exactly that name.", this);

            return wing.canopy;
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("Fall speed with the canopy out at this rank, as a fraction of normal.")]
            [Range(SlowestFallSpeed, 1f)] public float fallSpeed = 0.5f;

            [Tooltip("Top sideways speed in the air at this rank, against a run.")]
            [Min(1f)] public float airSpeed = 1.9f;

            [Tooltip("How hard the stick bites in the air at this rank.")]
            [Min(LeastAirControl)] public float airControl = 1.6f;

            public void Validate()
            {
                fallSpeed = Mathf.Clamp(fallSpeed, SlowestFallSpeed, 1f);
                airSpeed = Mathf.Max(1f, airSpeed);
                airControl = Mathf.Max(LeastAirControl, airControl);
            }
        }

        class Wing
        {
            public GameObject canopy;
            public bool hasSearched;
            public bool hasFlown;
        }
    }
}
