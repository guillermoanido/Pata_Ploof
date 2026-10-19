using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace FallingWizard.World
{
    public class Pickup : Item
    {
        const float KeySteps = 4f;

        const float FullCircle = Mathf.PI * 2f;

        const string WispPrefix = "wisp";
        const string HeartPrefix = "heart";

        [Header("Identity")]
        [Tooltip("Leave empty and this pickup is remembered by where it stands, which survives " +
                 "renaming it and re-parenting it. Moving it makes it a new pickup. Fill it in " +
                 "when two pickups share a spot, or when you want to move one without the game " +
                 "forgetting it was taken.")]
        public string id = "";

        [Header("Look")]
        [Tooltip("Height of the idle bob, in boxes. 0 holds it still. Only the art bobs - the " +
                 "trigger and the object itself never move, because a pickup that drifts would " +
                 "keep changing the name it is remembered by.")]
        [Min(0f)] public float bobHeight = 0.15f;

        [Tooltip("Bobs per second.")]
        [Min(0f)] public float bobSpeed = 1.5f;

        [Tooltip("Optional prefab spawned where it stood.")]
        public GameObject collectedEffect;

        [Header("Gift")]
        [Tooltip("What the wizard gets here. Wisps are carried, and kept only by reaching a rest " +
                 "site and turning back. A Heart is added to their health for good, and is never " +
                 "here again.")]
        public Gift gives = Gift.Wisps;

        [Tooltip("How many. For Wisps, the wisps carried away from here - die anywhere below " +
                 "before turning back and they are gone. For a Heart, the hearts added to the " +
                 "wizard's health, so the hat takes longer to wear down. They are healed on the " +
                 "spot, they survive dying and turning back, and this heart is never here again - " +
                 "which is why the next one has to be found somewhere deeper.")]
        [FormerlySerializedAs("hearts")]
        [Min(1)] public int amount = 1;

        [Tooltip("Hearts only. Wisps given instead when the wizard already has as many hearts as " +
                 "they can ever get. Never leave a player standing over dead loot.")]
        [Min(0)] public int wispsIfFull = 3;

        [NonSerialized] string key;
        [NonSerialized] Transform visual;
        [NonSerialized] Vector3 restingPoint;

        string Key
        {
            get
            {
                if (key == null)
                    key = BuildKey();

                return key;
            }
        }

        string Prefix => gives == Gift.Heart ? HeartPrefix : WispPrefix;

        StaysTaken StaysTaken => gives == Gift.Heart ? StaysTaken.ForGood : StaysTaken.OnceBanked;

        void Awake()
        {
            if (Progress.IsGone(Key))
            {
                Destroy(gameObject);
                return;
            }

            FindArtToBob();
        }

        void Update()
        {
            if (visual == null || bobHeight <= 0f || bobSpeed <= 0f)
                return;

            float rise = Mathf.Sin(Time.time * bobSpeed * FullCircle) * bobHeight;
            visual.localPosition = restingPoint + new Vector3(0f, rise, 0f);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (!wizard.Logic.health.IsAlive || !TryTake(wizard))
                return;

            Progress.MarkFound(Key, StaysTaken);

            if (collectedEffect != null)
                Instantiate(collectedEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }

        string BuildKey()
        {
            string scene = SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(id))
                return $"{Prefix}{scene}:{id}";

            Vector2 point = transform.position;

            return $"{Prefix}{scene}:" +
                   $"{Mathf.RoundToInt(point.x * KeySteps)}," +
                   $"{Mathf.RoundToInt(point.y * KeySteps)}";
        }

        void FindArtToBob()
        {
            var art = GetComponentInChildren<SpriteRenderer>();

            if (art == null || art.transform == transform)
                return;

            visual = art.transform;
            restingPoint = visual.localPosition;
        }

        bool TryTake(PlayerCharacter wizard)
        {
            switch (gives)
            {
                case Gift.Heart:
                    return TryTakeHeart(wizard);
                default:
                    return TryTakeWisps();
            }
        }

        bool TryTakeWisps()
        {
            Progress.CarryWisps(amount);
            return true;
        }

        bool TryTakeHeart(PlayerCharacter wizard)
        {
            if (wizard.Logic.TryGrowHeart(amount))
                return true;

            Progress.CarryWisps(wispsIfFull);
            return true;
        }

        public enum Gift
        {
            Wisps,
            Heart,
        }
    }
}
