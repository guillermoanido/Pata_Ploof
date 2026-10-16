using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Haste", fileName = "Haste")]
    public class HasteAbility : Ability
    {
        const string TrailName = "Haste Trail";

        [Header("The Wizard")]
        [Tooltip("Top speed while hasted, against a normal run.")]
        [Min(1f)] public float moveSpeed = 1.5f;

        [Tooltip("How hard the stick bites, so the speed is reachable rather than something you " +
                 "slide up to over a second.")]
        [Min(1f)] public float control = 1.6f;

        [Header("The World")]
        [Tooltip("How fast everything ELSE runs while this is up. 0.35 is a third speed, which " +
                 "is enough to walk into a gale or across a moving hazard that would otherwise " +
                 "have the timing on you. The wizard's own ragdoll is never scaled by this.")]
        [Range(0.05f, 1f)] public float worldScale = 0.35f;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal || wizard.State == PlayerState.OnVine;

        public override string WhyNot(PlayerLogic wizard) =>
            CanCast(wizard) ? null : $"you are {wizard.State}";

        public override bool OnCast(PlayerLogic wizard)
        {
            Tier tier = Of(wizard);

            Haste.Begin(tier.worldScale);
            ShowTrail(wizard, true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            Tier tier = Of(wizard);

            stats.MoveSpeedMultiplier *= tier.moveSpeed;
            stats.AirControlMultiplier *= control;
        }

        public override void OnEquipped(PlayerLogic wizard) => ShowTrail(wizard, false);

        public override void OnEnded(PlayerLogic wizard) => Stop(wizard);

        public override void OnRunReset(PlayerLogic wizard) => Stop(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => Stop(wizard);

        protected override void Validate()
        {
            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        void Stop(PlayerLogic wizard)
        {
            Haste.End();
            ShowTrail(wizard, false);
        }

        void ShowTrail(PlayerLogic wizard, bool shown)
        {
            GameObject effect = TrailOf(wizard);

            if (effect == null)
                return;

            effect.SetActive(shown);

            if (shown)
                ForgetOldTrailPoints(effect);
        }

        static void ForgetOldTrailPoints(GameObject effect)
        {
            foreach (TrailRenderer streak in effect.GetComponentsInChildren<TrailRenderer>())
                streak.Clear();
        }

        GameObject TrailOf(PlayerLogic wizard)
        {
            Trail trail = wizard.spellbook.StateOf<Trail>(this);

            if (trail.searched || wizard.Rig == null)
                return trail.effect;

            trail.searched = true;
            trail.effect = FindChildNamed(wizard.Rig, TrailName);

            if (trail.effect == null)
                Debug.LogWarning($"'{name}' found nothing called '{TrailName}' under the " +
                                 "wizard, so Haste will show no trail. Place your trail effect " +
                                 "under the wizard with exactly that name.", this);

            return trail.effect;
        }

        [System.Serializable]
        public class Tier
        {
            [Tooltip("Top speed while hasted at this rank, against a normal run.")]
            [Min(1f)] public float moveSpeed = 1.5f;

            [Tooltip("How fast everything else runs at this rank while Haste is up.")]
            [Range(0.05f, 1f)] public float worldScale = 0.35f;

            public void Validate()
            {
                moveSpeed = Mathf.Max(1f, moveSpeed);
                worldScale = Mathf.Clamp(worldScale, 0.05f, 1f);
            }
        }

        public class Trail
        {
            public GameObject effect;
            public bool searched;
        }
    }
}
