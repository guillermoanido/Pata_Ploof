using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class HeartPickup : Pickup
    {
        [Header("Heart")]
        [Tooltip("Hearts added to the wizard's health, for good, so the hat takes longer to " +
                 "wear down. They are healed on the spot, they survive dying and turning back, " +
                 "and this heart is never here again - which is why the next one has to be " +
                 "found somewhere deeper.")]
        [Min(1)] public int hearts = 1;

        [Tooltip("Wisps given instead when the wizard already has as many hearts as they can " +
                 "ever get. Never leave a player standing over dead loot.")]
        [Min(0)] public int wispsIfFull = 3;

        protected override string Prefix => "heart";

        protected override StaysTaken StaysTaken => StaysTaken.ForGood;

        protected override bool Take(PlayerCharacter wizard)
        {
            if (wizard.Logic.GrowHeart(hearts))
                return true;

            Progress.CarryWisps(wispsIfFull);
            return true;
        }
    }
}
