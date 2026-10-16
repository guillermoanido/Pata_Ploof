using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class PlayerTrigger : MonoBehaviour
    {
        const float NoStepYet = -1f;

        [NonSerialized] float lastStep = NoStepYet;

        protected virtual bool IsContinuous => false;

        void OnTriggerEnter2D(Collider2D other) => NotifyIfPlayerEntered(other);

        void OnCollisionEnter2D(Collision2D collision) => NotifyIfPlayerEntered(collision.collider);

        void OnTriggerStay2D(Collider2D other)
        {
            if (IsContinuous)
                NotifyIfPlayerInside(other);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (IsContinuous)
                NotifyIfPlayerInside(collision.collider);
        }

        protected abstract void OnPlayerEntered(PlayerCharacter wizard);

        protected virtual void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime) { }

        void NotifyIfPlayerEntered(Collider2D other)
        {
            PlayerCharacter wizard = FindWizardOncePerStep(other);

            if (wizard != null)
                OnPlayerEntered(wizard);
        }

        void NotifyIfPlayerInside(Collider2D other)
        {
            PlayerCharacter wizard = FindWizardOncePerStep(other);

            if (wizard != null)
                OnPlayerInside(wizard, Time.fixedDeltaTime);
        }

        PlayerCharacter FindWizardOncePerStep(Collider2D other)
        {
            PlayerCharacter wizard = FindTheWizard(other);

            if (wizard == null || Mathf.Approximately(Time.fixedTime, lastStep))
                return null;

            lastStep = Time.fixedTime;
            return wizard;
        }

        static PlayerCharacter FindTheWizard(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body == null)
                return null;

            var wizard = body.GetComponent<PlayerCharacter>();
            return wizard != null && other == wizard.Hitbox ? wizard : null;
        }
    }
}
