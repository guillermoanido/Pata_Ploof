using FallingWizard.Player;

namespace FallingWizard.World
{
    public class TripHazard : Hazard
    {
        const float TripCooldown = 1f;

        protected override void Reset()
        {
            base.Reset();

            minimumSpeed = RunningOnly;
            rearmDelay = TripCooldown;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.TryTrip();
    }
}
