using FallingWizard.Player;

namespace FallingWizard.World
{
    public class Stairs : Hazard
    {
        const float TumbleCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = TumbleCooldown;
            damage = 0;

            everyStep = true;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.TryTrip();
    }
}
