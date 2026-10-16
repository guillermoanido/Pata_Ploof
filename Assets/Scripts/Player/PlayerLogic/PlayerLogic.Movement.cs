using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
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

            [Header("Jumping")]
            [Tooltip("Whether the wizard can jump at all. Switch it OFF and the staff becomes the " +
                     "only way up: a lip shorter than the step assist below is walked over, and " +
                     "anything taller has to be climbed. Nothing else that throws the wizard into " +
                     "the air is affected - a slime, a fling and a bounce all go through Launch " +
                     "instead - and the Jump button keeps its other job of letting go of a vine, " +
                     "which is the only way off one. A spell handing out extra jumps cannot bring " +
                     "it back either.")]
            public bool canJump = false;

            [Tooltip("Height of a full jump, in boxes. The launch speed is worked out from gravity.")]
            [Min(0f)] public float jumpHeight = 2f;

            [Tooltip("Grace period after walking off a ledge where a jump still counts. It is " +
                     "ALSO the window the step assist below works in, so it still earns its keep " +
                     "with jumping switched off - zero it as a dead jump number and the wizard " +
                     "stops walking over tile seams as well.")]
            [Min(0f)] public float coyoteTime = 0.12f;

            [Tooltip("A jump pressed this many seconds before landing still fires on touchdown.")]
            [Min(0f)] public float jumpBuffer = 0.12f;

            [Tooltip("Upward speed kept when the jump button is released early. Lower = shorter hops.")]
            [Range(0f, 1f)] public float shortHopMultiplier = 0.45f;

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
            [NonSerialized] float coyoteTimer;
            [NonSerialized] float bufferTimer;
            [NonSerialized] float highestPoint;
            [NonSerialized] float pendingFallDistance;
            [NonSerialized] bool hasLanded;
            [NonSerialized] bool isRising;
            [NonSerialized] int airJumpsUsed;

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

            public void BufferJump(bool jumpPressedThisFrame, float deltaTime)
            {
                if (jumpPressedThisFrame)
                    bufferTimer = jumpBuffer;
                else
                    bufferTimer -= deltaTime;
            }

            public void FixedTick(Command command, Modifiers stats, float fixedDeltaTime)
            {
                lockout -= fixedDeltaTime;

                UpdateFacing(command.Steer);
                SenseGround(fixedDeltaTime);
                Run(command, stats, fixedDeltaTime);

                StepUpSmallLedges(command, stats);

                JumpIfBuffered(stats);
                ApplyShortHop(command.IsJumpHeld);

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
                coyoteTimer = 0f;
                isRising = false;
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
                isRising = false;

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

                isRising = false;
                isClimbingRamp = false;

                if (!resetsFall)
                    return;

                highestPoint = body.position.y;
                IsGrounded = false;
                coyoteTimer = 0f;
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

                if (isRising || body.linearVelocityY > topSpeed)
                    return false;

                RunAlongRamp(targetSpeed, topSpeed, change);
                return true;
            }

            void TakeBackLeftoverClimb(bool wasClimbing, float topSpeed)
            {
                if (wasClimbing && IsGrounded && !isRising &&
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

                if (IsGrounded && IsOnMovingPlatform && !isRising)
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

                if (coyoteTimer <= 0f || body.linearVelocityY > 0f)
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

            void JumpIfBuffered(Modifiers stats)
            {
                if (!canJump)
                    return;

                bool onGroundOrCoyote = coyoteTimer > 0f;
                bool hasAirJump = airJumpsUsed < stats.ExtraJumps;

                if (bufferTimer <= 0f || (!onGroundOrCoyote && !hasAirJump))
                    return;

                if (!onGroundOrCoyote)
                    airJumpsUsed++;

                bufferTimer = 0f;
                coyoteTimer = 0f;
                isRising = true;

                body.linearVelocityY = GetSpeedToRise(jumpHeight * stats.JumpHeightMultiplier);
            }

            float GetSpeedToRise(float height) =>
                Mathf.Sqrt(KineticEnergyFactor * BaseGravity * height);

            void ApplyShortHop(bool jumpHeld)
            {
                if (!isRising || jumpHeld)
                    return;

                if (body.linearVelocityY > 0f)
                    body.linearVelocityY *= shortHopMultiplier;

                isRising = false;
            }

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
                    "included, which start on Default. Jumping, ledge detection and the staff " +
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

                coyoteTimer = coyoteTime;
                highestPoint = body.position.y;
                airJumpsUsed = 0;
                isRising = false;
            }

            void UpdateWhileInTheAir(float fixedDeltaTime)
            {
                coyoteTimer -= fixedDeltaTime;
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
    }
}
