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

        [Header("Reach")]
        [Tooltip("How far the push of magic reaches, in boxes, all round the wizard. A mage is " +
                 "one box. It is the same at every rank: upgrades make things stay stopped " +
                 "longer, never reach further.")]
        [Min(ShortestReach)] public float reach = 4f;

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
                return $"nothing within {reach} boxes of you to open or stop: no shut Magic Door, " +
                       "and no moving platform, trap or hazard with Can Sleep ticked that is awake";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            float seconds = PickTier(tiers, wizard).sleepSeconds;
            int moved = 0;

            foreach (Collider2D thing in FindEverythingInReach(wizard))
            {
                if (TryOpenDoor(thing) || TryPutToSleep(thing, seconds))
                    moved++;
            }

            return moved > 0;
        }

        protected override void Validate()
        {
            reach = Mathf.Max(ShortestReach, reach);
            ValidateTiers(tiers);
        }

        int CountThingsToMove(PlayerLogic wizard)
        {
            int count = 0;

            foreach (Collider2D thing in FindEverythingInReach(wizard))
                if (IsShutDoor(thing) || IsAwake(thing))
                    count++;

            return count;
        }

        List<Collider2D> FindEverythingInReach(PlayerLogic wizard)
        {
            Physics2D.OverlapCircle(wizard.movement.Position, reach, ContactFilter2D.noFilter,
                InReach);

            return InReach;
        }

        static bool IsShutDoor(Collider2D thing)
        {
            var door = thing.GetComponent<MagicDoor>();
            return door != null && !door.IsOpen;
        }

        static bool IsAwake(Collider2D thing)
        {
            var sleeper = thing.GetComponentInParent<ISleepable>();
            return sleeper != null && sleeper.CanSleepNow;
        }

        static bool TryOpenDoor(Collider2D thing)
        {
            var door = thing.GetComponent<MagicDoor>();
            return door != null && door.TryOpen();
        }

        static bool TryPutToSleep(Collider2D thing, float seconds)
        {
            var sleeper = thing.GetComponentInParent<ISleepable>();
            return sleeper != null && sleeper.CanSleepNow && sleeper.TrySleep(seconds);
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("Seconds a platform, trap or hazard caught in it stays stopped at this rank. " +
                     "Doors stay open for good.")]
            [Min(ShortestSleep)] public float sleepSeconds = 3f;

            public void Validate() => sleepSeconds = Mathf.Max(ShortestSleep, sleepSeconds);
        }
    }
}
