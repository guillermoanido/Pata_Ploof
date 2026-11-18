using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class FollowCamera : MonoBehaviour
    {
        [Tooltip("Where the camera sits relative to the wizard, in boxes. (0, 0) keeps the wizard " +
                 "in the middle of the screen. A positive Y moves the camera up, so the wizard " +
                 "sits lower on screen.")]
        [SerializeField] Vector2 offset = Vector2.zero;

        [Tooltip("Lower is snappier. Seconds the camera takes to catch up.")]
        [SerializeField] float smoothTime = 0.18f;

        [Header("Peeking")]
        [Tooltip("How far the camera drops while the wizard looks over an edge.")]
        [SerializeField] float peekDistance = 4f;

        [SerializeField] float peekSmoothTime = 0.35f;

        [Header("Looking Ahead While Falling")]
        [Tooltip("How far below a falling wizard the camera looks, in seconds of fall: it drops " +
                 "by the fall speed times this, so the faster they fall, the more of what is " +
                 "coming shows. It also makes up for the camera trailing behind, which at full " +
                 "speed is about 3 boxes. 0 turns looking ahead off.")]
        [Min(0f)] [SerializeField] float lookAheadSeconds = 0.45f;

        [Tooltip("The furthest the camera drops below a falling wizard, in boxes. The screen is " +
                 "10 boxes tall, so 6 keeps a wizard falling at full speed in the top third of " +
                 "it, with most of the screen showing where they will land.")]
        [Min(0f)] [SerializeField] float lookAheadLimit = 6f;

        [Tooltip("Seconds the camera takes to ease into looking ahead, and back out after " +
                 "landing. Lower is snappier; too low and every landing jolts the screen.")]
        [Min(0.01f)] [SerializeField] float lookAheadSmoothTime = 0.3f;

        Vector3 followVelocity;
        float peekDrop;
        float peekVelocity;
        float fallDrop;
        float fallDropVelocity;

        void LateUpdate()
        {
            PlayerCharacter target = PlayerCharacter.Instance;
            if (target == null || !target.Logic.health.IsAlive)
                return;

            float wantedPeek = target.Logic.IsPeeking ? peekDistance : 0f;
            peekDrop = Mathf.SmoothDamp(peekDrop, wantedPeek, ref peekVelocity, peekSmoothTime);

            float wantedFallDrop = FindFallLookAhead(target.Logic);
            fallDrop = Mathf.SmoothDamp(fallDrop, wantedFallDrop, ref fallDropVelocity,
                lookAheadSmoothTime);

            float drop = Mathf.Max(peekDrop, fallDrop);

            Vector3 wantedPosition = new Vector3(
                target.transform.position.x + offset.x,
                target.transform.position.y + offset.y - drop,
                transform.position.z);

            transform.position = Vector3.SmoothDamp(transform.position, wantedPosition,
                ref followVelocity, smoothTime);
        }

        float FindFallLookAhead(PlayerLogic wizard)
        {
            bool isFree = wizard.State == PlayerState.Normal || wizard.State == PlayerState.Ragdoll;
            float fallSpeed = -wizard.movement.Velocity.y;

            if (!isFree || wizard.movement.IsGrounded || fallSpeed <= 0f)
                return 0f;

            return Mathf.Min(fallSpeed * lookAheadSeconds, lookAheadLimit);
        }
    }
}
