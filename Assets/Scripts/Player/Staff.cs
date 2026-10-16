using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    public enum StaffMode
    {
        Ladder,

        Bridge,
    }

    public enum StaffAim
    {
        Neutral,

        Raised,

        LookingDown,
    }

    public enum StaffHold
    {
        Holding,
        BackOnLedge,
        LetGo,
    }

    [RequireComponent(typeof(BoxCollider2D))]
    public class Staff : MonoBehaviour
    {
        static readonly Color HitboxColour = new Color(0.4f, 0.8f, 1f);
        static readonly Color ClimbCheckColour = new Color(0.4f, 1f, 0.5f);
        static readonly Color BridgeColour = new Color(1f, 0.6f, 0.2f);

        [Header("Rig")]
        [Tooltip("The pole's hitbox. Its height is the mechanic: it decides how far the wielder " +
                 "travels down or back up. Empty uses the collider on this object.")]
        public Collider2D hitbox;

        [Tooltip("A SOLID collider on a child, on the Ground layer, switched on only while the " +
                 "staff is a bridge. Empty means the bridge spell has nothing to stand on.")]
        public Collider2D bridgeCollider;

        [Tooltip("The hook at the top of the staff. The climb looks with it; the code only reads where it is.")]
        public Collider2D climbCheck;

        [Header("Behaviour")]
        public Pole pole = new Pole();

        bool isBound;

        public Pole Logic
        {
            get
            {
                Bind();
                return pole;
            }
        }

        void OnValidate() => pole.Validate();

        void Awake()
        {
            Bind();

            if (bridgeCollider != null)
                bridgeCollider.enabled = false;
        }

        void LateUpdate() => pole.HoldPolePosition();

        void Bind()
        {
            if (isBound)
                return;

            isBound = true;

            if (hitbox == null)
                hitbox = GetComponent<Collider2D>();

            if (hitbox == null)
            {
                Debug.LogError($"'{name}' has no hitbox, so it has no reach and cannot be climbed. " +
                               "Add a Collider2D to it.", this);
                return;
            }

            pole.BindPole(hitbox, GetComponentInChildren<SpriteRenderer>(true), bridgeCollider,
                climbCheck);
        }

        void OnDrawGizmosSelected()
        {
            DrawOutline(hitbox, HitboxColour);
            DrawOutline(climbCheck, ClimbCheckColour);

            if (bridgeCollider != null && bridgeCollider.enabled)
                DrawOutline(bridgeCollider, BridgeColour);

            pole.DrawGizmos();
        }

        static void DrawOutline(Collider2D shape, Color colour)
        {
            if (shape == null)
                return;

            Gizmos.color = colour;
            Gizmos.DrawWireCube(shape.bounds.center, shape.bounds.size);
        }

        [Serializable]
        public class Pole
        {
            const float Epsilon = 0.01f;

            const float MinScale = 0.0001f;
            const float MinSlideSpeed = 0.01f;
            const float TipMarkerRadius = 0.12f;

            const float QuarterTurn = 90f;

            const float LandingSlack = 0.06f;

            const float CeilingSlack = 0.02f;

            const float FloorSlack = 0.05f;

            const float Half = 0.5f;

            const float NotMounting = -1f;

            const int FacingLeft = -1;
            const int FacingRight = 1;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>();
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>();

            [Header("Climbing")]
            [Tooltip("How fast the wielder slides along the pole, in boxes per second.")]
            [Min(MinSlideSpeed)] public float slideSpeed = 3f;

            [Tooltip("Depth over which the wielder swings from the ledge onto the pole, so joining " +
                     "it is not a snap. How far out they end up is where the pole was driven in.")]
            [Min(0f)] public float swingDepth = 0.5f;

            [Tooltip("Stick tilt needed before up or down counts as climbing.")]
            [Range(0f, 1f)] public float leanThreshold = 0.5f;

            [Tooltip("Seconds of held down input at the very bottom before letting go. Short " +
                     "enough to feel instant, long enough that sliding down is not a drop.")]
            [Min(0f)] public float dropHoldTime = 0.2f;

            [Tooltip("Seconds after the staff is released before it can be planted again.")]
            [Min(0f)] public float cooldown = 0.5f;

            [Tooltip("How far the staff dips while looking down, in boxes. A tell for the player, nothing more.")]
            [Min(0f)] public float dipHeight = 0.25f;

            [Tooltip("How far the staff is lifted overhead, in boxes. Part of the reach, not added to it.")]
            [Min(0f)] public float raiseHeight = 0.6f;

            [Tooltip("How fast the staff moves between its normal spot and its raised one, in boxes per second. 0 snaps.")]
            [Min(0f)] public float aimSpeed = 8f;

            [Tooltip("Seconds spent stepping over the lip at the top of a climb. The wizard rises " +
                     "clear of the edge before sliding across it, so they arc over the corner " +
                     "instead of cutting through it - the same easing the descent uses to swing " +
                     "onto the pole, run the other way. 0 snaps them over as it used to.")]
            [Min(0f)] public float mountSeconds = 0.18f;

            [Header("Planting")]
            [Tooltip("How far past the lip of the ledge the pole is driven in, so it hangs clear " +
                     "of the ledge face instead of scraping down it.")]
            public float lipClearance = 0.15f;

            [Tooltip("How far above their middle the wielder grips. They can lower themselves " +
                     "until that grip reaches the very end of the pole, so the last stretch is a " +
                     "hand hang with the body dangling past the tip. Higher grip, lower hang.")]
            public float gripHeight = 0.25f;

            [Tooltip("Which layers the staff can find footing on, so it never lowers into solid " +
                     "ground. Defaults to Ground.")]
            public LayerMask groundLayers = Layers.GroundMask;

            [NonSerialized] Collider2D hitbox;
            [NonSerialized] Collider2D bridge;
            [NonSerialized] Transform poleTransform;
            [NonSerialized] SpriteRenderer visual;
            [NonSerialized] Transform carriedParent;
            [NonSerialized] Vector3 restPosition;
            [NonSerialized] float sideOffset;

            [NonSerialized] Rigidbody2D wielder;
            [NonSerialized] Collider2D wielderHitbox;
            [NonSerialized] RigidbodyType2D wielderBodyType;

            [NonSerialized] Vector2 anchor;
            [NonSerialized] Vector3 plantedPosition;
            [NonSerialized] Quaternion plantedRotation = Quaternion.identity;
            [NonSerialized] float reach;
            [NonSerialized] float depth;
            [NonSerialized] float dropTimer;
            [NonSerialized] float mountTimer = NotMounting;
            [NonSerialized] Vector2 mountFrom;

            [NonSerialized] float readyAt;

            [NonSerialized] StaffAim aim;

            [NonSerialized] bool isClimbingUp;
            [NonSerialized] Vector2 climbLanding;

            [NonSerialized] float climbHangX;

            [NonSerialized] int facing = FacingRight;

            [NonSerialized] Collider2D hook;
            [NonSerialized] float hookSideOffset;
            [NonSerialized] float poleBoxOffset;
            [NonSerialized] float ridingOffset;

            public bool IsPlanted { get; private set; }

            public bool IsReady => Time.time >= readyAt;

            public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

            public bool IsClimbing => IsPlanted && isClimbingUp;

            public StaffMode Mode { get; private set; } = StaffMode.Ladder;

            public bool HasPole => hitbox != null;

            public bool IsAtTop => depth <= Epsilon;
            public bool IsAtBottom => depth >= reach - Epsilon;

            public Vector2 HangPosition => FindHangPositionAt(depth);

            public bool HasHook => hook != null;

            public Bounds HookBounds => hook != null ? hook.bounds : default;

            bool HasWielder => wielder != null;

            bool IsMounting => mountTimer >= 0f;

            float ClimbUpHeight =>
                hook != null && wielderHitbox != null
                    ? hook.bounds.max.y - wielderHitbox.bounds.min.y
                    : 0f;

            float WielderFeetOffset =>
                wielder != null && wielderHitbox != null
                    ? wielder.position.y - wielderHitbox.bounds.min.y
                    : 0f;

            float HangBelowTip => WielderFeetOffset + gripHeight;

            ContactFilter2D GroundFilter => Layers.MakeSolidOnlyFilter(groundLayers);

            public void BindPole(Collider2D poleHitbox, SpriteRenderer poleVisual,
                Collider2D bridgeCollider, Collider2D hookCollider)
            {
                hitbox = poleHitbox;
                visual = poleVisual;
                bridge = bridgeCollider;
                hook = hookCollider;
                poleTransform = poleHitbox != null ? poleHitbox.transform : null;

                if (poleTransform == null)
                    return;

                restPosition = poleTransform.localPosition;
                carriedParent = poleTransform.parent;
                sideOffset = Mathf.Abs(restPosition.x);

                if (hook is BoxCollider2D hookBox)
                    hookSideOffset = Mathf.Abs(hookBox.offset.x);

                if (hitbox is BoxCollider2D carriedBox)
                    poleBoxOffset = carriedBox.offset.x;
            }

            public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
            {
                wielder = body;
                wielderHitbox = bodyHitbox;

                if (body != null)
                    wielderBodyType = body.bodyType;

                readyAt = 0f;
            }

            public void Aim(StaffAim next)
            {
                if (IsPlanted || aim == next)
                    return;

                aim = next;
                ShoulderPole();
            }

            public void Face(int wielderFacing)
            {
                if (IsPlanted || wielderFacing == 0)
                    return;

                facing = wielderFacing < 0 ? FacingLeft : FacingRight;
                ShoulderPole();
            }

            public void Validate()
            {
                slideSpeed = Mathf.Max(MinSlideSpeed, slideSpeed);
                raiseHeight = Mathf.Max(0f, raiseHeight);
                swingDepth = Mathf.Max(0f, swingDepth);
                dropHoldTime = Mathf.Max(0f, dropHoldTime);
                cooldown = Mathf.Max(0f, cooldown);
            }

            public bool TryPlant(StaffMode mode, int wielderFacing, float edgeX) =>
                mode == StaffMode.Bridge
                    ? TryPlantAsBridge(wielderFacing, edgeX)
                    : TryPlantAsLadder(wielderFacing, edgeX);

            public bool TryPlantAsClimb(int wielderFacing, Vector2 lip, Vector2 landing)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = new Vector2(wielder.position.x, lip.y + WielderFeetOffset);

                PlantPoleAt(lip.x - facing * lipClearance, lip.y - MeasureTopAboveOrigin(),
                    Quaternion.identity);

                depth = anchor.y - wielder.position.y;
                float climbHeight = ClimbUpHeight;

                if (depth <= Epsilon || depth > climbHeight)
                {
                    ShoulderPole();
                    return false;
                }

                SetUpTheClimb(landing);
                HangWielderOnTheLadder();
                return true;
            }

            public StaffHold Slide(float lean, float fixedDeltaTime)
            {
                if (!IsPlanted || !HasWielder || Mode != StaffMode.Ladder)
                    return StaffHold.LetGo;

                if (IsMounting)
                    return Mount(fixedDeltaTime);

                float pull = Mathf.Abs(lean) > leanThreshold ? lean : 0f;

                depth = Mathf.Clamp(depth - pull * slideSpeed * fixedDeltaTime, 0f, reach);
                wielder.MovePosition(FindHangPositionAt(depth));

                if (IsAtTop && lean > leanThreshold)
                    return LeaveByTheTop();

                return LetGoIfHeldDown(lean, fixedDeltaTime);
            }

            public void Release(bool arrived = false)
            {
                bool wasLadder = IsPlanted && Mode == StaffMode.Ladder;
                bool wasPlanted = IsPlanted;

                bool toppedOut = arrived && IsPlanted && isClimbingUp && IsAtTop;

                ClearPlantedState();
                BringPoleBackToHand();

                if (wasPlanted)
                    readyAt = Time.time + cooldown;

                if (!HasWielder)
                    return;

                if (wasLadder)
                    FreeTheWielder(toppedOut);
            }

            public void HoldPolePosition()
            {
                if (poleTransform == null)
                    return;

                if (IsPlanted)
                {
                    poleTransform.SetPositionAndRotation(plantedPosition, plantedRotation);
                    return;
                }

                ShoulderPole();
            }

            void ShoulderPole()
            {
                if (poleTransform == null)
                    return;

                float wanted = FindAimHeight();

                ridingOffset = aimSpeed <= 0f
                    ? wanted
                    : Mathf.MoveTowards(ridingOffset, wanted, aimSpeed * Time.deltaTime);

                CarryPole();
            }

            float FindAimHeight()
            {
                if (aim == StaffAim.Raised)
                    return FindRaiseThatFits();

                if (aim == StaffAim.LookingDown)
                    return -dipHeight;

                return 0f;
            }

            float FindRaiseThatFits()
            {
                if (poleTransform == null || hook == null || raiseHeight <= Epsilon)
                    return raiseHeight;

                FindHookAtRest(out Vector2 centre, out Vector2 size);

                var from = new Vector2(centre.x, centre.y + size.y * Half - CeilingSlack * Half);
                var probe = new Vector2(size.x, CeilingSlack);

                if (!TryFindGround(from, probe, Vector2.up, raiseHeight + CeilingSlack,
                        out float ceilingDistance))
                    return raiseHeight;

                return Mathf.Clamp(ceilingDistance - CeilingSlack, 0f, raiseHeight);
            }

            void FindHookAtRest(out Vector2 centre, out Vector2 size)
            {
                MeasureLocalBox(hook, out Vector2 localCentre, out Vector2 localSize);

                Transform hookTransform = hook.transform;
                var shoulder = new Vector3(poleTransform.localPosition.x, restPosition.y,
                    restPosition.z);

                if (poleTransform.parent != null)
                    shoulder = poleTransform.parent.TransformPoint(shoulder);

                centre = (Vector2)(hookTransform.TransformPoint(localCentre) + shoulder -
                                   poleTransform.position);
                size = new Vector2(
                    localSize.x * Mathf.Abs(hookTransform.lossyScale.x),
                    localSize.y * Mathf.Abs(hookTransform.lossyScale.y));
            }

            static void MeasureLocalBox(Collider2D collider2d, out Vector2 centre, out Vector2 size)
            {
                centre = Vector2.zero;
                size = Vector2.zero;

                if (collider2d == null)
                    return;

                if (collider2d is BoxCollider2D box)
                {
                    centre = box.offset;
                    size = box.size;
                    return;
                }

                Bounds bounds = collider2d.bounds;
                Transform owner = collider2d.transform;

                centre = owner.InverseTransformPoint(bounds.center);
                size = new Vector2(
                    bounds.size.x / Mathf.Max(MinScale, Mathf.Abs(owner.lossyScale.x)),
                    bounds.size.y / Mathf.Max(MinScale, Mathf.Abs(owner.lossyScale.y)));
            }

            bool TryFindGround(Vector2 from, Vector2 size, Vector2 direction, float distance,
                out float groundDistance)
            {
                groundDistance = 0f;

                if (Physics2D.BoxCast(from, size, 0f, direction, GroundFilter, Rays, distance) == 0)
                    return false;

                groundDistance = Rays[0].distance;
                return groundDistance > 0f;
            }

            void CarryPole()
            {
                if (poleTransform == null)
                    return;

                if (hitbox is BoxCollider2D carriedBox && carriedBox.offset.x != poleBoxOffset * facing)
                    carriedBox.offset = new Vector2(poleBoxOffset * facing, carriedBox.offset.y);

                float reachOut = FindCarryReachThatFits();

                poleTransform.localPosition = new Vector3(
                    reachOut * facing,
                    restPosition.y + ridingOffset,
                    restPosition.z);

                if (visual != null)
                    visual.flipX = facing < 0;

                float hookOut = (hookSideOffset + (sideOffset - reachOut)) * facing;

                if (hook is BoxCollider2D hookBox && hookBox.offset.x != hookOut)
                    hookBox.offset = new Vector2(hookOut, hookBox.offset.y);
            }

            float FindCarryReachThatFits()
            {
                if (!IsCarriedByTheWielder())
                    return sideOffset;

                MeasureLocalBox(hitbox, out Vector2 poleBox, out Vector2 poleSize);
                MeasureLocalBox(wielderHitbox, out Vector2 hullBox, out Vector2 hullSize);

                float boxAhead = poleBox.x * facing;
                float travel = sideOffset + boxAhead;
                float soles = hullBox.y - hullSize.y * Half + FloorSlack;
                float tip = restPosition.y + poleBox.y + poleSize.y * Half;

                if (travel <= Epsilon || tip <= soles + Epsilon)
                    return sideOffset;

                Vector2 from = poleTransform.parent.TransformPoint(
                    new Vector3(0f, (soles + tip) * Half, restPosition.z));

                var probe = new Vector2(poleSize.x, tip - soles);

                if (!TryFindGround(from, probe, new Vector2(facing, 0f), travel,
                        out float wallDistance))
                    return sideOffset;

                return Mathf.Clamp(wallDistance - boxAhead, 0f, sideOffset);
            }

            bool IsCarriedByTheWielder()
            {
                if (poleTransform == null || hitbox == null || wielderHitbox == null)
                    return false;

                return poleTransform.parent != null &&
                       wielderHitbox.transform == poleTransform.parent;
            }

            bool TryPlantAsLadder(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = wielder.position;
                depth = 0f;
                dropTimer = 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                float topAboveOrigin = MeasureTopAboveOrigin();

                PlantPoleAt(edgeX + facing * lipClearance, surfaceY - topAboveOrigin,
                    Quaternion.identity);

                reach = FindReachThatFits(MeasureReach() + HangBelowTip);

                if (reach <= Epsilon)
                {
                    ShoulderPole();
                    return false;
                }

                HangWielderOnTheLadder();
                return true;
            }

            float MeasureTopAboveOrigin()
            {
                MeasureLocalBox(hitbox, out Vector2 centre, out Vector2 size);
                float scale = Mathf.Abs(poleTransform.lossyScale.y);
                return (centre.y + size.y * Half) * scale;
            }

            void PlantPoleAt(float x, float y, Quaternion rotation)
            {
                plantedRotation = rotation;
                plantedPosition = new Vector3(x, y, poleTransform.position.z);

                poleTransform.SetPositionAndRotation(plantedPosition, plantedRotation);
            }

            float MeasureReach()
            {
                if (hitbox == null || poleTransform == null)
                    return 0f;

                MeasureLocalBox(hitbox, out _, out Vector2 size);
                return size.y * Mathf.Abs(poleTransform.lossyScale.y);
            }

            float FindReachThatFits(float rawReach)
            {
                if (rawReach <= Epsilon)
                    return 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                var origin = new Vector2(plantedPosition.x, surfaceY);

                if (Physics2D.Raycast(origin, Vector2.down, GroundFilter, Rays, rawReach) == 0)
                    return rawReach;

                return Mathf.Clamp(surfaceY - Rays[0].point.y, 0f, rawReach);
            }

            void HangWielderOnTheLadder()
            {
                wielderBodyType = wielder.bodyType;
                wielder.linearVelocity = Vector2.zero;
                wielder.bodyType = RigidbodyType2D.Kinematic;

                Mode = StaffMode.Ladder;
                IsPlanted = true;
            }

            bool TryPlantAsBridge(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                if (bridge == null)
                {
                    Debug.LogWarning("The staff has no bridge collider, so there is nothing to " +
                                     "stand on. Assign one on the Staff component.");
                    return false;
                }

                Face(wielderFacing);

                anchor = wielder.position;
                float surfaceY = anchor.y - WielderFeetOffset;

                LayPoleFlat(edgeX, surfaceY);

                bridge.enabled = true;
                reach = 0f;
                depth = 0f;

                Mode = StaffMode.Bridge;
                IsPlanted = true;
                return true;
            }

            void LayPoleFlat(float edgeX, float surfaceY)
            {
                MeasureLocalBox(bridge, out Vector2 localCentre, out Vector2 localSize);

                float scaleX = Mathf.Abs(poleTransform.lossyScale.x);
                float scaleY = Mathf.Abs(poleTransform.lossyScale.y);
                float length = localSize.y * scaleY;
                float thickness = localSize.x * scaleX;

                if (poleTransform.parent != null)
                    poleTransform.SetParent(null, true);

                Quaternion flatRotation =
                    Quaternion.Euler(0f, 0f, facing > 0 ? -QuarterTurn : QuarterTurn);

                var carried = new Vector2(
                    facing * localCentre.y * scaleY,
                    -facing * localCentre.x * scaleX);

                var wanted = new Vector2(
                    edgeX + facing * (lipClearance + length * Half),
                    surfaceY - thickness * Half);

                PlantPoleAt(wanted.x - carried.x, wanted.y - carried.y, flatRotation);
            }

            void SetUpTheClimb(Vector2 landing)
            {
                reach = depth;

                isClimbingUp = true;
                mountTimer = NotMounting;
                climbLanding = landing;
                climbHangX = wielder.position.x;

                dropTimer = -dropHoldTime;
            }

            StaffHold Mount(float fixedDeltaTime)
            {
                mountTimer += fixedDeltaTime;

                float mountProgress = Mathf.Clamp01(mountTimer / mountSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, mountProgress);

                wielder.MovePosition(new Vector2(
                    Mathf.Lerp(mountFrom.x, climbLanding.x, eased * eased),
                    Mathf.Lerp(mountFrom.y, climbLanding.y, Mathf.Sqrt(eased))));

                if (mountProgress < 1f)
                    return StaffHold.Holding;

                mountTimer = NotMounting;
                return StaffHold.BackOnLedge;
            }

            Vector2 FindHangPositionAt(float atDepth)
            {
                if (isClimbingUp)
                    return new Vector2(climbHangX, anchor.y - atDepth);

                float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

                return new Vector2(
                    Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                    anchor.y - atDepth);
            }

            StaffHold LeaveByTheTop()
            {
                if (!isClimbingUp || mountSeconds <= Epsilon || !IsLandingClear())
                    return StaffHold.BackOnLedge;

                mountFrom = wielder.position;
                mountTimer = 0f;
                return StaffHold.Holding;
            }

            bool IsLandingClear()
            {
                if (wielderHitbox == null || wielder == null)
                    return true;

                Bounds box = wielderHitbox.bounds;
                Vector2 hullAt = climbLanding + ((Vector2)box.center - wielder.position);

                var room = (Vector2)box.size - Vector2.one * LandingSlack;

                if (Physics2D.OverlapBox(hullAt, room, 0f, GroundFilter, Overlaps) == 0)
                    return true;

#if UNITY_EDITOR
                Debug.LogWarning("A staff climb reached the top and then could not be set down " +
                                 "on it, so the wizard was let go in mid-air and fell. Something " +
                                 "on the ground layer is in the way just past the lip.", poleTransform);
#endif

                return false;
            }

            StaffHold LetGoIfHeldDown(float lean, float fixedDeltaTime)
            {
                if (IsAtBottom && lean < -leanThreshold)
                {
                    dropTimer += fixedDeltaTime;

                    if (dropTimer >= dropHoldTime)
                        return StaffHold.LetGo;
                }
                else
                {
                    dropTimer = 0f;
                }

                return StaffHold.Holding;
            }

            void ClearPlantedState()
            {
                IsPlanted = false;
                isClimbingUp = false;
                mountTimer = NotMounting;
                aim = StaffAim.Neutral;
                ridingOffset = 0f;
                dropTimer = 0f;
            }

            void BringPoleBackToHand()
            {
                if (bridge != null)
                    bridge.enabled = false;

                if (poleTransform != null)
                {
                    poleTransform.localRotation = Quaternion.identity;

                    if (poleTransform.parent != carriedParent)
                        poleTransform.SetParent(carriedParent, false);
                }

                plantedRotation = Quaternion.identity;
                Mode = StaffMode.Ladder;
                ShoulderPole();
            }

            void FreeTheWielder(bool toppedOut)
            {
                if (toppedOut && IsLandingClear())
                    wielder.position = climbLanding;

                wielder.bodyType = wielderBodyType;
                wielder.linearVelocity = Vector2.zero;
            }

            public void DrawGizmos()
            {
                if (poleTransform == null)
                    return;

                if (!IsPlanted)
                {
                    DrawCarryGizmos();
                    DrawRaiseGizmos();
                    return;
                }

                if (Mode != StaffMode.Ladder)
                    return;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(FindHangPositionAt(0f), FindHangPositionAt(reach));

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(FindHangPositionAt(reach), TipMarkerRadius);
            }

            void DrawCarryGizmos()
            {
                if (hitbox == null || poleTransform.parent == null)
                    return;

                MeasureLocalBox(hitbox, out Vector2 poleBox, out Vector2 poleSize);

                Vector3 authored = poleTransform.parent.TransformPoint(new Vector3(
                    sideOffset * facing + poleBox.x,
                    restPosition.y + ridingOffset + poleBox.y,
                    restPosition.z));

                Vector3 actual = poleTransform.parent.TransformPoint(new Vector3(
                    poleTransform.localPosition.x + poleBox.x,
                    poleTransform.localPosition.y + poleBox.y,
                    restPosition.z));

                Gizmos.color = Color.grey;
                Gizmos.DrawWireCube(authored, poleSize);

                Gizmos.color = authored == actual ? Color.cyan : Color.red;
                Gizmos.DrawWireCube(actual, poleSize);
                Gizmos.DrawLine(authored, actual);
            }

            void DrawRaiseGizmos()
            {
                if (hook == null)
                    return;

                FindHookAtRest(out Vector2 centre, out Vector2 size);

                Gizmos.color = Color.grey;
                Gizmos.DrawWireCube(centre, size);

                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(centre + Vector2.up * raiseHeight, size);

                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(centre + Vector2.up * FindRaiseThatFits(), size);
            }
        }
    }
}
