using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Wall Growth", fileName = "Wall Growth")]
    public class WallGrowthAbility : Ability
    {
        const float SmallestScale = 0.01f;

        const float Half = 0.5f;

        [Header("Wall")]
        [Tooltip("Spawned as the block, and required - with this empty the spell will not cast. " +
                 "Its FIRST CHILD carries the sprite and the collider, ONE BOX SQUARE and centred " +
                 "on itself - the spell scales the root, and works out where to stand the root " +
                 "from that one box. Put it on a layer the wizard's ground check looks at, such " +
                 "as Ground, or they will walk straight through it.")]
        public GameObject wallPrefab;

        [Tooltip("Size of the finished block in boxes. One by one is a single tile, which is " +
                 "what the grid is built out of and what snapping expects.")]
        public Vector2 size = Vector2.one;

        [Header("Where It Grows")]
        [Tooltip("How many tiles ahead to hunt for the lip of a drop. The block is placed " +
                 "relative to THAT, not to where you happen to be stood, so it lands in the same " +
                 "spot whether you walked right up to the edge or stopped a tile short.")]
        [Range(1, 6)] public int reachInTiles = 3;

        [Tooltip("Tiles past the lip. 0 is the first empty column - the block hugs the ledge. " +
                 "1 leaves a one-tile gap you have to jump.")]
        [Range(0, 3)] public int outFromEdge = 0;

        [Tooltip("Which ROW, counted from the one your body is in. -1 is the floor row, so the " +
                 "block comes out level with the ground you are stood on and you can walk " +
                 "straight onto it. -2 is one below that, a step down into the drop. 0 is knee " +
                 "height, which is almost never what you want.")]
        [Range(-4, 1)] public int liftInTiles = -1;

        [Header("Growing")]
        [Tooltip("Seconds it takes to reach full size. Keep it SHORT: the collider follows " +
                 "the scale, so this is also how long the cell reads as half-empty to the next " +
                 "cast and half-solid to a wizard stood on it.")]
        [Min(0f)] public float growTime = 0.05f;

        public override bool CanCast(PlayerLogic wizard) =>
            wallPrefab != null &&
            wizard.State == PlayerState.Normal &&
            wizard.movement.IsGrounded &&
            wizard.spellbook.StateOf<Growth>(this).wall == null &&
            TryFindCell(wizard, out _, out _);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wallPrefab == null)
                return "this spell has no Wall Prefab - drag the Stone Wall prefab into its " +
                       "Wall Prefab slot";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and need both feet under you";

            if (!wizard.movement.IsGrounded)
                return "you are in the air - this grows out of the ground you are stood on";

            if (wizard.spellbook.StateOf<Growth>(this).wall != null)
                return "your last block is still standing";

            if (TryFindCell(wizard, out _, out _))
                return null;

            return $"no lip within {reachInTiles} tiles ahead of you, or the tile past it is " +
                   "already filled - this wants an edge to build off";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!TryFindCell(wizard, out Vector2Int cell, out float top))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            growth.wall = SpawnWall(new Vector2(TileGrid.CentreOf(cell).x, top - size.y * Half));
            growth.age = 0f;

            Transform body = FindBody(growth.wall);

            if (body != null)
                body.localPosition = Vector3.zero;

            ScaleWall(growth, growTime <= 0f ? 1f : 0f);
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            if (growth.wall == null)
                return;

            growth.age += fixedDeltaTime;

            ScaleWall(growth, growTime <= 0f ? 1f : Mathf.Clamp01(growth.age / growTime));
        }

        public override void OnEnded(PlayerLogic wizard) => RemoveWall(wizard);

        protected override void CleanUp(PlayerLogic wizard) => RemoveWall(wizard);

        void RemoveWall(PlayerLogic wizard)
        {
            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            if (growth.wall != null)
                Destroy(growth.wall);

            growth.wall = null;
            growth.age = 0f;
        }

        bool TryFindCell(PlayerLogic wizard, out Vector2Int cell, out float top)
        {
            PlayerLogic.Movement movement = wizard.movement;
            Vector2Int standingCell = TileGrid.StandingCell(movement);

            cell = default;
            top = 0f;

            if (!TileGrid.TryFindFloorRowUnder(standingCell, movement.groundLayers, out int floorRow))
                return false;

            for (int step = 0; step <= reachInTiles; step++)
            {
                int column = standingCell.x + movement.Facing * step;

                if (TileGrid.IsSolid(new Vector2Int(column, floorRow), movement.groundLayers))
                    continue;

                cell = new Vector2Int(column + movement.Facing * outFromEdge,
                    floorRow + 1 + liftInTiles);

                top = movement.Footing.y + (cell.y - floorRow);

                return TileGrid.IsFree(cell, movement.groundLayers);
            }

            return false;
        }

        GameObject SpawnWall(Vector2 anchor)
        {
            GameObject wall = Instantiate(wallPrefab, anchor, Quaternion.identity);
            wall.name = displayName;
            return wall;
        }

        static Transform FindBody(GameObject wall) =>
            wall != null && wall.transform.childCount > 0 ? wall.transform.GetChild(0) : null;

        void ScaleWall(Growth growth, float progress)
        {
            if (growth.wall == null)
                return;

            growth.wall.transform.localScale = new Vector3(
                Mathf.Max(SmallestScale, size.x * progress),
                Mathf.Max(SmallestScale, size.y * progress), 1f);
        }

        class Growth
        {
            public GameObject wall;
            public float age;
        }
    }
}
