using FallingWizard.Player;

namespace FallingWizard.World
{
    public class BrokenGlass : Hazard
    {
        const int CutDamage = 1;

        const float CutCooldown = 1f;

        void Reset()
        {
            damage = CutDamage;
            rearmDelay = CutCooldown;
            everyStep = true;
            ignoresWalking = true;
            affectsRagdolled = true;
        }

        protected override void Affect(PlayerLogic wizard) { }
    }
}
