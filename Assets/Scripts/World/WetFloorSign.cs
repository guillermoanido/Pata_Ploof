using FallingWizard.Player;

namespace FallingWizard.World
{
    public class WetFloorSign : Hazard
    {
        const float SlipCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = SlipCooldown;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.TryTrip();
    }
}
