using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public enum PlayerState
    {
        Normal,
        OnStaff,
        Ragdoll,
        OnVine,
    }

    [Serializable]
    public class PlayerLogic
    {
        const int NoStaffSelected = -1;

        [Header("Parts")]
        public Movement movement = new Movement();
        public Ragdoll ragdoll = new Ragdoll();
        public Health health = new Health();
        public Vine vine = new Vine();
        public Spellbook spellbook = new Spellbook();

        [Header("Fall Damage")]
        [Tooltip("Falls shorter than this many boxes are free.")]
        [Min(0f)] public float safeFallDistance = 3f;

        [Tooltip("Hearts lost per box fallen beyond the safe distance. At 1 a box, a wizard on " +
                 "full health dies on the eighth.")]
        [Min(0f)] public float damagePerBox = 1f;

        [NonSerialized] Staff.Pole pole;
        [NonSerialized] Staff[] staves;
        [NonSerialized] Staff carried;
        [NonSerialized] int staffRank = NoStaffSelected;
        [NonSerialized] Rigidbody2D wielderBody;
        [NonSerialized] Collider2D wielderHull;
        [NonSerialized] Intent input;
        [NonSerialized] Vector2 pendingWind;
        [NonSerialized] float pendingRampup;
        [NonSerialized] float pendingGroundScale = 1f;

        [NonSerialized] float pendingGrip = 1f;

        [NonSerialized] Vector2 pendingPlatformVelocity;

        [NonSerialized] public bool Invulnerable;

        public event Action Died;

        public Staff.Pole Pole => pole;
        public bool HasPole => pole != null && pole.HasPole;

        public bool IsStaffFree =>
            HasPole && !pole.IsPlanted && pole.IsReady && State == PlayerState.Normal;

        public Modifiers Stats => spellbook.Stats;
        public Intent Steering => input;
        public Transform Rig => movement.Rig;
        public PlayerState State { get; private set; }
        public bool IsOnStaff => State == PlayerState.OnStaff;
        public bool IsPeeking { get; private set; }
        public bool IsStaffLookingDown { get; private set; }

        public bool CanClimbHere =>
            IsStaffFree && pole.HasHook &&
            movement.TryFindClimbAt(pole.HookBounds, out _, out _);

        public bool IsOnVine => State == PlayerState.OnVine;

        public bool CanGrabVine =>
            health.IsAlive && State == PlayerState.Normal && vine.CanGrab;

        public void Attach(Rigidbody2D body, Transform art, Collider2D hitbox, Staff[] staffSet)
        {
            movement.Attach(body, art, hitbox);
            ragdoll.Attach(body, art, hitbox, movement.groundLayers);
            vine.Attach(body);

            health.SetBonus(Progress.BonusHearts);
            health.RestoreToFull();

            staves = staffSet;
            wielderBody = body;
            wielderHull = hitbox;

            SelectStaff(1);

            spellbook.Attach(this);
        }

        public void Observe(Intent frame, float deltaTime)
        {
            input = frame;

            if (!health.IsAlive)
                return;

            spellbook.Observe(deltaTime);

            IsPeeking = (State == PlayerState.OnStaff && !(HasPole && pole.IsClimbing)) ||
                        (State == PlayerState.Normal && input.IsLookingDown);
        }

        public void Simulate(float fixedDeltaTime)
        {
            if (!health.IsAlive)
                return;

            spellbook.TryCast(fixedDeltaTime);
            spellbook.Rebuild();
            ApplyExternalForce(fixedDeltaTime);
            TakeLedgeWhileLookingDown();

            UpdateCurrentState(fixedDeltaTime);

            spellbook.TickTimers(fixedDeltaTime);
        }

        public void Validate()
        {
            movement.Validate();
            ragdoll.Validate();
            health.Validate();
            vine.Validate();

            safeFallDistance = Mathf.Max(0f, safeFallDistance);
            damagePerBox = Mathf.Max(0f, damagePerBox);
        }

        public void DrawGizmos(Vector2 origin)
        {
            movement.DrawGizmos(origin);
            pole?.DrawGizmos();
            vine.DrawGizmos();
        }

        public bool TryTrip() => TryTrip(movement.TravelDirection);

        public bool TryTrip(int direction)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            int way = Movement.GetDirectionOf(direction);

            ragdoll.Begin(way, way == movement.TravelDirection);

            State = PlayerState.Ragdoll;
            return true;
        }

        public bool TryBounce(float heightInBoxes, float sideways, bool resetsFall)
        {
            if (!health.IsAlive || State == PlayerState.OnStaff || State == PlayerState.OnVine)
                return false;

            movement.Launch(heightInBoxes, sideways, resetsFall);
            return true;
        }

        public void Push(Vector2 boxesPerSecond, float rampup, float groundScale)
        {
            pendingWind += boxesPerSecond;
            pendingRampup = Mathf.Max(pendingRampup, rampup);
            pendingGroundScale = groundScale;
        }

        public void Slicken(float grip) => pendingGrip = Mathf.Min(pendingGrip, Mathf.Clamp01(grip));

        public void RideAlong(Vector2 platformVelocity) => pendingPlatformVelocity = platformVelocity;

        public void Shove(Vector2 velocity, float controlLockout)
        {
            if (State != PlayerState.OnStaff && State != PlayerState.OnVine)
                movement.AddImpulse(velocity, controlLockout);
        }

        public void Hurt(int hearts)
        {
            if (hearts <= 0 || !health.IsAlive || Invulnerable)
                return;

            health.TakeDamage(hearts);

            if (!health.IsAlive)
                Die();
        }

        public void Heal(int hearts) => health.Heal(hearts);

        public void RestoreHealth() => health.RestoreToFull();

        public bool TryGrowHeart(int hearts)
        {
            int taken = Mathf.Min(hearts, health.RoomForBonusHearts);

            if (taken <= 0)
                return false;

            Progress.TakeHearts(taken);
            health.SetBonus(Progress.BonusHearts);
            health.Heal(taken);
            return true;
        }

        public bool WouldFlingThroughHazard(Vector2 launch, Movement.ArcSettings look)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            return movement.WouldTumbleThroughHazard(launch, look);
        }

        public bool TryFling(Vector2 velocity)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            movement.Stop();
            movement.UseBaseGravity();
            movement.BeginFallFrom(movement.Position.y);
            ragdoll.Throw(Movement.GetDirectionOf(velocity.x), velocity);

            State = PlayerState.Ragdoll;
            return true;
        }

        public bool TryPlantStaff(StaffMode mode)
        {
            if (State != PlayerState.Normal || !HasPole)
                return false;

            if (pole.IsPlanted || !pole.IsReady)
                return false;

            if (!movement.TryFindLedgeEdge(out float edgeX))
                return false;

            if (!pole.TryPlant(mode, movement.Facing, edgeX))
                return false;

            if (mode == StaffMode.Ladder)
                State = PlayerState.OnStaff;

            return true;
        }

        public void RaiseStaff() => pole?.Aim(StaffAim.Raised);

        public void LowerStaff() => pole?.Aim(StaffAim.Neutral);

        public void AimStaffDown(bool down)
        {
            IsStaffLookingDown = down;
            pole?.Aim(down ? StaffAim.LookingDown : StaffAim.Neutral);
        }

        public bool TryClimbStaff()
        {
            if (State != PlayerState.Normal || !HasPole || pole.IsPlanted || !pole.IsReady ||
                !pole.HasHook)
                return false;

            if (!movement.TryFindClimbAt(pole.HookBounds, out Vector2 lip, out Vector2 landing))
                return false;

            bool caughtInTheAir = !movement.IsGrounded;

            if (!pole.TryPlantAsClimb(movement.Facing, lip, landing))
                return false;

            if (caughtInTheAir)
                movement.BeginFallFrom(movement.Position.y);

            State = PlayerState.OnStaff;
            return true;
        }

        public void SelectStaff(int rank)
        {
            if (staves == null || staves.Length == 0 || rank == staffRank ||
                (HasPole && pole.IsPlanted))
                return;

            staffRank = rank;
            carried = staves[Mathf.Clamp(rank - 1, 0, staves.Length - 1)];

            ShowOnlyCarriedStaff();
            SwitchToCarriedPole();
        }

        public void RecoverStaff() => RecoverStaff(false);

        public bool TryGrabVine(Vine.Hold spec)
        {
            if (!CanGrabVine || !vine.TryGrab(spec, movement.Position, movement.Velocity))
                return false;

            State = PlayerState.OnVine;
            return true;
        }

        public float GrabSnapDistance(Vine.Hold spec) =>
            Vector2.Distance(movement.Position, vine.WouldHangAt(spec, movement.Position));

        public void LetGoOfVine()
        {
            if (State != PlayerState.OnVine)
                return;

            float hangHeight = vine.HangPosition.y;
            Vector2 launch = vine.Release();

            State = PlayerState.Normal;

            movement.Stop();
            movement.BeginFallFrom(hangHeight);
            movement.AddImpulse(launch, 0f);
        }

        public void DropFromStaff()
        {
            if (State != PlayerState.OnStaff)
                return;

            float hangHeight = pole.HangPosition.y;

            pole.Release();
            movement.BeginFallFrom(hangHeight);
            State = PlayerState.Normal;
        }

        void ApplyExternalForce(float fixedDeltaTime)
        {
            movement.SetGrip(pendingGrip);
            movement.SetPlatformVelocity(pendingPlatformVelocity);

            ApplyPendingWind(fixedDeltaTime);
            ClearPendingForces();
        }

        void ApplyPendingWind(float fixedDeltaTime)
        {
            switch (State)
            {
                case PlayerState.OnStaff:
                case PlayerState.OnVine:
                    break;

                case PlayerState.Ragdoll:
                    movement.NudgeVelocity(pendingWind * fixedDeltaTime);
                    break;

                default:
                    movement.ApplyWind(pendingWind, pendingRampup, pendingGroundScale, fixedDeltaTime);
                    break;
            }
        }

        void ClearPendingForces()
        {
            pendingWind = Vector2.zero;
            pendingRampup = 0f;
            pendingGroundScale = 1f;

            pendingGrip = 1f;
            pendingPlatformVelocity = Vector2.zero;
        }

        void TakeLedgeWhileLookingDown()
        {
            if (!IsStaffLookingDown || !IsStaffFree || !movement.IsAtEdge)
                return;

            if (TryPlantStaff(StaffMode.Ladder))
                AimStaffDown(false);
        }

        void UpdateCurrentState(float fixedDeltaTime)
        {
            switch (State)
            {
                case PlayerState.OnStaff:
                    UpdateOnStaff(fixedDeltaTime);
                    break;

                case PlayerState.Ragdoll:
                    UpdateRagdoll(fixedDeltaTime);
                    break;

                case PlayerState.OnVine:
                    UpdateOnVine(fixedDeltaTime);
                    break;

                default:
                    UpdateNormal(fixedDeltaTime);
                    break;
            }
        }

        void UpdateOnStaff(float fixedDeltaTime)
        {
            switch (pole.Slide(input.Lean, fixedDeltaTime))
            {
                case StaffHold.BackOnLedge:
                    RecoverStaff(true);
                    break;

                case StaffHold.LetGo:
                    DropFromStaff();
                    break;
            }
        }

        void RecoverStaff(bool arrived)
        {
            pole?.Release(arrived);

            if (State == PlayerState.OnStaff)
                State = PlayerState.Normal;
        }

        void UpdateRagdoll(float fixedDeltaTime)
        {
            movement.SenseGround(fixedDeltaTime);
            CheckLanding();

            if (ragdoll.Tick(fixedDeltaTime, movement.IsGrounded, movement.HorizontalSpeed))
                State = PlayerState.Normal;
        }

        void CheckLanding()
        {
            if (movement.TryGetLanding(out float fallDistance))
                TakeFallDamage(fallDistance);
        }

        void TakeFallDamage(float fallDistance)
        {
            float excess = fallDistance - safeFallDistance;
            if (excess <= 0f)
                return;

            Hurt(Mathf.RoundToInt(excess * damagePerBox));
        }

        void UpdateOnVine(float fixedDeltaTime)
        {
            if (input.WasJumpPressed || !vine.Ride(input.Move, fixedDeltaTime))
                LetGoOfVine();
        }

        void UpdateNormal(float fixedDeltaTime)
        {
            movement.FixedTick(input.Command, Stats, fixedDeltaTime);

            pole?.Face(movement.Facing);

            CheckLanding();
        }

        void Die()
        {
            if (State == PlayerState.OnStaff)
                pole.Release();

            if (State == PlayerState.Ragdoll)
                ragdoll.Cancel();

            if (State == PlayerState.OnVine)
                vine.Cancel();

            State = PlayerState.Normal;
            movement.Stop();

            spellbook.ResetForRun();

            Died?.Invoke();
        }

        void ShowOnlyCarriedStaff()
        {
            foreach (Staff staff in staves)
            {
                if (staff != null)
                    staff.gameObject.SetActive(staff == carried);
            }
        }

        void SwitchToCarriedPole()
        {
            Staff.Pole next = carried != null ? carried.Logic : null;

            if (next == pole)
                return;

            pole = next;
            pole?.BindWielder(wielderBody, wielderHull);
            pole?.Face(movement.Facing);
        }

        [Serializable]
        public class Movement
        {
            public const float MinGravityScale = 0.01f;

            const int LayerCount = 32;

            const float MinGroundCheckWidth = 0.05f;
            const float MinGroundCheckThickness = 0.01f;

            const float MinTravelSpeed = 0.1f;

            const float SlopeProbeLift = 0.25f;

            const float StepClearance = 0.02f;

            const float ClimbInset = 0.05f;
            const float HeadroomCheckWidthFactor = 0.6f;

            const float ArcClearance = 0.15f;
            public const float MinArcStep = 0.005f;

            const float GroundlessWarning = 3f;
            const float NearbyGround = 1f;

            const float KineticEnergyFactor = 2f;

            const float Half = 0.5f;
            const float BothSides = 2f;

            static readonly Vector2 MinGroundCheck =
                new Vector2(MinGroundCheckWidth, MinGroundCheckThickness);

            static readonly Color ClimbProbeColour = new Color(0.98f, 0.86f, 0.42f);
            static readonly Color StandingRoomColour = new Color(0.4f, 1f, 0.5f, 0.8f);
            static readonly Color HeadroomColour = new Color(0.4f, 1f, 0.5f, 0.5f);

            static readonly List<Collider2D> Overlaps = new List<Collider2D>();
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>();

            [Header("Speed")]
            [Tooltip("Top speed at a normal run, in boxes per second. Running off a ledge drops you.")]
            [Min(0f)] public float runSpeed = 6f;

            [Tooltip("Top speed while walking. Walk is a toggle - press it once to switch it on and " +
                     "again to switch it off - and while it is on the wizard also refuses to step " +
                     "off a ledge, which is what makes it worth switching on near a drop.")]
            [Min(0f)] public float walkSpeed = 2f;

            [Tooltip("How fast speed builds up. Lower feels heavier and takes longer to get going.")]
            [Min(0f)] public float acceleration = 20f;

            [Tooltip("How fast the wizard coasts to a stop on the ground with no input.")]
            [Min(0f)] public float groundFriction = 26f;

            [Tooltip("Scales acceleration and friction in mid-air. 1 = full control, 0 = committed.")]
            [Range(0f, 1f)] public float airControl = 0.45f;

            [Tooltip("Stick tilt below this counts as no input at all.")]
            [Range(0f, 0.5f)] public float steerDeadzone = 0.01f;

            [Header("Falling")]
            [Tooltip("Gravity is multiplied by this while falling, so drops feel weighty.")]
            [Min(0f)] public float fallGravityMultiplier = 1.7f;

            [Tooltip("Fastest the wizard can fall, in boxes per second.")]
            [Min(0f)] public float maxFallSpeed = 16f;

            [Header("Ground Check")]
            [Tooltip("Which layers count as solid ground. Must NOT include the wizard's own layer, " +
                     "or they will stand on their own collider. Defaults to Ground.")]
            public LayerMask groundLayers = Layers.GroundMask;

            [Tooltip("Where the feet probe sits, relative to the wizard's middle.")]
            public Vector2 groundCheckOffset = new Vector2(0f, -0.596875f);

            [Tooltip("Size of the feet probe. Wider is more forgiving on ledges.")]
            public Vector2 groundCheckSize = new Vector2(0.703125f, 0.1f);

            [Header("Ground Check - Auto Fit")]
            [Tooltip("Gap left under the collider when Reset refits the probe to it.")]
            [Min(0f)] public float groundCheckSkin = 0.05f;

            [Tooltip("Thickness the refitted probe gets.")]
            [Min(MinGroundCheckThickness)] public float groundCheckThickness = 0.1f;

            [Tooltip("Fraction of the collider's width the refitted probe gets.")]
            [Range(0.1f, 1f)] public float groundCheckWidthFactor = 0.9f;

            [Header("Ledge Check")]
            [Tooltip("How far ahead of the feet to look for missing ground.")]
            [Min(0f)] public float ledgeCheckAhead = 0.5f;

            [Tooltip("A gap deeper than this counts as a ledge worth stopping at. Keep it above " +
                     "one box: the probe already hangs a skin's width below the soles, so at " +
                     "0.75 the top of every step of a staircase read as a cliff and a WALKING " +
                     "wizard refused to go down one.")]
            [Min(0f)] public float ledgeCheckDepth = 1.2f;

            [Tooltip("How finely to close in on the exact lip when planting the staff.")]
            [Range(4, 16)] public int edgeSearchSteps = 8;

            [Header("Slopes")]
            [Tooltip("Steepest ramp that counts as a floor to walk up rather than a wall to " +
                     "stop at, in degrees. The level's ramp tiles are 45, so anything comfortably " +
                     "above that takes them and still refuses a vertical face.")]
            [Range(0f, 80f)] public float maxSlopeAngle = 55f;

            [Tooltip("Tilt below this is treated as flat, so a floor that is a hair off level " +
                     "does not switch the wizard into ramp handling every other step.")]
            [Range(0f, 20f)] public float flatSlopeAngle = 3f;

            [Tooltip("How far below the soles to look for the tilt of what they are stood on. " +
                     "Needs to clear the probe's own skin without reaching the floor below.")]
            [Min(0.05f)] public float slopeProbeDepth = 0.5f;

            [Header("Steps")]
            [Tooltip("Tallest lip the wizard walks up on their own, in boxes. A painted tile is " +
                     "0.5, so at 0.55 every single-tile step is taken for free and the staff is " +
                     "only needed for walls two tiles and up. Below 0.5 single tiles need the " +
                     "staff again; at 1.0 or more two-tile walls become free too. 0 turns it off.")]
            [Min(0f)] public float stepHeight = 0.55f;

            [Tooltip("Seconds after leaving the ground that the step assist still works. This is " +
                     "what carries the wizard over tile seams and small lips instead of catching " +
                     "on them; at 0 they snag on every seam.")]
            [Min(0f)] public float stepAssistGrace = 0.12f;

            [Tooltip("How far PAST THE TOES to look for that lip, in boxes, and how far forward " +
                     "the step carries them. Roughly one physics step of running - keep it small. " +
                     "Reaching far ahead on a ramp finds a lip as tall as the reach itself, and " +
                     "the landing check then fails because the ramp goes on climbing through " +
                     "where the wizard would have stood: they stop dead at the bottom of every " +
                     "slope.")]
            [Min(0.02f)] public float stepReach = 0.1f;

            [Tooltip("How far above the lip the hop carries, in boxes. Also the headroom needed before hopping.")]
            [Min(0.02f)] public float hopClearance = 0.15f;

            [Tooltip("Extra forward speed added on the hop, in boxes per second.")]
            [Min(0f)] public float hopForward = 1f;

            [Header("Climbing")]
            [Tooltip("Let the staff catch a ledge while the wizard is in the air, not just from " +
                     "standing. The lip still has to be ABOVE their feet and inside the staff's " +
                     "reach, so this catches a wall you are dropping past rather than letting you " +
                     "climb from nothing. Catching also clears the fall you had banked - without " +
                     "that, topping out bills the whole drop and the catch that saved you kills " +
                     "you instead.")]
            public bool catchLedgesInTheAir = true;

            [Header("Contact")]
            [Tooltip("Friction between the wizard and the world. 0 is right for a platformer: " +
                     "speed is driven entirely by the numbers above, so physics friction adds " +
                     "nothing except corners and seams to snag on. Raise it only if you want " +
                     "them to catch on scenery deliberately.")]
            [Range(0f, 1f)] public float surfaceFriction = 0f;

            [Header("External Force")]
            [Tooltip("How fast wind fades once you leave the zone, in boxes per second squared.")]
            [Min(0f)] public float windDecay = 24f;

            [Tooltip("How hard an up or down wind grips, in boxes per second squared, ON TOP of " +
                     "whatever gravity is doing at the time. The wind's own number is the speed " +
                     "it wants to carry the wizard at; this is how quickly that speed is " +
                     "reached. It is added to gravity rather than fighting it, so a zone lifts " +
                     "at the number it says whatever the wizard weighs. Lower it for wind that " +
                     "takes a moment to catch someone, not for weaker wind.")]
            [Min(0f)] public float windLift = 30f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] Transform art;
            [NonSerialized] Collider2D hull;

            [NonSerialized] float baseGravityScale;
            [NonSerialized] float stepAssistTimer;
            [NonSerialized] float highestPoint;
            [NonSerialized] float pendingFallDistance;
            [NonSerialized] bool hasLanded;

            [NonSerialized] bool hasEverBeenGrounded;
            [NonSerialized] bool hasWarnedGroundless;
            [NonSerialized] float groundlessFor;

            [NonSerialized] Vector2 wind;
            [NonSerialized] float lockout;

            [NonSerialized] float grip = 1f;

            [NonSerialized] Vector2 platformVelocity;

            [NonSerialized] Vector2 groundNormal = Vector2.up;
            [NonSerialized] float groundAngle;
            [NonSerialized] bool isClimbingRamp;

            [NonSerialized] float approachVelocityX;

            [NonSerialized] Vector2 standBox;
            [NonSerialized] Vector2 headBox;
            [NonSerialized] Vector2 headBoxSize;
            [NonSerialized] Vector2 climbProbeBox;
            [NonSerialized] Vector2 climbProbeSize;
            [NonSerialized] float climbFaceY;
            [NonSerialized] bool hasFoundClimbFace;

            [NonSerialized] ContactFilter2D arcFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
            };

            public ClimbRefusal WhyNoClimb { get; private set; }

            public float ClimbRise { get; private set; }
            public float ClimbReach { get; private set; }

            public bool IsGrounded { get; private set; }

            public Collider2D StandingOn { get; private set; }

            public bool IsAtEdge { get; private set; }

            public float ApproachSpeed => Mathf.Abs(approachVelocityX);

            public int TravelDirection =>
                Mathf.Abs(approachVelocityX) > MinTravelSpeed
                    ? GetDirectionOf(approachVelocityX)
                    : Facing;

            public int Facing { get; private set; } = 1;
            public Vector2 Position => body == null ? Vector2.zero : body.position;
            public Transform Rig => body == null ? null : body.transform;

            public Vector2 Footing => hull != null
                ? new Vector2(hull.bounds.center.x, hull.bounds.min.y)
                : new Vector2(Position.x, FeetY + groundCheckSkin);

            public float HorizontalSpeed => body == null ? 0f : Mathf.Abs(body.linearVelocityX);
            public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;

            float FeetY => Position.y + groundCheckOffset.y;

            Vector2 ArcStart => new Vector2(Position.x, FeetY + ArcClearance);

            Vector2 ProbeOrigin => body.position + groundCheckOffset;

            ContactFilter2D GroundFilter => Layers.MakeSolidOnlyFilter(groundLayers);

            float BaseGravity => Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;

            bool IsOnRamp =>
                IsGrounded && groundAngle > flatSlopeAngle && groundAngle <= maxSlopeAngle;

            bool IsOnMovingPlatform => platformVelocity != Vector2.zero;

            float WindGrip => windLift + BaseGravity * fallGravityMultiplier;

            public void Attach(Rigidbody2D rigidbody2d, Transform wizardArt, Collider2D hitbox)
            {
                body = rigidbody2d;
                art = wizardArt;
                hull = hitbox;
                baseGravityScale = Mathf.Max(MinGravityScale, body.gravityScale);
                highestPoint = body.position.y;

                ApplySurfaceFriction();
            }

            public void FixedTick(Command command, Modifiers stats, float fixedDeltaTime)
            {
                lockout -= fixedDeltaTime;

                UpdateFacing(command.Steer);
                SenseGround(fixedDeltaTime);
                Run(command, stats, fixedDeltaTime);

                StepUpSmallLedges(command, stats);

                ApplyFallGravity(stats);
                ApplyWindLift(fixedDeltaTime);

                approachVelocityX = body.linearVelocityX;
            }

            public bool TryGetLanding(out float fallDistance)
            {
                fallDistance = pendingFallDistance;
                bool landedThisStep = hasLanded;
                hasLanded = false;
                return landedThisStep;
            }

            public void Stop()
            {
                body.linearVelocity = Vector2.zero;
                wind = Vector2.zero;
                grip = 1f;
                platformVelocity = Vector2.zero;
                isClimbingRamp = false;
            }

            public void BeginFallFrom(float height)
            {
                highestPoint = height;
                IsGrounded = false;
                stepAssistTimer = 0f;
            }

            public void UseBaseGravity() => body.gravityScale = baseGravityScale;

            public void ApplyWind(Vector2 target, float rampup, float groundScale, float fixedDeltaTime)
            {
                float sidewaysScale = IsGrounded ? groundScale : 1f;
                var feltWind = new Vector2(target.x * sidewaysScale, target.y);
                float rate = rampup > 0f ? rampup : windDecay;
                wind = Vector2.MoveTowards(wind, feltWind, rate * fixedDeltaTime);
            }

            public void SetGrip(float value) => grip = Mathf.Clamp01(value);

            public void SetPlatformVelocity(Vector2 velocity) => platformVelocity = velocity;

            public void AddImpulse(Vector2 velocity, float controlLockout)
            {
                if (body == null)
                    return;

                body.linearVelocity += velocity;
                isClimbingRamp = false;

                lockout = Mathf.Max(lockout, controlLockout);
            }

            public void NudgeVelocity(Vector2 velocity)
            {
                if (body != null)
                    body.linearVelocity += velocity;
            }

            public void Launch(float heightInBoxes, float sideways, bool resetsFall)
            {
                body.linearVelocityY = GetSpeedToRise(Mathf.Max(0f, heightInBoxes));

                if (sideways != 0f)
                    body.linearVelocityX += sideways;

                isClimbingRamp = false;

                if (!resetsFall)
                    return;

                highestPoint = body.position.y;
                IsGrounded = false;
                stepAssistTimer = 0f;
            }

            public void FitGroundCheckTo(Collider2D collider2d)
            {
                Bounds box = collider2d.bounds;
                Vector3 middle = collider2d.transform.position;

                groundCheckOffset = new Vector2(
                    box.center.x - middle.x,
                    box.min.y - middle.y - groundCheckSkin);

                groundCheckSize = new Vector2(
                    box.size.x * groundCheckWidthFactor, groundCheckThickness);
            }

            public void Validate()
            {
                runSpeed = Mathf.Max(0f, runSpeed);
                walkSpeed = Mathf.Clamp(walkSpeed, 0f, runSpeed);
                groundCheckSize = Vector2.Max(groundCheckSize, MinGroundCheck);
                flatSlopeAngle = Mathf.Min(flatSlopeAngle, maxSlopeAngle);

                WarnAboutRiskySettings();
            }

            public bool TryFindLedgeEdge(out float edgeX)
            {
                edgeX = ProbeOrigin.x;

                if (!IsGrounded || !IsAtEdge)
                    return false;

                if (!TryFindFooting(out float footing))
                    return false;

                float air = CloseInOnTheLip(footing);

                if (HasGroundAt(air))
                    return false;

                edgeX = ProbeOrigin.x + Facing * air;
                return true;
            }

            public void SenseGround(float fixedDeltaTime)
            {
                bool wasGrounded = IsGrounded;

                int count = Physics2D.OverlapBox(
                    ProbeOrigin, groundCheckSize, 0f, GroundFilter, Overlaps);

                IsGrounded = count > 0;
                StandingOn = IsGrounded ? Overlaps[0] : null;

                SenseSlope();

                WatchForMissingGround(fixedDeltaTime);

                IsAtEdge = IsGrounded && !HasGroundAt(ledgeCheckAhead);

                if (IsGrounded)
                    UpdateWhileGrounded(wasGrounded);
                else
                    UpdateWhileInTheAir(fixedDeltaTime);
            }

            public bool TryFindClimbAt(Bounds hook, out Vector2 lip, out Vector2 landing)
            {
                lip = Vector2.zero;
                landing = Vector2.zero;

                if (!TryStartMeasuringClimb(hook))
                    return false;

                Bounds box = hull.bounds;

                ClimbReach = hook.max.y - box.min.y;

                if (!TryFindClimbLip(box, hook, out lip))
                    return false;

                var hullOnTop = new Vector2(
                    lip.x + Facing * (box.extents.x + StepClearance),
                    lip.y + box.extents.y + StepClearance);

                if (!HasRoomOnTop(box, hullOnTop) || !HasHeadroom(box, hullOnTop))
                    return false;

                landing = hullOnTop + (body.position - (Vector2)box.center);
                WhyNoClimb = ClimbRefusal.None;
                return true;
            }

            public bool WouldTumbleThroughHazard(Vector2 launch, ArcSettings look)
            {
                if (body == null)
                    return false;

                arcFilter.layerMask = look.Layers;
                return FlyAndWatchForHazards(launch, look);
            }

            public static int GetDirectionOf(float value) => value < 0f ? -1 : 1;

            void ApplySurfaceFriction()
            {
                if (body == null)
                    return;

                body.sharedMaterial = new PhysicsMaterial2D("Wizard Contact")
                {
                    friction = surfaceFriction,
                    bounciness = 0f,
                };
            }

            void UpdateFacing(float steer)
            {
                if (IsSteering(steer))
                    Facing = GetDirectionOf(steer);

                if (art != null)
                    art.localScale = new Vector3(Facing * Mathf.Abs(art.localScale.x),
                        art.localScale.y, art.localScale.z);
            }

            bool IsSteering(float steer) => Mathf.Abs(steer) > steerDeadzone;

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;

                float steer = stats.IsRooted ? 0f : command.Steer;
                float topSpeed = command.IsWalking ? walkSpeed : runSpeed;
                float targetSpeed = GetTargetSpeed(command, stats, steer, topSpeed);
                float rate = GetSpeedChangeRate(stats, steer);

                if (TryRunAlongRamp(targetSpeed, topSpeed * stats.MoveSpeedMultiplier,
                        rate * fixedDeltaTime))
                    return;

                RunHorizontally(targetSpeed, rate * fixedDeltaTime);
            }

            float GetTargetSpeed(Command command, Modifiers stats, float steer, float topSpeed)
            {
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                if (!IsGrounded)
                    targetSpeed *= stats.AirSpeedMultiplier;

                if (command.IsWalking && IsGrounded && IsAtEdge && IsSteering(steer))
                    targetSpeed = 0f;

                targetSpeed += wind.x;
                return targetSpeed;
            }

            float GetSpeedChangeRate(Modifiers stats, float steer)
            {
                bool steering = IsSteering(steer);
                float rate = steering ? acceleration : groundFriction;

                if (!IsGrounded)
                    rate *= airControl *
                            (steering ? stats.AirControlMultiplier : stats.AirDragMultiplier);
                else
                    rate *= grip;

                return rate;
            }

            bool TryRunAlongRamp(float targetSpeed, float topSpeed, float change)
            {
                bool wasClimbing = isClimbingRamp;
                isClimbingRamp = false;

                if (!IsOnRamp)
                {
                    TakeBackLeftoverClimb(wasClimbing, topSpeed);
                    return false;
                }

                if (body.linearVelocityY > topSpeed)
                    return false;

                RunAlongRamp(targetSpeed, topSpeed, change);
                return true;
            }

            void TakeBackLeftoverClimb(bool wasClimbing, float topSpeed)
            {
                if (wasClimbing && IsGrounded &&
                    body.linearVelocityY > 0f && body.linearVelocityY <= topSpeed)
                    body.linearVelocityY = 0f;
            }

            void RunAlongRamp(float targetSpeed, float topSpeed, float change)
            {
                var along = new Vector2(groundNormal.y, -groundNormal.x);

                if (along.x < 0f)
                    along = -along;

                float lean = Mathf.Max(along.x, Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad));

                float cap = topSpeed / lean;

                float carried = Mathf.Clamp(
                    Vector2.Dot(body.linearVelocity, along), -cap, cap);

                float speed = Mathf.MoveTowards(carried, targetSpeed / lean, change / lean);

                body.linearVelocity = along * speed;
                isClimbingRamp = speed * along.y > 0f;
            }

            void RunHorizontally(float targetSpeed, float change)
            {
                float speedOverTheGround = body.linearVelocityX - platformVelocity.x;

                body.linearVelocityX = platformVelocity.x +
                    Mathf.MoveTowards(speedOverTheGround, targetSpeed, change);

                if (IsGrounded && IsOnMovingPlatform)
                    body.linearVelocityY = platformVelocity.y;
            }

            void StepUpSmallLedges(Command command, Modifiers stats)
            {
                if (!CanStepUp(stats))
                    return;

                float steer = command.Steer;

                if (!IsSteering(steer))
                    return;

                int direction = GetDirectionOf(steer);

                if (!TryFindStepLip(direction, out float lipTop))
                    return;

                Bounds box = hull.bounds;
                float rise = lipTop - box.min.y;

                if (rise <= StepClearance || rise > stepHeight)
                    return;

                if (!HasRoomToHop(box))
                    return;

                Launch(rise + hopClearance, hopForward * direction, true);
            }

            bool CanStepUp(Modifiers stats)
            {
                if (stepHeight <= 0f || body == null || hull == null)
                    return false;

                if (lockout > 0f || stats.IsRooted)
                    return false;

                if (stepAssistTimer <= 0f || body.linearVelocityY > 0f)
                    return false;

                return true;
            }

            bool TryFindStepLip(int direction, out float top)
            {
                top = 0f;

                Bounds box = hull.bounds;
                var from = new Vector2(
                    box.center.x + direction * (box.extents.x + stepReach),
                    box.min.y + stepHeight + StepClearance);

                if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                        stepHeight + StepClearance + groundCheckSkin) <= 0)
                    return false;

                top = Rays[0].point.y;
                return true;
            }

            bool HasRoomToHop(Bounds box)
            {
                var overhead = new Vector2(box.center.x, box.max.y + hopClearance * Half);
                var overheadSize = new Vector2(box.size.x - StepClearance * BothSides, hopClearance);

                return Physics2D.OverlapBox(overhead, overheadSize, 0f, GroundFilter, Overlaps) <= 0;
            }

            float GetSpeedToRise(float height) =>
                Mathf.Sqrt(KineticEnergyFactor * BaseGravity * height);

            void ApplyFallGravity(Modifiers stats)
            {
                float floatiness = stats.FallSpeedMultiplier;

                bool falling = !IsGrounded && body.linearVelocityY < 0f;

                body.gravityScale = falling
                    ? baseGravityScale * fallGravityMultiplier * floatiness
                    : baseGravityScale;

                float terminalSpeed = GetTerminalFallSpeed(floatiness);
                if (body.linearVelocityY < -terminalSpeed)
                    body.linearVelocityY = -terminalSpeed;
            }

            float GetTerminalFallSpeed(float floatiness) =>
                Mathf.Max(maxFallSpeed * floatiness, -wind.y);

            void ApplyWindLift(float fixedDeltaTime)
            {
                if (wind.y == 0f || lockout > 0f)
                    return;

                body.linearVelocityY =
                    Mathf.MoveTowards(body.linearVelocityY, wind.y, WindGrip * fixedDeltaTime);
            }

            void WarnAboutRiskySettings()
            {
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0 && IsLayerInMask(playerLayer, groundLayers))
                    Debug.LogWarning("Movement.groundLayers includes the Player layer, so the " +
                                     "wizard will try to stand on their own collider.");

                if (stepHeight >= 1f)
                    Debug.LogWarning("Movement.stepHeight is a whole box or more, and a painted " +
                                     "tile is half that, so the wizard now walks up two-tile " +
                                     "walls on their own and the staff has almost nothing left " +
                                     "to climb. Keep it under 1.");

                if (stepHeight > 0f && ledgeCheckDepth <= 1f)
                    Debug.LogWarning("Movement.ledgeCheckDepth is one box or less, so the top of " +
                                     "every step down reads as a cliff and a walking wizard " +
                                     "refuses to take it. Keep it above 1.");
            }

            static bool IsLayerInMask(int layer, LayerMask mask) =>
                (mask.value & (1 << layer)) != 0;

            bool TryFindFooting(out float footing)
            {
                footing = 0f;

                if (HasGroundAt(footing))
                    return true;

                footing = -groundCheckSize.x * Half;
                return HasGroundAt(footing);
            }

            bool HasGroundAt(float ahead)
            {
                Vector2 probe = ProbeOrigin + new Vector2(Facing * ahead, 0f);
                return Physics2D.Raycast(probe, Vector2.down, GroundFilter, Rays, ledgeCheckDepth) > 0;
            }

            float CloseInOnTheLip(float footing)
            {
                float air = ledgeCheckAhead;

                for (int step = 0; step < edgeSearchSteps; step++)
                {
                    float halfway = (footing + air) * Half;

                    if (HasGroundAt(halfway))
                        footing = halfway;
                    else
                        air = halfway;
                }

                return air;
            }

            void SenseSlope()
            {
                groundNormal = Vector2.up;
                groundAngle = 0f;

                if (!IsGrounded || body == null)
                    return;

                float lift = Mathf.Max(SlopeProbeLift, groundCheckSize.y);
                float halfWidth = groundCheckSize.x * Half;

                for (int i = -1; i <= 1; i++)
                {
                    var from = new Vector2(ProbeOrigin.x + i * halfWidth, ProbeOrigin.y + lift);
                    MeasureSlopeAt(from, lift);
                }
            }

            void MeasureSlopeAt(Vector2 from, float lift)
            {
                if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                        lift + slopeProbeDepth) <= 0)
                    return;

                Vector2 normal = Rays[0].normal;
                float angle = Vector2.Angle(normal, Vector2.up);

                if (angle > groundAngle && angle <= maxSlopeAngle)
                {
                    groundAngle = angle;
                    groundNormal = normal;
                }
            }

            void WatchForMissingGround(float fixedDeltaTime)
            {
                if (IsGrounded)
                {
                    hasEverBeenGrounded = true;
                    return;
                }

                if (hasEverBeenGrounded || hasWarnedGroundless)
                    return;

                groundlessFor += fixedDeltaTime;
                if (groundlessFor < GroundlessWarning)
                    return;

                hasWarnedGroundless = true;

                if (IsGroundNearby())
                    WarnProbeMissesTheGround();
                else
                    WarnNothingOnTheGroundLayers();
            }

            bool IsGroundNearby()
            {
                Vector2 nearbySize = groundCheckSize + Vector2.one * NearbyGround;

                return Physics2D.OverlapBox(
                    ProbeOrigin, nearbySize, 0f, GroundFilter, Overlaps) > 0;
            }

            void WarnProbeMissesTheGround()
            {
                Debug.LogWarning(
                    $"The wizard has not found the ground in {GroundlessWarning:0} seconds, " +
                    "but there IS something on the right layer within a box of their feet - " +
                    "so the mask is fine and the probe is missing it. Two usual causes: " +
                    "groundCheckOffset sits the probe below the surface instead of across " +
                    "it, or the ground is a CompositeCollider2D set to Outlines, which is a " +
                    "zero-thickness line the probe can sit underneath. Switch the composite " +
                    "to Polygons, or raise groundCheckOffset until the probe straddles the " +
                    "wizard's feet.");
            }

            void WarnNothingOnTheGroundLayers()
            {
                Debug.LogWarning(
                    $"The wizard has not found the ground in {GroundlessWarning:0} seconds, and " +
                    "there is nothing on the right layer anywhere near them. " +
                    $"Movement.groundLayers is set to [{ListLayerNames(groundLayers)}], and anything " +
                    "they are meant to stand on must be on one of those layers - tilemaps " +
                    "included, which start on Default. Landing, ledge detection and the staff " +
                    "all read this one mask.");
            }

            static string ListLayerNames(LayerMask mask)
            {
                var listed = new List<string>();

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    if (!IsLayerInMask(layer, mask))
                        continue;

                    string name = LayerMask.LayerToName(layer);
                    listed.Add(string.IsNullOrEmpty(name) ? layer.ToString() : name);
                }

                return listed.Count > 0 ? string.Join(", ", listed) : "nothing";
            }

            void UpdateWhileGrounded(bool wasGrounded)
            {
                if (!wasGrounded)
                {
                    pendingFallDistance = Mathf.Max(0f, highestPoint - body.position.y);
                    hasLanded = true;
                }

                stepAssistTimer = stepAssistGrace;
                highestPoint = body.position.y;
            }

            void UpdateWhileInTheAir(float fixedDeltaTime)
            {
                stepAssistTimer -= fixedDeltaTime;
                highestPoint = Mathf.Max(highestPoint, body.position.y);
            }

            bool TryStartMeasuringClimb(Bounds hook)
            {
                ClimbRise = 0f;
                hasFoundClimbFace = false;
                climbProbeBox = hook.center;
                climbProbeSize = hook.size;

                if (body == null || hull == null || (!IsGrounded && !catchLedgesInTheAir))
                {
                    WhyNoClimb = ClimbRefusal.NotStanding;
                    climbProbeSize = Vector2.zero;
                    return false;
                }

                return true;
            }

            bool TryFindClimbLip(Bounds box, Bounds hook, out Vector2 lip)
            {
                lip = Vector2.zero;

                if (Physics2D.OverlapBox(hook.center, hook.size, 0f, GroundFilter, Overlaps) <= 0)
                {
                    WhyNoClimb = ClimbRefusal.NoWall;
                    return false;
                }

                if (!TryFindTopOfWall(hook, out float lipY))
                    return false;

                lip = new Vector2(FindFaceUnderLip(box, hook, lipY), lipY);

                ClimbRise = lip.y - box.min.y;
                climbFaceY = lip.y;
                hasFoundClimbFace = true;
                return true;
            }

            bool TryFindTopOfWall(Bounds hook, out float top)
            {
                top = 0f;

                var from = new Vector2(hook.center.x, hook.max.y + ClimbInset);

                if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                        hook.size.y + ClimbInset * BothSides) <= 0)
                {
                    WhyNoClimb = ClimbRefusal.NothingOnTop;
                    return false;
                }

                if (Rays[0].distance <= 0f)
                {
                    WhyNoClimb = ClimbRefusal.TooTall;
                    return false;
                }

                top = Rays[0].point.y;
                return true;
            }

            float FindFaceUnderLip(Bounds box, Bounds hook, float lipY)
            {
                var from = new Vector2(box.center.x, lipY - ClimbInset);
                float reach = Mathf.Abs(hook.center.x - from.x) + hook.size.x;

                if (Physics2D.Raycast(from, new Vector2(Facing, 0f), GroundFilter, Rays, reach) > 0 &&
                    Rays[0].distance > 0f)
                    return Rays[0].point.x;

                return hook.center.x;
            }

            bool HasRoomOnTop(Bounds box, Vector2 hullOnTop)
            {
                standBox = hullOnTop;

                var standingSize = (Vector2)box.size - Vector2.one * (StepClearance * BothSides);

                if (Physics2D.OverlapBox(hullOnTop, standingSize, 0f, GroundFilter, Overlaps) > 0)
                {
                    WhyNoClimb = ClimbRefusal.NoRoomOnTop;
                    return false;
                }

                return true;
            }

            bool HasHeadroom(Bounds box, Vector2 hullOnTop)
            {
                float headroom = hullOnTop.y + box.extents.y - box.max.y;

                headBox = new Vector2(box.center.x, box.max.y + headroom * Half);
                headBoxSize = new Vector2(
                    box.size.x * HeadroomCheckWidthFactor, Mathf.Max(0f, headroom));

                if (headroom > 0f &&
                    Physics2D.OverlapBox(headBox, headBoxSize, 0f, GroundFilter, Overlaps) > 0)
                {
                    WhyNoClimb = ClimbRefusal.NoHeadroom;
                    return false;
                }

                return true;
            }

            bool FlyAndWatchForHazards(Vector2 launch, ArcSettings look)
            {
                float step = Mathf.Max(MinArcStep, look.Step);
                Vector2 point = ArcStart;
                Vector2 velocity = launch;
                float travelled = 0f;
                bool crossesHazard = false;

                for (int i = 0; i < look.Steps && travelled < look.Distance; i++)
                {
                    velocity.y -= BaseGravity * step;
                    Vector2 leg = velocity * step;

                    if (TryLandAlong(point, leg, ref crossesHazard))
                        return crossesHazard;

                    travelled += leg.magnitude;
                    point += leg;
                }

                return crossesHazard;
            }

            bool TryLandAlong(Vector2 from, Vector2 leg, ref bool crossesHazard)
            {
                float length = leg.magnitude;

                if (length <= Mathf.Epsilon)
                    return false;

                int found = Physics2D.Raycast(from, leg / length, arcFilter, Rays, length);

                for (int hit = 0; hit < found; hit++)
                {
                    if (IsLayerInMask(Rays[hit].collider.gameObject.layer, groundLayers))
                        return true;

                    crossesHazard = true;
                }

                return false;
            }

            public void DrawGizmos(Vector2 origin)
            {
                DrawGroundProbe(origin);
                DrawLedgeProbe(origin);

                if (stepHeight <= 0f)
                    return;

                DrawStepProbe(origin);

                if (climbProbeSize.y > 0f)
                    DrawClimbProbe();

                DrawRoomOnTop();
            }

            void DrawGroundProbe(Vector2 origin)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(origin + groundCheckOffset, groundCheckSize);
            }

            void DrawLedgeProbe(Vector2 origin)
            {
                Gizmos.color = Color.yellow;
                Vector2 probe = origin + groundCheckOffset + new Vector2(Facing * ledgeCheckAhead, 0f);
                Gizmos.DrawLine(probe, probe + Vector2.down * ledgeCheckDepth);
            }

            void DrawStepProbe(Vector2 origin)
            {
                Gizmos.color = Color.green;
                float soles = origin.y + groundCheckOffset.y + groundCheckSkin;
                var stepProbe = new Vector2(origin.x + groundCheckOffset.x + Facing * stepReach, soles);
                Gizmos.DrawLine(stepProbe, stepProbe + Vector2.up * stepHeight);
            }

            void DrawClimbProbe()
            {
                Gizmos.color = WhyNoClimb == ClimbRefusal.NoWall
                    ? Color.red
                    : ClimbProbeColour;

                Gizmos.DrawWireCube(climbProbeBox, climbProbeSize);

                if (hasFoundClimbFace)
                    Gizmos.DrawLine(
                        new Vector2(climbProbeBox.x - climbProbeSize.x, climbFaceY),
                        new Vector2(climbProbeBox.x + climbProbeSize.x, climbFaceY));
            }

            void DrawRoomOnTop()
            {
                bool measured = WhyNoClimb == ClimbRefusal.None ||
                                WhyNoClimb == ClimbRefusal.NoRoomOnTop ||
                                WhyNoClimb == ClimbRefusal.NoHeadroom;

                if (hull == null || !measured)
                    return;

                Gizmos.color = WhyNoClimb == ClimbRefusal.NoRoomOnTop
                    ? Color.red
                    : StandingRoomColour;

                Gizmos.DrawWireCube(standBox, hull.bounds.size);

                if (headBoxSize.y <= 0f)
                    return;

                Gizmos.color = WhyNoClimb == ClimbRefusal.NoHeadroom
                    ? Color.red
                    : HeadroomColour;

                Gizmos.DrawWireCube(headBox, headBoxSize);
            }

            public enum ClimbRefusal
            {
                None,
                NotStanding,
                NoWall,
                NothingOnTop,
                TooTall,
                NoRoomOnTop,
                NoHeadroom,
            }

            public struct ArcSettings
            {
                public LayerMask Layers;
                public float Step;
                public int Steps;
                public float Distance;
            }
        }

        [Serializable]
        public class Ragdoll
        {
            const float MinStandUp = 0.01f;
            const float MinTumbleSpread = 0.1f;

            const float NotStandingUp = -1f;

            const float BounceCooldown = 0.08f;
            const float WallSkin = 0.04f;

            const float Half = 0.5f;
            const float ProbeHeightFraction = 0.7f;

            const int MaxWallHits = 2;

            static readonly RaycastHit2D[] WallHits = new RaycastHit2D[MaxWallHits];

            [Header("Tumble")]
            [Tooltip("How fast the wizard spins as they go over, in degrees per second. They " +
                     "always roll the way they were going.")]
            public float spinSpeed = 520f;

            [Tooltip("How quickly that spin slows, in degrees per second squared. 0 keeps " +
                     "spinning at full rate until they get up.")]
            [Min(0f)] public float spinDown = 240f;

            [Tooltip("How much of their speed carries into the tumble. 1 keeps all of it, so a " +
                     "trip is a loss of footing rather than a wall.")]
            [Range(0f, 1f)] public float momentumKept = 1f;

            [Header("Launch")]
            [Tooltip("Shove ONWARD as they go over, in boxes per second, on top of whatever " +
                     "speed they already had. This is what makes a trip throw you rather than " +
                     "drop you.")]
            [Min(0f)] public float launchForward = 3f;

            [Tooltip("Lift as they go over, in boxes per second. A little goes a long way: it " +
                     "gets them off the floor so the launch is not immediately scrubbed off.")]
            [Min(0f)] public float launchUp = 5f;

            [Tooltip("Least speed they leave the ground with, in boxes per second. Tripping at " +
                     "a crawl and tripping at a sprint then differ in degree, not in kind.")]
            [Min(0f)] public float minimumLaunch = 4f;

            [Header("Getting Up")]
            [Tooltip("Minimum seconds spent tumbling before they can start getting up.")]
            [Min(0f)] public float minimumDuration = 0.9f;

            [Tooltip("Hard limit on a tumble, in seconds. They get up after this whether or not " +
                     "they ever found the ground. Without it, one trip somewhere the ground check " +
                     "cannot see is a wizard who never moves again.")]
            [Min(MinTumbleSpread)] public float maximumDuration = 3f;

            [Tooltip("How fast the skid bleeds off once they are back on the ground, in boxes " +
                     "per second squared. This rather than physics friction, so the same trip " +
                     "always slides the same distance.")]
            [Min(0f)] public float slideFriction = 9f;

            [Tooltip("They only get up once grounded and slower than this, in boxes per second.")]
            [Min(0f)] public float recoverSpeed = 1.2f;

            [Tooltip("Seconds spent straightening back up.")]
            [Min(MinStandUp)] public float standUpDuration = 0.35f;

            [Header("Walls")]
            [Tooltip("How much sideways speed a tumbling wizard keeps when they hit a wall, as a " +
                     "fraction of what they arrived with. 0 is a dead stop; 1 is a full rebound.")]
            [Range(0f, 1f)] public float wallBounce = 0.55f;

            [Tooltip("Slowest arrival that still bounces, in boxes per second. Below it they come " +
                     "to rest against the wall instead of chattering off it.")]
            [Min(0f)] public float minimumBounceSpeed = 1.5f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] Transform visual;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float tumbleTimer;
            [NonSerialized] float elapsed;
            [NonSerialized] float standUpTimer;
            [NonSerialized] float standUpFromAngle;
            [NonSerialized] Collider2D hull;
            [NonSerialized] LayerMask wallLayers;
            [NonSerialized] float bounceReadyAt;

            public event Action<Vector2> BouncedOffAWall;

            bool IsStandingUp => standUpTimer >= 0f;

            public void Attach(Rigidbody2D wizardBody, Transform art, Collider2D hitbox,
                LayerMask groundLayers)
            {
                body = wizardBody;
                visual = art;
                hull = hitbox;
                wallLayers = groundLayers;
                standUpTimer = NotStandingUp;
                angle = 0f;
                bounceReadyAt = 0f;
                RotateArt();
            }

            public void Begin(int direction, bool keepMomentum)
            {
                StartTumbling(direction);

                float carried = keepMomentum ? body.linearVelocityX * momentumKept : 0f;
                float thrown = carried + direction * launchForward;

                if (Mathf.Abs(thrown) < minimumLaunch)
                    thrown = direction * minimumLaunch;

                body.linearVelocityX = thrown;

                body.linearVelocityY = Mathf.Max(body.linearVelocityY, launchUp);
            }

            public void Throw(int direction, Vector2 velocity)
            {
                StartTumbling(direction);
                body.linearVelocity = velocity;
            }

            public bool Tick(float fixedDeltaTime, bool grounded, float horizontalSpeed)
            {
                if (IsStandingUp)
                    return StandUp(fixedDeltaTime);

                Tumble(fixedDeltaTime);
                BounceOffWalls(fixedDeltaTime);
                SlowTheSkid(grounded, fixedDeltaTime);

                bool waitedLongEnough = elapsed >= maximumDuration;

                if (!waitedLongEnough &&
                    (tumbleTimer > 0f || !grounded || horizontalSpeed > recoverSpeed))
                    return false;

                standUpFromAngle = angle;
                standUpTimer = 0f;
                return false;
            }

            public void Cancel()
            {
                spin = 0f;
                angle = 0f;
                standUpTimer = NotStandingUp;
                RotateArt();
            }

            public void Validate()
            {
                standUpDuration = Mathf.Max(MinStandUp, standUpDuration);
                maximumDuration = Mathf.Max(maximumDuration, minimumDuration + MinTumbleSpread);
                wallBounce = Mathf.Clamp01(wallBounce);
                minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);

                if (slideFriction <= 0f && recoverSpeed < minimumLaunch)
                    Debug.LogWarning("Ragdoll.slideFriction is 0 and recoverSpeed is below " +
                                     "minimumLaunch, so a tripped wizard can never slow down " +
                                     "enough to stand back up.");
            }

            void RotateArt()
            {
                if (visual != null)
                    visual.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            void StartTumbling(int direction)
            {
                spin = -direction * spinSpeed;
                angle = 0f;
                tumbleTimer = minimumDuration;
                standUpTimer = NotStandingUp;
                elapsed = 0f;
            }

            bool StandUp(float fixedDeltaTime)
            {
                standUpTimer += fixedDeltaTime;

                float progress = Mathf.Clamp01(standUpTimer / Mathf.Max(MinStandUp, standUpDuration));
                angle = Mathf.LerpAngle(standUpFromAngle, 0f, progress);
                RotateArt();

                if (progress < 1f)
                    return false;

                Cancel();
                return true;
            }

            void Tumble(float fixedDeltaTime)
            {
                angle += spin * fixedDeltaTime;
                spin = Mathf.MoveTowards(spin, 0f, spinDown * fixedDeltaTime);
                RotateArt();

                tumbleTimer -= fixedDeltaTime;
                elapsed += fixedDeltaTime;
            }

            void BounceOffWalls(float fixedDeltaTime)
            {
                if (wallBounce <= 0f || body == null || hull == null || Time.time < bounceReadyAt)
                    return;

                float speed = body.linearVelocityX;

                if (Mathf.Abs(speed) < minimumBounceSpeed)
                    return;

                if (!IsWallAhead(speed, fixedDeltaTime))
                    return;

                body.linearVelocityX = -speed * wallBounce;
                spin = -spin;
                bounceReadyAt = Time.time + BounceCooldown;

                BouncedOffAWall?.Invoke(WallHits[0].point);
            }

            bool IsWallAhead(float speed, float fixedDeltaTime)
            {
                var way = new Vector2(Movement.GetDirectionOf(speed), 0f);
                Bounds box = hull.bounds;

                var probe = new Vector2(box.size.x * Half, box.size.y * ProbeHeightFraction);
                float reach = box.extents.x * Half + Mathf.Abs(speed) * fixedDeltaTime + WallSkin;

                ContactFilter2D filter = Layers.MakeSolidOnlyFilter(wallLayers);

                return Physics2D.BoxCast(box.center, probe, 0f, way, filter, WallHits, reach) > 0;
            }

            void SlowTheSkid(bool grounded, float fixedDeltaTime)
            {
                if (grounded && slideFriction > 0f)
                    body.linearVelocityX =
                        Mathf.MoveTowards(body.linearVelocityX, 0f, slideFriction * fixedDeltaTime);
            }
        }

        [Serializable]
        public class Health
        {
            const int LeastHealth = 1;

            [Header("Health")]
            [Tooltip("Most hearts that can ever be added on top, across the whole save. Place " +
                     "fewer hearts than this in the game and the cap never comes up - it is here " +
                     "so a generous level cannot quietly make the wizard unkillable.")]
            [Min(0)] public int maxBonusHearts = 4;

            [Tooltip("Seconds of immunity after a hit, so one hazard cannot chain-kill.")]
            [Min(0f)] public float invulnerabilityTime = 0.6f;

            [NonSerialized] float invulnerableUntil;
            [NonSerialized] int baseHealth = LeastHealth;
            [NonSerialized] int bonus;

            public int Current { get; private set; }
            public bool IsAlive => Current > 0;
            public bool IsInvulnerable => Time.time < invulnerableUntil;

            public int RoomForBonusHearts => Mathf.Max(0, maxBonusHearts - bonus);

            int Max => baseHealth + bonus;

            public void SetBaseHealth(int hearts)
            {
                baseHealth = Mathf.Max(LeastHealth, hearts);
                Current = Mathf.Min(Current, Max);
            }

            public void SetBonus(int extra)
            {
                bonus = Mathf.Clamp(extra, 0, maxBonusHearts);
                Current = Mathf.Min(Current, Max);
            }

            public void RestoreToFull() => Current = Max;

            public void TakeDamage(int amount)
            {
                if (amount <= 0 || !IsAlive || IsInvulnerable)
                    return;

                Current = Mathf.Max(0, Current - amount);
                invulnerableUntil = Time.time + invulnerabilityTime;
            }

            public void Heal(int amount)
            {
                if (amount <= 0 || !IsAlive)
                    return;

                Current = Mathf.Min(Max, Current + amount);
            }

            public void Validate()
            {
                maxBonusHearts = Mathf.Max(0, maxBonusHearts);
                invulnerabilityTime = Mathf.Max(0f, invulnerabilityTime);
            }
        }

        [Serializable]
        public class Vine
        {
            const float Epsilon = 0.0001f;

            const float MaxLean = 89f;

            const float MaxDamping = 8f;

            const float MinReach = 0.1f;

            const float GripRadius = 0.2f;

            const float MaxHaulSpeed = 60f;

            const float BlockedDistance = 0.25f;

            static readonly Color RopeColour = new Color(0.42f, 0.75f, 0.38f);

            [Header("Swinging")]
            [Tooltip("How hard the wizard hangs, against Unity's own gravity. Match it to the " +
                     "Rigidbody2D's gravity scale and a swing takes as long as a fall of the " +
                     "same size looks like it should. Lower is a slow, floaty rope.")]
            [Min(0f)] public float weight = 3f;

            [Tooltip("How hard left and right kick the swing, in boxes per second squared. This " +
                     "is a push, not a speed: you pump a swing with it the way you would on a " +
                     "real one, and how much you get out depends on when you push.")]
            [Min(0f)] public float swingPush = 26f;

            [Tooltip("How quickly a swing dies down with nobody steering it, per second. 0 swings " +
                     "forever. High numbers hang almost still. This is what settles the wizard " +
                     "back under the knot when they let the stick go.")]
            [Range(0f, MaxDamping)] public float damping = 1.1f;

            [Tooltip("How far the vine will lean either side of straight down, in degrees. It " +
                     "stops dead at the limit rather than bouncing off it.")]
            [Range(0f, MaxLean)] public float maxSwing = 70f;

            [Header("Climbing")]
            [Tooltip("Fastest the wizard can ever climb, in boxes per second. A spell asks for " +
                     "its own climb speed when it grabs and the smaller of the two wins, so this " +
                     "is a ceiling rather than the number in play.")]
            [Min(0f)] public float climbSpeed = 4f;

            [Tooltip("Closest you can climb to where the vine is tied, in boxes. Keeps the wizard " +
                     "out of the ceiling.")]
            [Min(MinReach)] public float minDepth = 0.75f;

            [Header("Letting Go")]
            [Tooltip("How much of the swing you actually leave with. 1 is exactly the speed you " +
                     "were travelling, which is what the arc has been showing you all along - " +
                     "above that and a release throws further than it looked like it would.")]
            [Min(0f)] public float releaseBoost = 1f;

            [Tooltip("Extra upward speed on letting go, in boxes per second, so a release near " +
                     "the bottom of a swing still clears something.")]
            [Min(0f)] public float releaseLift = 3f;

            [Tooltip("Fastest you can be flung off, in boxes per second. A long vine swung hard " +
                     "would otherwise fire the wizard across the level.")]
            [Min(0f)] public float maxReleaseSpeed = 14f;

            [Tooltip("Seconds before another vine can be caught. Stops one press re-grabbing the " +
                     "vine you just left.")]
            [Min(0f)] public float regrabDelay = 0.35f;

            [Tooltip("Let go the moment the vine runs out under you, rather than hanging on at " +
                     "the very end.")]
            public bool letGoAtTheEnd = false;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] float gravityToRestore;

            [NonSerialized] bool isRiding;
            [NonSerialized] Vector2 anchor;
            [NonSerialized] float length;
            [NonSerialized] float swingLimit;
            [NonSerialized] float depth;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float climbSpeedInPlay;
            [NonSerialized] float readyAt;

            [NonSerialized] Vector2 haulTarget;
            [NonSerialized] bool hasSteered;

            public bool CanGrab => body != null && Time.time >= readyAt;

            public Vector2 Anchor => anchor;

            public Vector2 HangPosition => FindHangPosition(anchor, angle, depth);

            public float Lean => angle * Mathf.Rad2Deg;

            Vector2 ArcDirection => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            public void Attach(Rigidbody2D wizardBody)
            {
                body = wizardBody;

                if (body != null)
                    gravityToRestore = body.gravityScale;

                isRiding = false;
                readyAt = 0f;
            }

            public Vector2 WouldHangAt(Hold spec, Vector2 from)
            {
                Vector2 reach = from - spec.Anchor;

                float hangDepth = Mathf.Clamp(reach.magnitude, minDepth, spec.Length);
                float lean = MeasureLean(reach, FindSwingLimit(spec));

                return FindHangPosition(spec.Anchor, lean, hangDepth);
            }

            public bool TryGrab(Hold spec, Vector2 from, Vector2 carriedVelocity)
            {
                if (body == null || isRiding || spec.Length <= minDepth)
                    return false;

                if (Vector2.Distance(from, WouldHangAt(spec, from)) > spec.SnapLimit)
                    return false;

                anchor = spec.Anchor;
                length = spec.Length;
                swingLimit = FindSwingLimit(spec);

                climbSpeedInPlay = Mathf.Min(climbSpeed, Mathf.Max(0f, spec.ClimbSpeed));

                StartSwingFrom(from, carriedVelocity);
                TakeOverBody();

                hasSteered = false;
                isRiding = true;
                return true;
            }

            public bool Ride(Vector2 stick, float fixedDeltaTime)
            {
                if (!isRiding || body == null || fixedDeltaTime <= Epsilon)
                    return false;

                StopSwingIfBlocked();
                CatchUpWithBody();
                Climb(stick, fixedDeltaTime);
                Swing(stick, fixedDeltaTime);
                HaulBodyToRope(fixedDeltaTime);

                return !letGoAtTheEnd || depth < length - Epsilon || stick.y >= 0f;
            }

            public Vector2 Release()
            {
                RestoreGravity();

                isRiding = false;
                readyAt = Time.time + regrabDelay;

                float speed = Mathf.Clamp(spin * depth * releaseBoost,
                    -maxReleaseSpeed, maxReleaseSpeed);

                spin = 0f;

                return ArcDirection * speed + Vector2.up * releaseLift;
            }

            public void Cancel()
            {
                RestoreGravity();

                isRiding = false;
                spin = 0f;
                hasSteered = false;
                readyAt = 0f;
            }

            public void Validate()
            {
                weight = Mathf.Max(0f, weight);
                swingPush = Mathf.Max(0f, swingPush);
                damping = Mathf.Clamp(damping, 0f, MaxDamping);
                maxSwing = Mathf.Clamp(maxSwing, 0f, MaxLean);
                climbSpeed = Mathf.Max(0f, climbSpeed);
                minDepth = Mathf.Max(MinReach, minDepth);
                releaseBoost = Mathf.Max(0f, releaseBoost);
                releaseLift = Mathf.Max(0f, releaseLift);
                maxReleaseSpeed = Mathf.Max(0f, maxReleaseSpeed);
                regrabDelay = Mathf.Max(0f, regrabDelay);
            }

            public void DrawGizmos()
            {
                if (!isRiding)
                    return;

                Gizmos.color = RopeColour;
                Gizmos.DrawLine(anchor, HangPosition);
                Gizmos.DrawWireSphere(HangPosition, GripRadius);
            }

            static float MeasureLean(Vector2 reach, float limit)
            {
                if (reach.sqrMagnitude < Epsilon)
                    return 0f;

                return Mathf.Clamp(Mathf.Atan2(reach.x, -reach.y), -limit, limit);
            }

            float FindSwingLimit(Hold spec) =>
                Mathf.Min(maxSwing, Mathf.Abs(spec.MaxSwingDegrees)) * Mathf.Deg2Rad;

            static Vector2 FindHangPosition(Vector2 anchorPoint, float lean, float distance) =>
                anchorPoint + new Vector2(Mathf.Sin(lean), -Mathf.Cos(lean)) * distance;

            void StartSwingFrom(Vector2 from, Vector2 carriedVelocity)
            {
                Vector2 reach = from - anchor;

                depth = Mathf.Clamp(reach.magnitude, minDepth, length);
                angle = MeasureLean(reach, swingLimit);
                spin = depth <= Epsilon ? 0f : Vector2.Dot(carriedVelocity, ArcDirection) / depth;
            }

            void TakeOverBody()
            {
                gravityToRestore = body.gravityScale;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
            }

            void StopSwingIfBlocked()
            {
                if (hasSteered &&
                    (body.position - haulTarget).sqrMagnitude > BlockedDistance * BlockedDistance)
                    spin = 0f;
            }

            void CatchUpWithBody()
            {
                Vector2 reach = body.position - anchor;

                if (reach.sqrMagnitude > Epsilon)
                {
                    depth = Mathf.Clamp(reach.magnitude, minDepth, length);
                    angle = MeasureLean(reach, swingLimit);
                }
            }

            void Climb(Vector2 stick, float fixedDeltaTime)
            {
                depth = Mathf.Clamp(depth - stick.y * climbSpeedInPlay * fixedDeltaTime,
                    minDepth, length);
            }

            void Swing(Vector2 stick, float fixedDeltaTime)
            {
                float rope = Mathf.Max(depth, MinReach);
                float gravity = Mathf.Abs(Physics2D.gravity.y) * weight;

                float pull = -(gravity / rope) * Mathf.Sin(angle);
                float push = stick.x * (swingPush / rope);

                spin += (pull + push) * fixedDeltaTime;
                spin *= Mathf.Clamp01(1f - damping * fixedDeltaTime);

                angle += spin * fixedDeltaTime;

                StopAtSwingLimit();
            }

            void StopAtSwingLimit()
            {
                if (Mathf.Abs(angle) > swingLimit)
                {
                    angle = Mathf.Clamp(angle, -swingLimit, swingLimit);

                    if (spin * angle > 0f)
                        spin = 0f;
                }
            }

            void HaulBodyToRope(float fixedDeltaTime)
            {
                haulTarget = HangPosition;
                hasSteered = true;

                Vector2 haul = (haulTarget - body.position) / fixedDeltaTime;

                body.linearVelocity = Vector2.ClampMagnitude(haul, MaxHaulSpeed);
            }

            void RestoreGravity()
            {
                if (body != null)
                    body.gravityScale = gravityToRestore;
            }

            public struct Hold
            {
                public Vector2 Anchor;
                public float Length;
                public float MaxSwingDegrees;
                public float ClimbSpeed;
                public float SnapLimit;
            }
        }

        [Serializable]
        public class Spellbook
        {
            public const int SlotCount = Progress.SlotCount;

            const float ExplainAfter = 0.35f;

            const float NotReleased = -1f;

            public static readonly string[] SlotActions =
                { "Spell1", "Spell2", "Spell3", "Spell4" };

            [Header("Spells")]
            [Tooltip("The catalogue every slot draws from. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

            [Tooltip("Print a line to the console whenever a press comes to nothing, saying " +
                     "which spell refused and why. Editor only. Leave it on while building a " +
                     "level - a spell that silently does nothing is the hardest kind to chase.")]
            public bool explainRefusals = true;

            [NonSerialized] Modifiers stats = new Modifiers();
            [NonSerialized] Slot[] slots = Array.Empty<Slot>();
            [NonSerialized] PlayerLogic owner;

            [NonSerialized] readonly Dictionary<Ability, object> spellStates =
                new Dictionary<Ability, object>();

            public int Version { get; private set; }

            public IReadOnlyList<Slot> Slots => slots;

            public Modifiers Stats => stats;

            public void Attach(PlayerLogic player)
            {
                owner = player;

                if (book == null)
                    book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

                if (book == null)
                {
                    Debug.LogError("No spellbook found. Create Assets/Resources/Spellbook.asset " +
                                   "from Assets > Create > Falling Wizard > Spellbook, and put " +
                                   "the Staff in both of its lists.");
                    slots = Array.Empty<Slot>();
                    return;
                }

                slots = new Slot[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                    slots[i] = new Slot(Controls.Player(SlotActions[i]));

                Seed(book);
                Reload();
            }

            public static void Seed(AbilityBook book)
            {
                if (book == null)
                    return;

                foreach (Ability spell in book.known)
                    if (spell != null)
                        Progress.Grant(spell.Key);

                foreach (Ability spell in book.spells)
                    if (spell != null && spell.locked && spell.fixedSlot >= 0 &&
                        Progress.Owns(spell.Key) && Progress.SlotHolding(spell.Key) < 0)
                        Progress.Equip(spell.fixedSlot, spell.Key);
            }

            public void Reload()
            {
                if (slots.Length == 0)
                    return;

                Seed(book);

                for (int i = 0; i < SlotCount; i++)
                {
                    Ability next = book.Find(Progress.EquippedIn(i));

                    if (next != null && !Progress.Owns(next.Key))
                        next = null;

                    ReloadSlot(slots[i], next);
                }

                Version++;
            }

            public bool TryEquip(Ability spell, int slotIndex)
            {
                if (slotIndex < 0 || slotIndex >= SlotCount)
                    return false;

                if (spell != null && (!Progress.Owns(spell.Key) || spell.locked))
                    return false;

                Ability leaving = book.Find(Progress.EquippedIn(slotIndex));

                if (leaving != null && leaving.locked)
                    return false;

                Progress.Place(slotIndex, spell != null ? spell.Key : string.Empty);
                Reload();
                return true;
            }

            public T StateOf<T>(Ability spell) where T : class, new()
            {
                if (spell == null)
                    return null;

                if (spellStates.TryGetValue(spell, out object stored) && stored is T state)
                    return state;

                var fresh = new T();
                spellStates[spell] = fresh;
                return fresh;
            }

            public void Extinguish(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsLit)
                    return;

                PutOut(slot);
            }

            public int RankOf(Ability spell)
            {
                Slot slot = SlotOf(spell);
                return slot != null ? slot.Rank : 0;
            }

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (paused || slot.Action == null)
                        ForgetInput(slot);
                    else if (slot.Ability == null)
                        RefuseEmptySlot(i, slot);
                    else
                        ReadButton(i, slot, deltaTime);
                }
            }

            public void TryCast(float fixedDeltaTime)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (slot.Ability == null)
                        continue;

                    if (slot.Ability.chargesOnHold)
                        AdvanceCharge(i, slot, fixedDeltaTime);
                    else
                        TryCastBufferedPress(slot);
                }
            }

            public bool TryFire(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsReady)
                    return false;

                slot.BeginCast();
                return true;
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Ability != null)
                        slot.Ability.ModifyStats(owner, stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit)
                        slot.Ability.ModifyStatsWhileLit(owner, stats);
            }

            public void TickTimers(float fixedDeltaTime)
            {
                foreach (Slot slot in slots)
                {
                    if (slot.CooldownLeft > 0f)
                        slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.Ability.OnLit(owner, fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.LitLeft -= fixedDeltaTime;

                    if (slot.LitLeft <= 0f)
                        PutOut(slot);
                }
            }

            public void ResetForRun()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Fill(slot.Ability);

                    if (slot.Ability != null)
                        slot.Ability.OnRunReset(owner);
                }
            }

            void ReloadSlot(Slot slot, Ability next)
            {
                slot.Rank = next != null ? Progress.Rank(next.Key) : 0;

                if (slot.Ability == next)
                    return;

                if (slot.Ability != null)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Ability.OnUnequipped(owner);
                }

                slot.Fill(next);

                if (next != null)
                    next.OnEquipped(owner);
            }

            Slot SlotOf(Ability spell)
            {
                if (spell == null)
                    return null;

                foreach (Slot slot in slots)
                {
                    if (slot.Ability == spell)
                        return slot;
                }

                return null;
            }

            void PutOut(Slot slot)
            {
                slot.LitLeft = 0f;
                slot.CooldownLeft = slot.Ability.cooldown;
                slot.Ability.OnEnded(owner);
            }

            void ForgetInput(Slot slot)
            {
                slot.BufferLeft = 0f;

                if (slot.HeldFor > 0f && slot.Ability != null)
                {
                    slot.Ability.OnChargeLost(owner);
                    slot.DropCharge();
                }
            }

            void RefuseEmptySlot(int slotIndex, Slot slot)
            {
                if (slot.Action.WasPressedThisFrame())
                    Explain(slotIndex, null, "there is nothing in that slot");

                slot.BufferLeft = 0f;
            }

            void Explain(int slotIndex, Ability spell, string reason)
            {
#if UNITY_EDITOR
                if (!explainRefusals || string.IsNullOrEmpty(reason))
                    return;

                string spellName = spell != null ? spell.Name : $"Slot {slotIndex + 1}";

                Debug.LogWarning($"{spellName} did not cast: {reason}.");
#endif
            }

            void ReadButton(int slotIndex, Slot slot, float deltaTime)
            {
                bool pressed = slot.Action.WasPressedThisFrame();

                slot.IsHeld = slot.Action.IsPressed();

                if (slot.Action.WasReleasedThisFrame() && slot.HeldFor > 0f)
                    slot.ReleasedAfter = slot.HeldFor;

                if (pressed)
                {
                    slot.BufferLeft = slot.Ability.pressBuffer;
                    slot.HasFired = false;
                    slot.WasExplained = false;
                    return;
                }

                if (slot.Ability.chargesOnHold)
                    return;

                CountDownBuffer(slotIndex, slot, deltaTime);
            }

            void CountDownBuffer(int slotIndex, Slot slot, float deltaTime)
            {
                float bufferBefore = slot.BufferLeft;
                slot.BufferLeft -= deltaTime;

                if (bufferBefore > 0f && slot.BufferLeft <= 0f && !slot.HasFired)
                    Explain(slotIndex, slot.Ability, DescribeRefusal(slot));
            }

            string DescribeRefusal(Slot slot)
            {
                if (slot.CooldownLeft > 0f)
                    return $"it is still cooling down, {slot.CooldownLeft:0.0}s to go";

                if (!slot.HasUsesLeft)
                    return "it has no casts left in this level";

                return slot.Ability.WhyNot(owner);
            }

            void AdvanceCharge(int slotIndex, Slot slot, float fixedDeltaTime)
            {
                if (slot.ReleasedAfter >= 0f)
                {
                    ReleaseCharge(slot);
                    return;
                }

                if (!slot.IsHeld || !slot.IsReady)
                {
                    slot.HeldFor = 0f;
                    return;
                }

                slot.HeldFor += fixedDeltaTime;
                slot.Ability.OnHeld(owner, slot.HeldFor, fixedDeltaTime);

                ExplainRefusedHold(slotIndex, slot);
            }

            void ReleaseCharge(Slot slot)
            {
                float heldSeconds = slot.ReleasedAfter;

                slot.ReleasedAfter = NotReleased;
                slot.HeldFor = 0f;

                slot.Ability.OnReleased(owner, heldSeconds);
            }

            void ExplainRefusedHold(int slotIndex, Slot slot)
            {
                if (slot.WasExplained || slot.HeldFor < ExplainAfter || slot.Ability.CanCast(owner))
                    return;

                slot.WasExplained = true;
                Explain(slotIndex, slot.Ability, DescribeRefusal(slot));
            }

            void TryCastBufferedPress(Slot slot)
            {
                if (slot.BufferLeft <= 0f || !slot.IsReady)
                    return;

                if (!slot.Ability.CanCast(owner))
                    return;

                if (!slot.Ability.OnCast(owner))
                    return;

                slot.BufferLeft = 0f;
                slot.BeginCast();
            }

            public class Slot
            {
                public float BufferLeft;
                public bool HasFired;

                public int Rank;

                public bool IsHeld;
                public float HeldFor;

                public bool WasExplained;

                public float ReleasedAfter = NotReleased;
                public float LitLeft;
                public float CooldownLeft;

                public Ability Ability { get; private set; }

                public InputAction Action { get; private set; }

                public int UsesLeft { get; private set; }

                public bool IsLit => LitLeft > 0f;

                public bool HasUsesLeft =>
                    Ability == null || Ability.usesPerLevel <= 0 || UsesLeft > 0;

                public bool IsReady => Ability != null && CooldownLeft <= 0f && HasUsesLeft;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;

                public Slot(InputAction action) => Action = action;

                public void Fill(Ability spell)
                {
                    Ability = spell;
                    BufferLeft = 0f;
                    LitLeft = 0f;
                    CooldownLeft = 0f;
                    UsesLeft = spell != null ? spell.usesPerLevel : 0;

                    DropCharge();
                }

                public void DropCharge()
                {
                    IsHeld = false;
                    HeldFor = 0f;
                    ReleasedAfter = NotReleased;
                    WasExplained = false;
                }

                public void BeginCast()
                {
                    HasFired = true;
                    LitLeft = Ability.activeDuration;

                    if (Ability.usesPerLevel > 0)
                        UsesLeft = Mathf.Max(0, UsesLeft - 1);

                    if (LitLeft <= 0f)
                        CooldownLeft = Ability.cooldown;
                }
            }
        }

        public class Modifiers
        {
            public float MoveSpeedMultiplier;
            public float FallSpeedMultiplier;

            public float AirSpeedMultiplier;
            public float AirControlMultiplier;
            public float AirDragMultiplier;

            public bool IsRooted;

            public Modifiers() => Reset();

            public void Reset()
            {
                MoveSpeedMultiplier = 1f;
                FallSpeedMultiplier = 1f;
                AirSpeedMultiplier = 1f;
                AirControlMultiplier = 1f;
                AirDragMultiplier = 1f;
                IsRooted = false;
            }
        }

        public struct Intent
        {
            public Vector2 Move;
            public bool WasJumpPressed;
            public bool IsWalking;
            public bool IsLookingDown;

            public float Lean => Move.y;

            public Command Command => new Command
            {
                Steer = Move.x,
                IsWalking = IsWalking,
            };
        }

        public struct Command
        {
            public float Steer;
            public bool IsWalking;
        }
    }
}
