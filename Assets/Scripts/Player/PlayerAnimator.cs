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
        static readonly int HatChangedId = Animator.StringToHash("HatChanged");

        [Header("Ragdoll")]
        [Tooltip("How many ragdoll reactions to build and cycle through. Rebuild after changing.")]
        [Min(1)] public int ragdollVariants = 4;

        [Header("Locomotion")]
        [Tooltip("Speed below which the wizard counts as standing still, in boxes per second.")]
        [Min(0f)] public float standStillSpeed = 0.1f;

        [Tooltip("Min and max cycle-rate multiplier for Walk and Run.")]
        public Vector2 cycleRateRange = new Vector2(0.5f, 1.5f);

        PlayerCharacter character;
        HealthLook[] looks = new HealthLook[0];
        HealthLook shownLook;
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

            looks = GetComponentsInChildren<HealthLook>(true);
        }

        void Start()
        {
            PlayerLogic.Health health = character.Logic.health;

            lastHealth = health.Current;
            ShowLook(ChooseLook(health.Current));
        }

        void Update()
        {
            PlayerLogic wizard = character.Logic;

            ShowHealth(wizard.health);

            if (animator == null)
                return;

            int mode = ChooseMode(wizard);

            SetAnimatorParameters(wizard, mode);
            WatchForAClimbStarting(wizard, mode);
            WatchForATumble(mode, wizard.movement);
        }

        void ShowHealth(PlayerLogic.Health health)
        {
            bool tookAHit = health.Current < lastHealth && health.IsInvulnerable && health.IsAlive;
            HealthLook look = ChooseLook(health.Current);
            bool lookChanged = look != shownLook;

            lastHealth = health.Current;

            if (lookChanged)
                ShowLook(look);

            if (tookAHit && animator != null)
                animator.SetTrigger(lookChanged ? HatChangedId : HurtId);
        }

        HealthLook ChooseLook(int health)
        {
            HealthLook highestReached = null;
            HealthLook lowest = null;

            foreach (HealthLook look in looks)
            {
                if (lowest == null || look.health < lowest.health)
                    lowest = look;

                if (look.health <= health && (highestReached == null || look.health > highestReached.health))
                    highestReached = look;
            }

            return highestReached != null ? highestReached : lowest;
        }

        void ShowLook(HealthLook chosen)
        {
            foreach (HealthLook look in looks)
                look.gameObject.SetActive(look == chosen);

            shownLook = chosen;
            animator = chosen != null ? chosen.GetComponent<Animator>() : null;

            wasOnStaff = false;
            wasRagdolling = false;
            wasDownOnTheFloor = false;
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
    }
}
