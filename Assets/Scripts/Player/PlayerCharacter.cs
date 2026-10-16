using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCharacter : SingletonBehaviour<PlayerCharacter>
    {
        const string ArtObjectName = "Visual";

        [Header("Body")]
        [Tooltip("Gravity written onto the Rigidbody2D by Reset. 3 with a 2 box jump is the feel " +
                 "the rest of the tuning assumes.")]
        [Min(PlayerLogic.Movement.MinGravityScale)] public float gravityScale = 3f;

        [Header("Stick Feel")]
        [Tooltip("Stick tilt needed before looking up or down counts. Raise it if a worn stick drifts.")]
        [Range(0f, 1f)] public float lookThreshold = 0.5f;

        [Tooltip("Seconds down has to be held before the camera drops to look ahead. Without it " +
                 "the camera lurches every time the stick brushes downward while walking, which " +
                 "reads as the camera being loose rather than as looking.")]
        [Min(0f)] public float lookHoldSeconds = 0.4f;

        [Header("Death")]
        [Tooltip("Reload the level when the wizard runs out of hearts.")]
        public bool restartLevelOnDeath = true;

        [Tooltip("Seconds to wait before that restart, so the death can be seen.")]
        [Min(0f)] public float respawnDelay = 1.25f;

        [Tooltip("Ask where to go instead of dropping straight back at the last rest: carry on " +
                 "from there, or give the run up and go back to spend what is already banked.")]
        public bool offerChoiceOnDeath = true;

        [Header("Staves")]
        [Tooltip("One staff per rank, weakest first. Only the staff for the current rank is left " +
                 "switched on - the others are switched off, and nothing else about any of them " +
                 "is touched. Leave it empty and the Staff objects under this one are used in " +
                 "hierarchy order.")]
        public Staff[] staves;

        [Header("Behaviour")]
        public PlayerLogic logic = new PlayerLogic();

        Controls controls;

        public PlayerLogic Logic => logic;

        public Collider2D Hitbox { get; private set; }

        void Reset()
        {
            var rigidbody2d = GetComponent<Rigidbody2D>();
            rigidbody2d.freezeRotation = true;
            rigidbody2d.gravityScale = gravityScale;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rigidbody2d.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
                logic.movement.FitGroundCheckTo(collider2d);
        }

        void OnValidate() => logic.Validate();

        protected override void OnAwake()
        {
            Hitbox = GetComponent<Collider2D>();

            if (staves == null || staves.Length == 0)
                staves = GetComponentsInChildren<Staff>(true);

            controls = new Controls();

            MoveToCheckpoint();

            logic.health.SetBaseHealth(FindHighestLookHealth());
            logic.Attach(GetComponent<Rigidbody2D>(), FindArt(), Hitbox, staves);
            logic.Died += OnDied;
        }

        void FixedUpdate() => logic.Simulate(Time.fixedDeltaTime);

        void Update() =>
            logic.Observe(controls.Read(lookThreshold, lookHoldSeconds, Time.deltaTime), Time.deltaTime);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            logic.Died -= OnDied;
        }

        void MoveToCheckpoint()
        {
            if (!Progress.IsCheckpointHere)
                return;

            var body = GetComponent<Rigidbody2D>();

            body.position = Progress.CheckpointPoint;
            body.linearVelocity = Vector2.zero;
            transform.position = Progress.CheckpointPoint;
        }

        int FindHighestLookHealth()
        {
            int highest = 0;

            foreach (HealthLook look in GetComponentsInChildren<HealthLook>(true))
                highest = Mathf.Max(highest, look.health);

            if (highest == 0)
                Debug.LogError($"'{name}' has no Health Look under it, so the wizard has the " +
                               "least health possible. Give every health level its own child " +
                               "under Visual with a Health Look on it.", this);

            return highest;
        }

        Transform FindArt()
        {
            Transform art = transform.Find(ArtObjectName);

            if (art == null)
                Debug.LogError($"'{name}' has no child called '{ArtObjectName}', so there is " +
                               "nothing to turn round when the wizard faces the other way or " +
                               "tumbles. Put the wizard's art under a child with that name.", this);

            return art;
        }

        void OnDied()
        {
            Progress.LoseCarried();

            if (offerChoiceOnDeath)
                Invoke(nameof(AskWhereToGo), respawnDelay);
            else if (restartLevelOnDeath)
                Invoke(nameof(RestartLevel), respawnDelay);
        }

        void AskWhereToGo()
        {
            ChoiceScreen screen = ChoiceScreen.Open(Loc.Get(Loc.Keys.DeathTitle),
                                                    Loc.Get(Loc.Keys.DeathBlurb));

            screen.Status(Loc.Format(Loc.Keys.DeathStatus, Progress.Wisps));

            if (Progress.HasCheckpoint)
                screen.Choice(Loc.Get(Loc.Keys.DeathContinue),
                    () => screen.CloseThen(Game.ReloadCurrentScene));

            screen.Choice(Loc.Get(Loc.Keys.DeathGiveUp), () => screen.CloseThen(GiveUpTheRun));
        }

        static void GiveUpTheRun()
        {
            Progress.EndRun();
            SkillScreen.Open(Game.LoadFirstLevel);
        }

        void RestartLevel() => Game.ReloadCurrentScene();

        void OnDrawGizmosSelected() => logic.DrawGizmos(transform.position);

        class Controls
        {
            readonly InputAction moveAction = Core.Controls.Player("Move");
            readonly InputAction jumpAction = Core.Controls.Player("Jump");
            readonly InputAction walkAction = Core.Controls.Player("Walk");

            float secondsHeldDown;
            bool isWalking;

            public PlayerLogic.Intent Read(float lookThreshold, float holdSeconds, float deltaTime)
            {
                if (Game.IsPaused)
                {
                    secondsHeldDown = 0f;
                    return default;
                }

                Vector2 stick = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

                secondsHeldDown = stick.y < -lookThreshold ? secondsHeldDown + deltaTime : 0f;

                if (walkAction != null && walkAction.WasPressedThisFrame())
                    isWalking = !isWalking;

                return new PlayerLogic.Intent
                {
                    Move = stick,
                    WasJumpPressed = jumpAction != null && jumpAction.WasPressedThisFrame(),
                    IsJumpHeld = jumpAction != null && jumpAction.IsPressed(),
                    IsWalking = isWalking,
                    IsLookingDown = secondsHeldDown >= holdSeconds && secondsHeldDown > 0f,
                };
            }
        }
    }
}
