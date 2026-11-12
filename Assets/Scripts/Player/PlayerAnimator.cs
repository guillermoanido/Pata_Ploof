using UnityEngine;

namespace FallingWizard.Player
{
    [DefaultExecutionOrder(RunAfterTheWizard)]
    public class PlayerAnimator : MonoBehaviour
    {
        const int RunAfterTheWizard = 50;

        public const int ModeGround = 0;
        public const int ModeStaff = 1;
        public const int ModeRagdoll = 2;
        public const int ModeDead = 3;

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

        [Header("Ragdoll")]
        [Tooltip("How many ragdoll reactions to build and cycle through. Rebuild after changing.")]
        [Min(1)] public int ragdollVariants = 4;

        [Header("Locomotion")]
        [Tooltip("Speed below which the wizard counts as standing still, in boxes per second.")]
        [Min(0f)] public float standStillSpeed = 0.1f;

        [Tooltip("Min and max cycle-rate multiplier for Walk and Run.")]
        public Vector2 cycleRateRange = new Vector2(0.5f, 1.5f);

        [Header("Effects")]
        [Tooltip("Played once each time a hazard trips the wizard into a tumble: the stars flung " +
                 "off on impact. A particle system on a child of the wizard, set up entirely in " +
                 "its own Inspector. Leave it empty for no effect.")]
        public ParticleSystem impactStars;

        PlayerCharacter character;
        Animator animator;

        int lastHealth;
        int chosenRagdollVariant;
        bool wasOnStaff;
        bool wasRagdolling;
        bool wasDownOnTheFloor;

        void Awake()
        {
            character = GetComponent<PlayerCharacter>();

            if (character == null)
            {
                Debug.LogError($"'{name}' has a PlayerAnimator but no PlayerCharacter beside it, " +
                               "so there is no wizard to read. Put them on the same object.", this);
                enabled = false;
                return;
            }

            animator = GetComponentInChildren<Animator>();
        }

        void Start()
        {
            lastHealth = character.Logic.health.Current;
            character.Logic.Impacted += PlayImpactStars;
        }

        void Update()
        {
            if (animator == null)
                return;

            PlayerLogic wizard = character.Logic;

            WatchForAHit(wizard.health);

            int mode = ChooseMode(wizard);

            SetAnimatorParameters(wizard, mode);
            WatchForAClimbStarting(wizard, mode);
            WatchForATumble(mode, wizard.movement);
        }

        void OnDestroy()
        {
            if (character != null)
                character.Logic.Impacted -= PlayImpactStars;
        }

        void WatchForAHit(PlayerLogic.Health health)
        {
            bool tookAHit = health.Current < lastHealth && health.IsInvulnerable && health.IsAlive;

            lastHealth = health.Current;

            if (tookAHit)
                animator.SetTrigger(HurtId);
        }

        static int ChooseMode(PlayerLogic wizard)
        {
            if (!wizard.health.IsAlive)
                return ModeDead;

            if (wizard.State == PlayerState.Ragdoll)
                return ModeRagdoll;

            if (wizard.State == PlayerState.OnStaff)
                return ModeStaff;

            return ModeGround;
        }

        void SetAnimatorParameters(PlayerLogic wizard, int mode)
        {
            PlayerLogic.Movement movement = wizard.movement;

            bool grounded = wizard.State != PlayerState.OnVine && movement.IsGrounded;
            bool moving = mode == ModeGround && grounded && movement.HorizontalSpeed > standStillSpeed;

            bool walking = wizard.Steering.IsWalking;

            animator.SetInteger(ModeId, mode);
            animator.SetBool(GroundedId, grounded);
            animator.SetBool(MovingId, moving);
            animator.SetBool(WalkingId, walking);
            animator.SetFloat(SpeedId, CalculateCycleRate(wizard, movement, walking));
            animator.SetFloat(ClimbRateId, CalculateClimbRate(wizard, mode));
        }

        float CalculateCycleRate(PlayerLogic wizard, PlayerLogic.Movement movement, bool walking)
        {
            float topSpeed = (walking ? movement.walkSpeed : movement.runSpeed) *
                             wizard.Stats.MoveSpeedMultiplier;

            if (topSpeed <= Mathf.Epsilon)
                return 1f;

            return Mathf.Clamp(movement.HorizontalSpeed / topSpeed, cycleRateRange.x, cycleRateRange.y);
        }

        float CalculateClimbRate(PlayerLogic wizard, int mode)
        {
            if (mode != ModeStaff || !wizard.HasPole)
                return 0f;

            Staff.Pole pole = wizard.Pole;
            float lean = wizard.Steering.Lean;

            if (Mathf.Abs(lean) <= pole.leanThreshold)
                return 0f;

            if ((lean > 0f && pole.IsAtTop) || (lean < 0f && pole.IsAtBottom))
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

        void WatchForATumble(int mode, PlayerLogic.Movement movement)
        {
            bool ragdolling = mode == ModeRagdoll;

            bool downOnTheFloor = ragdolling && movement.IsGrounded;

            if ((ragdolling && !wasRagdolling) || (downOnTheFloor && !wasDownOnTheFloor))
            {
                chosenRagdollVariant = ragdollVariants <= 1
                    ? 0
                    : (chosenRagdollVariant + 1) % ragdollVariants;

                animator.SetInteger(RagdollVariantId, chosenRagdollVariant);
            }

            wasRagdolling = ragdolling;
            wasDownOnTheFloor = downOnTheFloor;
        }

        void PlayImpactStars()
        {
            if (impactStars != null)
                impactStars.Play();
        }
    }
}
