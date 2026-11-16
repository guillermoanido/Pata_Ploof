using System.Collections.Generic;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Telekinesis", fileName = "Telekinesis")]
    public class TelekinesisAbility : Ability
    {
        const float ShortestReach = 1f;
        const float ShortestSleep = 0.5f;

        static readonly List<Collider2D> InReach = new List<Collider2D>();

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal && CountThingsToMove(wizard) > 0;

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            if (CountThingsToMove(wizard) == 0)
                return $"no shut Magic Door or awake hazard within {PickTier(tiers, wizard).reach} " +
                       "boxes of you";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Tier tier = PickTier(tiers, wizard);
            int moved = 0;

            foreach (Collider2D thing in FindEverythingInReach(wizard))
            {
                if (TryOpenDoor(thing) || TryPutToSleep(thing, tier.sleepSeconds))
                    moved++;
            }

            return moved > 0;
        }

        protected override void Validate() => ValidateTiers(tiers);

        int CountThingsToMove(PlayerLogic wizard)
        {
            int count = 0;

            foreach (Collider2D thing in FindEverythingInReach(wizard))
                if (IsShutDoor(thing) || IsAwakeHazard(thing))
                    count++;

            return count;
        }

        List<Collider2D> FindEverythingInReach(PlayerLogic wizard)
        {
            Physics2D.OverlapCircle(wizard.movement.Position, PickTier(tiers, wizard).reach,
                ContactFilter2D.noFilter, InReach);

            return InReach;
        }

        static bool IsShutDoor(Collider2D thing)
        {
            var door = thing.GetComponent<MagicDoor>();
            return door != null && !door.IsOpen;
        }

        static bool IsAwakeHazard(Collider2D thing)
        {
            var hazard = thing.GetComponent<Hazard>();
            return hazard != null && hazard.canSleep && !hazard.IsAsleep;
        }

        static bool TryOpenDoor(Collider2D thing)
        {
            var door = thing.GetComponent<MagicDoor>();
            return door != null && door.TryOpen();
        }

        static bool TryPutToSleep(Collider2D thing, float seconds)
        {
            var hazard = thing.GetComponent<Hazard>();
            return hazard != null && !hazard.IsAsleep && hazard.TrySleep(seconds);
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("How far the push of magic reaches at this rank, in boxes, all round the " +
                     "wizard. A mage is one box.")]
            [Min(ShortestReach)] public float reach = 4f;

            [Tooltip("Seconds a hazard caught in it stays asleep at this rank. Doors stay open " +
                     "for good.")]
            [Min(ShortestSleep)] public float sleepSeconds = 3f;

            public void Validate()
            {
                reach = Mathf.Max(ShortestReach, reach);
                sleepSeconds = Mathf.Max(ShortestSleep, sleepSeconds);
            }
        }
    }
}
