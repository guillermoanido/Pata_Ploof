using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(PlayerCharacter))]
    public class RagdollBumps : MonoBehaviour
    {
        [Header("Effect")]
        [Tooltip("Spawned wherever the tumbling wizard bumps into something - the floor, a wall " +
                 "or a ceiling. The Ragdoll Bump Stars prefab, or any effect of your own. Leave " +
                 "it empty for none.")]
        public GameObject bumpEffect;

        [Tooltip("Seconds before each spawned effect is cleared away. Keep it longer than the " +
                 "effect itself lasts.")]
        [Min(0f)] public float secondsToKeepEachBump = 2f;

        [Header("When")]
        [Tooltip("Slowest hit that still makes stars, in boxes per second, so rolling to a stop " +
                 "does not leave a trail of them. Wall bounces always count.")]
        [Min(0f)] public float minimumBumpSpeed = 3f;

        [Tooltip("Shortest gap between two bursts, in seconds.")]
        [Min(0f)] public float secondsBetweenBumps = 0.15f;

        PlayerCharacter wizard;
        float nextBumpAllowedAt;

        bool IsTumbling => wizard.Logic.State == PlayerState.Ragdoll;

        void Awake() => wizard = GetComponent<PlayerCharacter>();

        void OnEnable() => wizard.Logic.ragdoll.BouncedOffAWall += BumpAt;

        void OnDisable() => wizard.Logic.ragdoll.BouncedOffAWall -= BumpAt;

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsTumbling || collision.contactCount == 0)
                return;

            if (collision.relativeVelocity.magnitude >= minimumBumpSpeed)
                BumpAt(collision.GetContact(0).point);
        }

        void BumpAt(Vector2 point)
        {
            if (bumpEffect == null || Time.time < nextBumpAllowedAt)
                return;

            nextBumpAllowedAt = Time.time + secondsBetweenBumps;

            GameObject bump = Instantiate(bumpEffect, point, Quaternion.identity);
            Destroy(bump, secondsToKeepEachBump);
        }
    }
}
