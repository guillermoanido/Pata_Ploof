using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Haste", fileName = "Haste")]
    public class HasteAbility : Ability
    {
        const string TrailName = "Haste Trail";

        [Header("The Wizard")]
        [Tooltip("How hard the stick bites, so the speed is reachable rather than something you " +
                 "slide up to over a second.")]
        [Min(1f)] public float control = 1.6f;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal || wizard.State == PlayerState.OnVine;

        public override string WhyNot(PlayerLogic wizard) =>
            CanCast(wizard) ? null : $"you are {wizard.State}";

        public override bool OnCast(PlayerLogic wizard)
        {
            Tier tier = PickTier(tiers, wizard);

            Haste.Begin(tier.worldScale);
            ShowTrail(wizard, true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            Tier tier = PickTier(tiers, wizard);

            stats.MoveSpeedMultiplier *= tier.moveSpeed;
            stats.AirControlMultiplier *= control;
        }

        public override void OnEquipped(PlayerLogic wizard) => ShowTrail(wizard, false);

        public override void OnEnded(PlayerLogic wizard) => StopHaste(wizard);

        protected override void Validate() => ValidateTiers(tiers);

        protected override void CleanUp(PlayerLogic wizard) => StopHaste(wizard);

        void StopHaste(PlayerLogic wizard)
        {
            Haste.End();
            ShowTrail(wizard, false);
        }

        void ShowTrail(PlayerLogic wizard, bool isShown)
        {
            GameObject effect = wizard.spellbook.StateOf<Trail>(this).effect.Find(wizard, this);

            if (effect == null)
                return;

            effect.SetActive(isShown);

            if (isShown)
                ForgetOldTrailPoints(effect);
        }

        static void ForgetOldTrailPoints(GameObject effect)
        {
            foreach (TrailRenderer streak in effect.GetComponentsInChildren<TrailRenderer>())
                streak.Clear();
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("Top speed while hasted at this rank, against a normal run.")]
            [Min(1f)] public float moveSpeed = 1.5f;

            [Tooltip("How fast everything else runs at this rank while Haste is up.")]
            [Range(Haste.SlowestWorldScale, 1f)] public float worldScale = 0.35f;

            public void Validate()
            {
                moveSpeed = Mathf.Max(1f, moveSpeed);
                worldScale = Mathf.Clamp(worldScale, Haste.SlowestWorldScale, 1f);
            }
        }

        class Trail
        {
            public readonly WizardEffect effect = new WizardEffect(TrailName);
        }
    }
}
