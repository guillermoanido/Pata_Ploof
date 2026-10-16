using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class WaxDrop : Hazard
    {
        const int BurnDamage = 1;

        const float SlowestFall = 0.5f;

        static readonly List<RaycastHit2D> Hits = new List<RaycastHit2D>();

        [Header("Drip")]
        [Tooltip("Seconds the drop hangs under the candle before it lets go. A hanging drop is " +
                 "harmless - it is the warning that one is about to fall - so 0 drops it with " +
                 "no tell at all.")]
        [Min(0f)] public float hangSeconds = 0.5f;

        [Tooltip("How fast it falls, in boxes per second. It falls at a steady speed rather " +
                 "than speeding up, so at 6 a drop three boxes above the wizard's head lands " +
                 "in half a second. The Haste spell slows it along with everything else.")]
        [Min(SlowestFall)] public float fallSpeed = 6f;

        [Tooltip("How far a drop may fall, in boxes, before it is removed. This only matters " +
                 "for a drop with nothing under it, such as one over a pit, which would " +
                 "otherwise fall forever. It vanishes without a splash.")]
        [Min(0f)] public float maxFallDistance = 30f;

        [Tooltip("Layers the drop splashes on. Leave it on Ground. Only solid colliders count, " +
                 "so a trigger zone on one of these layers is fallen straight through. Never " +
                 "add the Player layer: the burn comes from touching the wizard, and a drop " +
                 "that splashed on them this way would do no damage.")]
        public LayerMask groundLayers = Layers.GroundMask;

        [Tooltip("Optional prefab spawned where the drop splashes, on the floor or on the " +
                 "wizard. Nothing removes it afterwards, so it has to tidy itself away - a " +
                 "particle system with Stop Action set to Destroy does.")]
        public GameObject splashEffect;

        [NonSerialized] Rigidbody2D body;
        [NonSerialized] float hungFor;
        [NonSerialized] float fallen;
        [NonSerialized] bool falling;
        [NonSerialized] bool gone;

        public bool IsFalling => falling;

        protected override bool IsDangerousNow => IsFalling;

        protected override bool Continuous => true;

        ContactFilter2D GroundFilter => new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundLayers,
            useTriggers = false,
        };

        void Reset()
        {
            damage = BurnDamage;
            affectsOnStaff = true;
            affectsRagdolled = true;

            var rigidBody = GetComponent<Rigidbody2D>();
            rigidBody.bodyType = RigidbodyType2D.Kinematic;
            rigidBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        protected override void Awake()
        {
            base.Awake();

            body = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (gone)
                return;

            if (!falling)
            {
                HangOneStep();
                return;
            }

            FallOneStep();
        }

        void HangOneStep()
        {
            hungFor += Haste.FixedDeltaTime;

            if (hungFor >= hangSeconds)
                falling = true;
        }

        void FallOneStep()
        {
            float step = fallSpeed * Haste.FixedDeltaTime;

            if (Physics2D.Raycast(body.position, Vector2.down, GroundFilter, Hits, step) > 0)
            {
                Splash(Hits[0].point);
                return;
            }

            body.MovePosition(body.position + Vector2.down * step);
            fallen += step;

            if (fallen > maxFallDistance)
                Vanish();
        }

        protected override void Affect(PlayerLogic wizard) => Splash(body.position);

        void Splash(Vector2 point)
        {
            if (gone)
                return;

            if (splashEffect != null)
                Instantiate(splashEffect, point, Quaternion.identity);

            Vanish();
        }

        void Vanish()
        {
            gone = true;
            falling = false;
            Destroy(gameObject);
        }
    }
}
