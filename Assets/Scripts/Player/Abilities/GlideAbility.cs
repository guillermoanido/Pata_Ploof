using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        const string CanopyName = "Glide Canopy";

        [Header("Descent")]
        [Tooltip("Fall speed with the canopy out, as a fraction of normal. Set it as low as " +
                 "reads well: fall damage counts BOXES fallen rather than how fast you were " +
                 "going, so however slow this makes the descent, the drop bills you exactly the " +
                 "same. It cannot turn this into a survival tool. It is also where the REACH " +
                 "comes from: distance across a fall is airtime times sideways speed, so halving " +
                 "the descent is what gives the wing its arc. At 0.8 the spell was just a sprint " +
                 "that happened in the air.")]
        [Range(0.2f, 1f)] public float fallSpeed = 0.5f;

        [Header("Reach")]
        [Tooltip("Top sideways speed in the air, against a run. About 1.9 turns a running jump " +
                 "from a bit over two boxes into nearly four. It does nothing on the ground - a " +
                 "canopy needs air under it.")]
        [Min(1f)] public float airSpeed = 1.9f;

        [Tooltip("How hard the stick bites in the air. Steering only - it does not touch how " +
                 "fast you coast to a stop.")]
        [Min(0.5f)] public float airControl = 1.6f;

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
            wizard.spellbook.StateOf<Wing>(this).flown = false;
            ShowCanopy(wizard, true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            Tier tier = Of(wizard);

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
                wing.flown = true;
                return;
            }

            if (foldsOnLanding && wing.flown)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) => ShowCanopy(wizard, false);

        public override void OnEquipped(PlayerLogic wizard) => ShowCanopy(wizard, false);

        public override void OnRunReset(PlayerLogic wizard) => ShowCanopy(wizard, false);

        public override void OnUnequipped(PlayerLogic wizard) => ShowCanopy(wizard, false);

        protected override void Validate()
        {
            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        void ShowCanopy(PlayerLogic wizard, bool shown)
        {
            GameObject canopy = CanopyOf(wizard);

            if (canopy != null)
                canopy.SetActive(shown);
        }

        GameObject CanopyOf(PlayerLogic wizard)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (wing.searched || wizard.Rig == null)
                return wing.canopy;

            wing.searched = true;
            wing.canopy = FindChildNamed(wizard.Rig, CanopyName);

            if (wing.canopy == null)
                Debug.LogWarning($"'{name}' found nothing called '{CanopyName}' under the " +
                                 "wizard, so the canopy will not show. Place your canopy art " +
                                 "under the wizard's Visual object with exactly that name.", this);

            return wing.canopy;
        }

        [System.Serializable]
        public class Tier
        {
            [Tooltip("Fall speed with the canopy out at this rank, as a fraction of normal.")]
            [Range(0.2f, 1f)] public float fallSpeed = 0.5f;

            [Tooltip("Top sideways speed in the air at this rank, against a run.")]
            [Min(1f)] public float airSpeed = 1.9f;

            [Tooltip("How hard the stick bites in the air at this rank.")]
            [Min(0.5f)] public float airControl = 1.6f;

            public void Validate()
            {
                fallSpeed = Mathf.Clamp(fallSpeed, 0.2f, 1f);
                airSpeed = Mathf.Max(1f, airSpeed);
                airControl = Mathf.Max(0.5f, airControl);
            }
        }

        public class Wing
        {
            public GameObject canopy;
            public bool searched;
            public bool flown;
        }
    }
}
