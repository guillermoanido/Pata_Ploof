using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    [DefaultExecutionOrder(RunBeforeTheWizard)]
    public class MovingPlatform : MonoBehaviour
    {
        const int RunBeforeTheWizard = -50;

        const float ShortestPath = 0.01f;

        static readonly Color PathColour = new Color(0.86f, 0.62f, 0.3f);

        [Header("Path")]
        [Tooltip("Where the far end of the ride is, in boxes, measured from where you placed the " +
                 "platform. (4, 0) slides four boxes to the right and back, (0, -3) sinks three " +
                 "boxes and comes back up, and (3, 2) rides a diagonal. The scene view draws the " +
                 "path and the platform's outline at the far end, so you can line that outline " +
                 "up with the ledge it should meet. It glides straight through walls and floors, " +
                 "so keep the path clear, with a box of headroom above it the whole way, or a " +
                 "wizard riding it gets jammed into the ceiling.")]
        public Vector2 travel = new Vector2(4f, 0f);

        [Tooltip("How fast it travels along the path, in boxes per second. A wizard stood on it " +
                 "rides along at this speed and runs on top of it, so running the way it is going " +
                 "is quicker than a run on solid ground. The Haste spell slows it down with the " +
                 "rest of the world. 0 holds it still.")]
        [Min(0f)] public float speed = 2f;

        [Tooltip("Seconds it waits at each end before heading back. This is the calm moment to " +
                 "step on or off, so be generous on a platform the player has to board from a " +
                 "ledge. The Haste spell stretches the wait as well.")]
        [Min(0f)] public float pauseSeconds = 0.5f;

        [Tooltip("How far along the path it starts when the level begins, from 0 (where you " +
                 "placed it) to 1 (the far end). The path is always measured from where you " +
                 "placed it. Matching platforms left at the same number move together; give them " +
                 "different numbers and they move out of step, and 0 and 1 make a pair that " +
                 "always heads in opposite directions.")]
        [Range(0f, 1f)] public float startAlong = 0f;

        [NonSerialized] Rigidbody2D body;
        [NonSerialized] Vector2 start;
        [NonSerialized] float along;
        [NonSerialized] bool isHeadingOut = true;
        [NonSerialized] float pauseLeft;

        float PathLength => Mathf.Max(ShortestPath, travel.magnitude);

        bool IsInPlay => Application.isPlaying && body != null;

        Vector2 PathStart => IsInPlay ? start : (Vector2)transform.position;

        Vector2 CurrentPosition => IsInPlay ? body.position : (Vector2)transform.position;

        void Reset()
        {
            var platformBody = GetComponent<Rigidbody2D>();
            platformBody.bodyType = RigidbodyType2D.Kinematic;
            platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;

            gameObject.layer = Layers.Ground;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            start = body.position;

            along = startAlong;
            isHeadingOut = along < 1f;

            PlaceAt(PointAlong(along));
            WarnIfNotOnGround();
        }

        void FixedUpdate()
        {
            MoveAlongThePath();

            Vector2 next = PointAlong(along);
            Vector2 velocity = (next - body.position) / Time.fixedDeltaTime;

            body.MovePosition(next);
            CarryTheWizard(velocity);
        }

        void MoveAlongThePath()
        {
            if (pauseLeft > 0f)
            {
                pauseLeft -= Haste.FixedDeltaTime;
                return;
            }

            float end = isHeadingOut ? 1f : 0f;
            along = Mathf.MoveTowards(along, end, speed * Haste.FixedDeltaTime / PathLength);

            if (Mathf.Approximately(along, end))
                TurnAround();
        }

        void TurnAround()
        {
            isHeadingOut = !isHeadingOut;
            pauseLeft = pauseSeconds;
        }

        void CarryTheWizard(Vector2 velocity)
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null && IsStoodOnBy(wizard))
                wizard.Logic.RideAlong(velocity);
        }

        bool IsStoodOnBy(PlayerCharacter wizard)
        {
            Collider2D underFoot = wizard.Logic.movement.StandingOn;
            return underFoot != null && underFoot.attachedRigidbody == body;
        }

        Vector2 PointAlong(float howFar) => start + travel * howFar;

        void PlaceAt(Vector2 point)
        {
            body.position = point;
            transform.position = new Vector3(point.x, point.y, transform.position.z);
        }

        void WarnIfNotOnGround()
        {
            if (gameObject.layer == Layers.Ground)
                return;

            Debug.LogWarning($"{name} is a Moving Platform but is not on the Ground layer, so the " +
                             "wizard never counts as standing on it: it will not carry them, and " +
                             "they steer on it as if they were in mid-air. Put it on Ground.", this);
        }

        void OnDrawGizmos()
        {
            Vector2 farEnd = PathStart + travel;

            Gizmos.color = PathColour;
            Gizmos.DrawLine(PathStart, farEnd);

            var shape = GetComponent<Collider2D>();

            if (shape == null)
                return;

            Bounds box = shape.bounds;
            Gizmos.DrawWireCube(farEnd + ((Vector2)box.center - CurrentPosition), box.size);
        }
    }
}
