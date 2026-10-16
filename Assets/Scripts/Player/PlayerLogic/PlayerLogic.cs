using System;
using FallingWizard.Core;
using UnityEngine;

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
    public partial class PlayerLogic
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

            movement.BufferJump(frame.WasJumpPressed, deltaTime);
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
            if (hearts <= 0 || !health.IsAlive || Invulnerable || Stats.IsShielded)
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

        public bool IsStaffPlantedAs(StaffMode mode) =>
            HasPole && pole.IsPlanted && pole.Mode == mode;

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
                    movement.NudgeVelocity(pendingWind * Stats.WindMultiplier * fixedDeltaTime);
                    break;

                default:
                    movement.ApplyWind(pendingWind * Stats.WindMultiplier, pendingRampup,
                        pendingGroundScale, fixedDeltaTime);
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

            Hurt(Mathf.RoundToInt(excess * damagePerBox * Stats.FallDamageMultiplier));
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
    }
}
