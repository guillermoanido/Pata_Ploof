using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Hazard : Zone
    {
        protected const float RunningOnly = 3f;

        [Header("Hazard")]
        [Tooltip("Hazards are things you pass straight through that do something to you on the " +
                 "way, rather than things you bump into. Applied on Awake, so ticking this fixes " +
                 "a hazard already placed in a scene without touching its collider by hand. " +
                 "Untick only for something meant to be solid - and if you do, keep it OFF the " +
                 "Hazard layer, because the ground check ignores that layer and a solid hazard " +
                 "there is something the wizard comes to rest on while the game still believes " +
                 "they are falling.")]
        public bool passThrough = true;

        [Tooltip("Only fires above this speed, in boxes per second. Check the wizard in the " +
                 "scene before picking a number - the class defaults are a run of 6 and a walk " +
                 "of 2, but the Level 1 wizard overrides them to 4 and 2, so 3 is what sets a " +
                 "run off there and never a walk. Never set this TO the run speed: the wizard " +
                 "is hardly ever at exactly top speed once a ramp or a slow spell has touched " +
                 "them, so the hazard would fire or not on the fourth decimal place. This " +
                 "measures SIDEWAYS speed only, so a wizard dropping straight down onto it " +
                 "never counts. 0 always fires.")]
        [Min(0f)] public float minimumSpeed = 0f;

        [Tooltip("Seconds before it can catch the wizard again.")]
        [Min(0f)] public float rearmDelay = 0.5f;

        [Tooltip("Fire on every step the wizard is inside it, rather than only on the way in. " +
                 "Off is right for something you HIT - a slime, a rake, a sign - which happens " +
                 "once and is over. On is right for a PLACE, like a staircase, where breaking " +
                 "into a run half way along should count for as much as arriving at a run. The " +
                 "re-arm delay above is what stops it firing every physics step.")]
        public bool everyStep = false;

        [Tooltip("Hearts taken on contact. 0 for hazards that only shove you around.")]
        [Min(0)] public int damage = 0;

        [Tooltip("Let a WALKING wizard pass untouched. This reads the walk button rather than a " +
                 "speed, so it still fires on a wizard who is falling onto it or shoved into it, " +
                 "and it cannot be fooled by Haste - which lifts a walk to the same 3 boxes a " +
                 "second that Minimum Speed would have to sit at to tell a walk from a run.")]
        public bool ignoresWalking = false;

        [NonSerialized] float readyAt;

        protected virtual bool IsDangerousNow => true;

        protected override bool IsContinuous => everyStep;

        protected virtual void Awake()
        {
            var hitbox = GetComponent<Collider2D>();

            if (hitbox != null)
                hitbox.isTrigger = passThrough;
        }

        public void Disarm(float seconds) => readyAt = Time.time + Mathf.Max(0f, seconds);

        protected virtual void Affect(PlayerLogic wizard) { }

        protected sealed override void OnPlayerEntered(PlayerCharacter wizard) => Catch(wizard);

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime) =>
            Catch(wizard);

        void Catch(PlayerCharacter wizard)
        {
            if (Time.time < readyAt || !IsDangerousNow || !CanAffect(wizard))
                return;

            if (wizard.Logic.movement.ApproachSpeed < minimumSpeed)
                return;

            if (ignoresWalking && IsWalking(wizard))
                return;

            readyAt = Time.time + rearmDelay;

            Affect(wizard.Logic);

            if (damage > 0)
                wizard.Logic.Hurt(damage);
        }

        static bool IsWalking(PlayerCharacter wizard)
        {
            PlayerLogic logic = wizard.Logic;

            return logic.Steering.IsWalking && logic.movement.IsGrounded;
        }
    }
}
