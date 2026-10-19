using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Checkpoint : Item
    {
        const float SamePlace = 0.01f;

        const float MarkerRadius = 0.25f;

        static readonly Color MarkerColour = new Color(0.98f, 0.86f, 0.42f);

        [Header("Checkpoint")]
        [Tooltip("Where the wizard reappears, relative to this object. Lift it clear of the " +
                 "floor so they do not respawn inside it.")]
        public Vector2 respawnOffset = new Vector2(0f, 0.5f);

        [Tooltip("Optional prefab spawned when this checkpoint is first reached.")]
        public GameObject reachedEffect;

        [Header("Look")]
        [Tooltip("A child object with your lit art, switched on only while this is the live " +
                 "checkpoint the wizard will come back to, and off the rest of the time. Your own " +
                 "art on this object is never touched. Never put the checkpoint itself here, or " +
                 "it switches itself off. Leave it empty and nothing changes when it is reached.")]
        public GameObject litLook;

        Vector2 RespawnPoint => (Vector2)transform.position + respawnOffset;

        bool IsLive =>
            Progress.IsCheckpointHere &&
            (Progress.CheckpointPoint - RespawnPoint).sqrMagnitude < SamePlace * SamePlace;

        void OnValidate()
        {
            if (WouldSwitchItselfOff(litLook))
                Debug.LogWarning($"{name}: the Lit Look is the checkpoint itself or one of its " +
                                 "parents, so switching that look off would switch the whole " +
                                 "checkpoint off with it. Make the art a child of the checkpoint " +
                                 "and drag the child in instead.", this);
        }

        void Awake() => ShowLook(litLook, IsLive);

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (!IsLive)
                BecomeLive();
        }

        void BecomeLive()
        {
            Progress.MarkCheckpoint(RespawnPoint);
            LightOnlyThisOne();

            if (reachedEffect != null)
                Instantiate(reachedEffect, transform.position, Quaternion.identity);
        }

        void LightOnlyThisOne()
        {
            foreach (Checkpoint other in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                ShowLook(other.litLook, other == this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = MarkerColour;
            Gizmos.DrawWireSphere(RespawnPoint, MarkerRadius);
        }
    }
}
