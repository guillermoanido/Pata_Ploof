using System;
using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(50)]
    public class PlayerAnimator : MonoBehaviour
    {
        public const int ModeGround = 0;
        public const int ModeStaff = 1;
        public const int ModeRagdoll = 2;
        public const int ModeDead = 3;

        const int NoHat = -1;

        static readonly int ModeId = Animator.StringToHash("Mode");
        static readonly int GroundedId = Animator.StringToHash("Grounded");
        static readonly int MovingId = Animator.StringToHash("Moving");
        static readonly int WalkingId = Animator.StringToHash("Walking");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ClimbRateId = Animator.StringToHash("ClimbRate");
        static readonly int ClimbUpId = Animator.StringToHash("ClimbUp");
        static readonly int ClimbDownId = Animator.StringToHash("ClimbDown");
        static readonly int RagdollVariantId = Animator.StringToHash("RagdollVariant");
        static readonly int HurtId = Animator.StringToHash("Hurt");
        static readonly int HatChangedId = Animator.StringToHash("HatChanged");

        [Header("Ragdoll")]
        [Tooltip("How many ragdoll reactions to build and cycle through. Rebuild after changing.")]
        [Min(1)] public int ragdollVariants = 4;

        [Header("Locomotion")]
        [Tooltip("Speed below which the wizard counts as standing still, in boxes per second.")]
        [Min(0f)] public float standStillSpeed = 0.1f;

        [Tooltip("Min and max cycle-rate multiplier for Walk and Run.")]
        public Vector2 cycleRateRange = new Vector2(0.5f, 1.5f);

        [Header("Staff")]
        [Tooltip("Hide the separate staff art while carried, for art that draws it into the character.")]
        public bool staffIsDrawnIntoTheArt = false;

        [Header("Hat")]
        [Tooltip("The hat shows how much health the wizard has left. One entry per look, listed " +
                 "from the healthiest hat to the most damaged. Leave it empty and the wizard " +
                 "looks the same at any health.")]
        public HatState[] hatStates = new HatState[0];

        Animator animator;
        RuntimeAnimatorController normalAnimations;
        PlayerCharacter character;
        SpriteRenderer staffArt;

        int lastHealth;
        int wornHat = NoHat;
        int variant;
        bool wasOnStaff;
        bool wasRagdolling;
        bool wasRagdollGrounded;

        void Awake()
        {
            animator = GetComponent<Animator>();
            normalAnimations = animator.runtimeAnimatorController;
            character = GetComponent<PlayerCharacter>();

            if (character == null)
            {
                Debug.LogError($"'{name}' has a PlayerAnimator but no PlayerCharacter beside it, " +
                               "so there is no wizard to read. Put them on the same object.", this);
                enabled = false;
                return;
            }

            Staff staff = GetComponentInChildren<Staff>(true);
            staffArt = staff != null ? staff.GetComponentInChildren<SpriteRenderer>(true) : null;
        }

        void Start()
        {
            PlayerLogic.Health health = character.Logic.health;

            lastHealth = health.Current;
            PutOnHat(ChooseHat(health.Percent));
        }

        void Update()
        {
            PlayerLogic wizard = character.Logic;
            PlayerLogic.Movement walk = wizard.movement;

            ShowHealth(wizard.health);

            int mode = !wizard.health.IsAlive ? ModeDead
                     : wizard.State == PlayerState.Ragdoll ? ModeRagdoll
                     : wizard.State == PlayerState.OnStaff ? ModeStaff
                     : ModeGround;

            bool grounded = wizard.State != PlayerState.OnVine && walk.IsGrounded;
            bool moving = mode == ModeGround && grounded && walk.HorizontalSpeed > standStillSpeed;

            bool walking = wizard.Steering.Walk;

            animator.SetInteger(ModeId, mode);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(MovingId, moving);
            animator.SetBool(WalkingId, walking);
            animator.SetFloat(SpeedId, CycleRate(wizard, walk, walking));
            animator.SetFloat(ClimbRateId, ClimbRate(wizard, mode));

            WatchForAClimbStarting(wizard, mode);
            WatchForATumble(mode, walk);
            ShowTheStaffWhenThePoleOwnsIt(wizard);
        }

        float CycleRate(PlayerLogic wizard, PlayerLogic.Movement walk, bool walking)
        {
            float top = (walking ? walk.walkSpeed : walk.runSpeed) * wizard.Stats.MoveSpeedMultiplier;

            if (top <= Mathf.Epsilon)
                return 1f;

            return Mathf.Clamp(walk.HorizontalSpeed / top, cycleRateRange.x, cycleRateRange.y);
        }

        float ClimbRate(PlayerLogic wizard, int mode)
        {
            if (mode != ModeStaff || !wizard.HasPole)
                return 0f;

            Staff.Pole pole = wizard.Pole;
            float lean = wizard.Steering.Lean;

            if (Mathf.Abs(lean) <= pole.leanThreshold)
                return 0f;

            if ((lean > 0f && pole.AtTop) || (lean < 0f && pole.AtBottom))
                return 0f;

            return Mathf.Sign(lean);
        }

        void WatchForAClimbStarting(PlayerLogic wizard, int mode)
        {
            bool onStaff = mode == ModeStaff;

            if (onStaff && !wasOnStaff)
                animator.SetTrigger(wizard.HasPole && wizard.Pole.IsClimbing
                    ? ClimbUpId
                    : ClimbDownId);

            wasOnStaff = onStaff;
        }

        void WatchForATumble(int mode, PlayerLogic.Movement walk)
        {
            bool ragdolling = mode == ModeRagdoll;

            bool downOnTheFloor = ragdolling && walk.IsGrounded;

            if ((ragdolling && !wasRagdolling) || (downOnTheFloor && !wasRagdollGrounded))
            {
                variant = ragdollVariants <= 1 ? 0 : (variant + 1) % ragdollVariants;
                animator.SetInteger(RagdollVariantId, variant);
            }

            wasRagdolling = ragdolling;
            wasRagdollGrounded = downOnTheFloor;
        }

        void ShowHealth(PlayerLogic.Health health)
        {
            bool tookAHit = health.Current < lastHealth && health.IsInvulnerable && health.IsAlive;
            int hat = ChooseHat(health.Percent);
            bool hatChanged = hat != wornHat;

            lastHealth = health.Current;

            if (hatChanged)
                PutOnHat(hat);

            if (tookAHit)
                animator.SetTrigger(hatChanged ? HatChangedId : HurtId);
        }

        int ChooseHat(int healthPercent)
        {
            for (int i = 0; i < hatStates.Length; i++)
            {
                if (healthPercent >= hatStates[i].fromHealthPercent)
                    return i;
            }

            return hatStates.Length > 0 ? hatStates.Length - 1 : NoHat;
        }

        void PutOnHat(int hat)
        {
            wornHat = hat;

            RuntimeAnimatorController animations = AnimationsFor(hat);

            if (animator.runtimeAnimatorController != animations)
                animator.runtimeAnimatorController = animations;
        }

        RuntimeAnimatorController AnimationsFor(int hat)
        {
            if (hat == NoHat || hatStates[hat].animations == null)
                return normalAnimations;

            return hatStates[hat].animations;
        }

        void ShowTheStaffWhenThePoleOwnsIt(PlayerLogic wizard)
        {
            if (!staffIsDrawnIntoTheArt || staffArt == null)
                return;

            staffArt.enabled = wizard.HasPole && wizard.Pole.IsPlanted;
        }

        [Serializable]
        public class HatState
        {
            [Tooltip("A name for you, so the list is easy to read.")]
            public string name = "Full Hat";

            [Tooltip("This hat is worn while health is at or above this percent of full health.")]
            [Range(0, PlayerLogic.Health.FullHealthPercent)]
            public int fromHealthPercent = PlayerLogic.Health.FullHealthPercent;

            [Tooltip("The wizard's animations drawn with this hat: an Animator Override " +
                     "Controller made from Player.controller. Its Hat Change clip plays when a " +
                     "hit puts this hat on. Leave it empty to use the normal animations.")]
            public AnimatorOverrideController animations;
        }
    }
}
