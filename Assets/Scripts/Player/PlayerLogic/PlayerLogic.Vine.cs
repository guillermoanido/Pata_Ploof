using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
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
    }
}
