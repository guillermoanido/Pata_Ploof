using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public abstract class Zone : Item
    {
        protected const float Epsilon = 0.0001f;

        protected const float UnselectedGizmo = 0.5f;
        protected const float SelectedGizmo = 1f;

        [Header("Zone")]
        [Tooltip("Can this reach the wizard while they hang on their staff? Off makes the staff " +
                 "a safe perch.")]
        public bool affectsOnStaff = false;

        [Tooltip("Can this hit a wizard who is already tumbling?")]
        public bool affectsRagdolled = false;

        protected override bool IsContinuous => true;

        protected bool CanAffect(PlayerCharacter wizard)
        {
            PlayerLogic logic = wizard.Logic;

            return logic.health.IsAlive &&
                   (affectsOnStaff || logic.State != PlayerState.OnStaff) &&
                   (affectsRagdolled || logic.State != PlayerState.Ragdoll);
        }

        protected static Color MakeFaded(Color colour, float strength)
        {
            colour.a *= strength;
            return colour;
        }
    }
}
