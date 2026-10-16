using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        [Header("Controls")]
        [Tooltip("A press shorter than this toggles looking down. Hold it longer and the staff raises instead.")]
        [Min(0.01f)] public float tapSeconds = 0.2f;

        [Tooltip("Shown in the HUD slot while the staff is looking down. Empty keeps the normal icon.")]
        public Sprite lookingDownIcon;

        public override Sprite IconFor(PlayerLogic wizard) =>
            wizard.IsStaffLookingDown && lookingDownIcon != null ? lookingDownIcon : icon;

        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats) =>
            wizard.SelectStaff(wizard.spellbook.RankOf(this));

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.IsOnStaff || wizard.IsStaffLookingDown ||
            (wizard.IsStaffFree && (wizard.movement.IsAtEdge || wizard.CanClimbHere));

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            Aim aim = wizard.spellbook.StateOf<Aim>(this);

            if (!aim.isPressed)
            {
                aim.isPressed = true;
                aim.hasLetGoOfTheStaff = wizard.IsOnStaff;

                if (aim.hasLetGoOfTheStaff)
                    wizard.DropFromStaff();
            }

            if (aim.hasLetGoOfTheStaff || heldSeconds < tapSeconds || !wizard.IsStaffFree)
                return;

            if (wizard.IsStaffLookingDown)
                wizard.AimStaffDown(false);

            wizard.RaiseStaff();
            wizard.TryClimbStaff();
        }

        public override void OnReleased(PlayerLogic wizard, float heldSeconds)
        {
            Aim aim = wizard.spellbook.StateOf<Aim>(this);

            bool tapped = heldSeconds <= tapSeconds && !aim.hasLetGoOfTheStaff;

            aim.Clear();

            if (tapped)
            {
                wizard.AimStaffDown(!wizard.IsStaffLookingDown);
                return;
            }

            LowerStaffUnlessLookingDown(wizard);
        }

        public override void OnChargeLost(PlayerLogic wizard)
        {
            Aim aim = wizard.spellbook.StateOf<Aim>(this);

            aim.Clear();
            LowerStaffUnlessLookingDown(wizard);
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!wizard.HasPole)
                return "there is no Staff object under the wizard to plant";

            if (!wizard.Pole.HasHook)
                return "the staff has no Climb Check collider, so it has nothing to look with";

            if (wizard.Pole.IsPlanted && wizard.Pole.Mode != StaffMode.Ladder)
                return "the staff is already out, laid flat";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            if (!wizard.Pole.IsReady)
                return "the staff is still being brought back to hand, " +
                       $"{wizard.Pole.CooldownLeft:0.00}s to go";

            if (wizard.movement.TryFindLedgeEdge(out _))
                return "the drop here is too shallow for the staff to reach down into";

            return ExplainWhyNoClimb(wizard.movement);
        }

        static void LowerStaffUnlessLookingDown(PlayerLogic wizard)
        {
            if (!wizard.IsStaffLookingDown)
                wizard.LowerStaff();
        }

        static string ExplainWhyNoClimb(PlayerLogic.Movement movement)
        {
            switch (movement.WhyNoClimb)
            {
                case PlayerLogic.Movement.ClimbRefusal.NotStanding:
                    return movement.catchLedgesInTheAir
                        ? "there is nothing here to raise the staff against"
                        : "you are not stood on anything to raise the staff from";

                case PlayerLogic.Movement.ClimbRefusal.NoWall:
                    return "the staff's hook is not touching anything - walk closer, or move " +
                           "the Climb Check collider so it reaches further out";

                case PlayerLogic.Movement.ClimbRefusal.NothingOnTop:
                    return "the hook is touching something, but there is no surface inside the " +
                           "hook itself to catch on - it is against a face, not a lip";

                case PlayerLogic.Movement.ClimbRefusal.TooTall:
                    return $"what the hook caught carries on above it, so there is no top to " +
                           $"climb onto - the hook reaches {movement.ClimbReach:0.00} boxes up";

                case PlayerLogic.Movement.ClimbRefusal.NoRoomOnTop:
                    return $"the top is {movement.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but the wizard does not fit standing on it - something is in " +
                           "the way just past the lip";

                case PlayerLogic.Movement.ClimbRefusal.NoHeadroom:
                    return $"the top is {movement.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but something is in the way directly above the wizard's head";
            }

            return null;
        }

        class Aim
        {
            public bool isPressed;
            public bool hasLetGoOfTheStaff;

            public void Clear()
            {
                isPressed = false;
                hasLetGoOfTheStaff = false;
            }
        }
    }
}
