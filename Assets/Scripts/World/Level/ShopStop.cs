using FallingWizard.Core;
using FallingWizard.Player;
using FallingWizard.UI;

namespace FallingWizard.World
{
    public class ShopStop : Item
    {
        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.IsModalOpen || !wizard.Logic.health.IsAlive)
                return;

            SkillScreen.OpenInLevel(() => CarryOn(wizard));
        }

        static void CarryOn(PlayerCharacter wizard)
        {
            if (wizard != null)
                wizard.Logic.spellbook.Reload();

            Game.SetPaused(false);
        }
    }
}
