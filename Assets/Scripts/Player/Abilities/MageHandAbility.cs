using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Mage Hand", fileName = "Mage Hand")]
    public class MageHandAbility : Ability
    {
        const float ShortestGrasp = 0.1f;

        const float LongestGrasp = 1.2f;

        const float WidestSwing = 89f;

        [Header("Grasp")]
        [Tooltip("How far a grab is allowed to MOVE you, in boxes, measured to the point on the " +
                 "arc you would actually end up at. One box is one mage. Keep it under about 1.2: " +
                 "above that no grab can finish inside a single physics step, and the swing " +
                 "reads its own overshoot as having hit a wall and throws your speed away.")]
        [Range(ShortestGrasp, LongestGrasp)] public float graspRange = 1f;

        [Tooltip("Pressing again while hanging lets go. Off means only Jump lets go, which frees " +
                 "the button up but makes a mistimed grab harder to undo.")]
        public bool pressAgainToLetGo = true;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return pressAgainToLetGo;

            if (!wizard.CanGrabVine)
                return false;

            MagicHand vine = NearestFreeHand(wizard);

            return vine != null &&
                   wizard.GrabSnapDistance(DescribeHold(wizard, vine)) <= graspRange;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return "'press again to let go' is switched off on this spell";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            if (!wizard.CanGrabVine)
                return "you only just let go of one";

            return WhyNoVineIsInReach(wizard);
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
            {
                LetGo(wizard);
                return true;
            }

            MagicHand vine = NearestFreeHand(wizard);

            if (vine == null || !wizard.TryGrabVine(DescribeHold(wizard, vine)))
                return false;

            vine.CallDown();

            wizard.spellbook.StateOf<Grip>(this).held = vine;
            return true;
        }

        protected override void Validate()
        {
            graspRange = Mathf.Clamp(graspRange, ShortestGrasp, LongestGrasp);

            ValidateTiers(tiers);
        }

        protected override void CleanUp(PlayerLogic wizard) => LetGo(wizard);

        PlayerLogic.Vine.Hold DescribeHold(PlayerLogic wizard, MagicHand vine)
        {
            Tier tier = PickTier(tiers, wizard);

            return new PlayerLogic.Vine.Hold
            {
                Anchor = vine.Knot,
                Length = vine.length,

                MaxSwingDegrees = Mathf.Min(vine.maxSwing, tier.maxSwing),
                ClimbSpeed = tier.climbSpeed,
                SnapLimit = graspRange,
            };
        }

        string WhyNoVineIsInReach(PlayerLogic wizard)
        {
            MagicHand vine = NearestFreeHand(wizard);

            if (vine == null)
                return MagicHand.All.Count == 0
                    ? "there is not a single Magic Hand in this scene"
                    : "no Magic Hand is close enough that you did not just let go of - look for one that has started glowing";

            float snap = wizard.GrabSnapDistance(DescribeHold(wizard, vine));

            if (snap > graspRange)
                return $"the hand would have to drag you {snap:0.0} boxes to reach it - get " +
                       $"level with the rope rather than standing over where it is tied";

            return null;
        }

        MagicHand NearestFreeHand(PlayerLogic wizard)
        {
            Grip grip = wizard.spellbook.StateOf<Grip>(this);
            MagicHand resting = Time.time < grip.freeAgainAt ? grip.letGoOf : null;

            return MagicHand.Nearest(wizard.movement.Position, resting);
        }

        void LetGo(PlayerLogic wizard)
        {
            Grip grip = wizard.spellbook.StateOf<Grip>(this);

            if (grip.held != null)
            {
                grip.held.RollUp();
                grip.letGoOf = grip.held;
                grip.freeAgainAt = Time.time + PickTier(tiers, wizard).sameHandCooldown;
                grip.held = null;
            }

            if (wizard.IsOnVine)
                wizard.LetGoOfVine();
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("How far the hand leans either side of straight down, in degrees. The vine " +
                     "and the wizard both keep their own ceiling; the smallest of the three wins.")]
            [Range(0f, WidestSwing)] public float maxSwing = 30f;

            [Tooltip("Climbing up and down, in boxes per second. 0 means you cannot - that is " +
                     "rank 1, and unlocking it is the upgrade.")]
            [Min(0f)] public float climbSpeed = 0f;

            [Tooltip("Seconds after letting go of a Magic Hand before the spell can grab that same " +
                     "hand again. Any other Magic Hand in reach can be grabbed at once. 0 lets " +
                     "you catch the same one straight back, which is the last rank's reward.")]
            [Min(0f)] public float sameHandCooldown = 2f;

            public void Validate()
            {
                maxSwing = Mathf.Clamp(maxSwing, 0f, WidestSwing);
                climbSpeed = Mathf.Max(0f, climbSpeed);
                sameHandCooldown = Mathf.Max(0f, sameHandCooldown);
            }
        }

        class Grip
        {
            public MagicHand held;
            public MagicHand letGoOf;
            public float freeAgainAt;
        }
    }
}
