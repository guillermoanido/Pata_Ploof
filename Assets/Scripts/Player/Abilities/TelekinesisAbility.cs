using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Telekinesis", fileName = "Telekinesis")]
    public class TelekinesisAbility : Ability
    {
        [Header("Reach")]
        [Tooltip("How many tiles ahead it will look for somewhere to set it down. It takes the " +
                 "first one that will have it.")]
        [Range(1, 6)] public int placeInTiles = 3;

        [Tooltip("Which ROW, counted from the one your body is in. 0 is the row you are stood " +
                 "in, so it goes down on the same floor you are walking on. 1 is head height, " +
                 "-1 puts it in the floor and is only useful over a drop.")]
        [Range(-2, 2)] public int liftInTiles = 0;

        [Tooltip("Insist on a floor underneath. ON, it refuses any column with no ground in " +
                 "reach. OFF, such a column still takes the thing, hung at your own height - " +
                 "over a drop that is a Bouncy Trash you put exactly where you wanted it. Either way " +
                 "a column that DOES have ground puts it down on that ground, as low as it goes.")]
        public bool needsAFloor = false;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return false;

            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            return hands.carried != null
                ? TryFindShelf(wizard, out _)
                : FindCarryableInReach(wizard) != null;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            if (hands.carried != null)
                return WhyItCannotBePutDown(wizard);

            if (FindCarryableInReach(wizard) == null)
                return Carryable.All.Count == 0
                    ? "there is nothing carryable in this scene - put a Carryable on a Bouncy Trash"
                    : $"nothing within {PickTier(tiers, wizard).reach} boxes of you to take";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            if (hands.carried != null)
                return TryPutDown(wizard, hands);

            return TryPickUp(wizard, hands);
        }

        public override Sprite IconFor(PlayerLogic wizard)
        {
            Carryable held = wizard.spellbook.StateOf<Hands>(this).carried;

            return held != null && held.Icon != null ? held.Icon : icon;
        }

        public override void OnRunReset(PlayerLogic wizard) =>
            wizard.spellbook.StateOf<Hands>(this).carried = null;

        public override void OnUnequipped(PlayerLogic wizard)
        {
            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            if (hands.carried != null)
                hands.carried.GoHome();

            hands.carried = null;
        }

        protected override void Validate() => ValidateTiers(tiers);

        bool TryFindShelf(PlayerLogic wizard, out Vector2Int cell)
        {
            PlayerLogic.Movement movement = wizard.movement;
            Vector2Int standingCell = TileGrid.StandingCell(movement);

            for (int step = 1; step <= placeInTiles; step++)
            {
                if (TileGrid.TryFindRestingCell(standingCell.x + movement.Facing * step,
                        standingCell.y + liftInTiles, movement.groundLayers, needsAFloor,
                        out cell))
                    return true;
            }

            cell = default;
            return false;
        }

        Carryable FindCarryableInReach(PlayerLogic wizard) =>
            Carryable.Nearest(wizard.movement.Position, PickTier(tiers, wizard).reach);

        string WhyItCannotBePutDown(PlayerLogic wizard)
        {
            if (TryFindShelf(wizard, out _))
                return null;

            if (needsAFloor)
                return $"nothing within {placeInTiles} tiles ahead of you has a floor to " +
                       "rest it on. Turn off needs-a-floor to set it down over a drop";

            return $"every column within {placeInTiles} ahead of you is walled in higher " +
                   "than this can lift";
        }

        bool TryPutDown(PlayerLogic wizard, Hands hands)
        {
            if (!TryFindShelf(wizard, out Vector2Int cell))
                return false;

            float height = wizard.movement.Footing.y;

            if (TileGrid.TryFindSurfaceUnder(cell, wizard.movement.groundLayers, out float top))
                height = top;

            hands.carried.PutDownOn(TileGrid.CentreOf(cell).x, height);
            hands.carried = null;
            return true;
        }

        bool TryPickUp(PlayerLogic wizard, Hands hands)
        {
            Carryable found = FindCarryableInReach(wizard);

            if (found == null)
                return false;

            found.Stow();
            hands.carried = found;
            return true;
        }

        [System.Serializable]
        public class Tier : ITier
        {
            [Tooltip("How far the wizard can take hold of something at this rank, in boxes. A " +
                     "mage is one box.")]
            [Min(1f)] public float reach = 6f;

            public void Validate() => reach = Mathf.Max(1f, reach);
        }

        class Hands
        {
            public Carryable carried;
        }
    }
}
