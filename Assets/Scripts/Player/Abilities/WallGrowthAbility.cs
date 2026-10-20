using System;
using System.Collections.Generic;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Wall Growth", fileName = "Wall Growth")]
    public class WallGrowthAbility : Ability
    {
        const float SmallestScale = 0.01f;

        const float Half = 0.5f;

        const float FallingSpeed = 0.5f;

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
        [Tooltip("How many tiles ahead, along the floor you are stood on, to hunt for its edge. " +
                 "The block always comes out level with that floor, in the first empty tile past " +
                 "the edge, so it only ever makes the floor longer. The hunt stops at a wall, so " +
                 "it never grows on the far side of one.")]
        [Range(1, 6)] public int reachInTiles = 3;

        [Header("Growing")]
        [Tooltip("Seconds it takes to reach full size. Keep it SHORT: the collider follows " +
                 "the scale, so this is also how long the cell reads as half-empty to the next " +
                 "cast and half-solid to a wizard stood on it.")]
        [Min(0f)] public float growTime = 0.05f;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            wallPrefab != null &&
            wizard.State == PlayerState.Normal &&
            IsStandingFirmly(wizard.movement) &&
            HasBlocksLeft(wizard) &&
            TryFindCell(wizard.movement, out _);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wallPrefab == null)
                return "this spell has no Wall Prefab - drag the Stone Wall prefab into its " +
                       "Wall Prefab slot";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and need both feet under you";

            if (!IsStandingFirmly(wizard.movement))
                return "you are in the air or falling - this grows out of the floor you are stood on";

            if (!HasBlocksLeft(wizard))
                return $"you have grown all {PickTier(tiers, wizard).blocks} block(s) this rank " +
                       "allows - wait for them to crumble and the spell to cool down";

            if (TryFindCell(wizard.movement, out _))
                return null;

            return $"the floor you are stood on has no edge within {reachInTiles} tiles ahead " +
                   "of you, or a wall is in the way - this only ever makes that floor longer";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!TryFindCell(wizard.movement, out Vector2Int cell))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            var block = new Block { wall = SpawnWall(FindAnchor(cell, wizard.movement.Footing.y)) };

            Transform body = FindBody(block.wall);

            if (body != null)
                body.localPosition = Vector3.zero;

            ScaleWall(block);

            growth.blocks.Add(block);
            growth.grown++;
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            List<Block> blocks = wizard.spellbook.StateOf<Growth>(this).blocks;

            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                Block block = blocks[i];
                block.age += fixedDeltaTime;

                if (HasCrumbled(block))
                    RemoveBlockAt(blocks, i);
                else
                    ScaleWall(block);
            }
        }

        public override void OnEnded(PlayerLogic wizard) => RemoveAllBlocks(wizard);

        protected override void Validate() => ValidateTiers(tiers);

        protected override void CleanUp(PlayerLogic wizard) => RemoveAllBlocks(wizard);

        static bool IsStandingFirmly(PlayerLogic.Movement movement) =>
            movement.IsGrounded && movement.Velocity.y > -FallingSpeed;

        bool HasBlocksLeft(PlayerLogic wizard) =>
            wizard.spellbook.StateOf<Growth>(this).grown < PickTier(tiers, wizard).blocks;

        bool TryFindCell(PlayerLogic.Movement movement, out Vector2Int cell)
        {
            Vector2Int standing = TileGrid.StandingCell(movement);
            var ahead = new Vector2Int(movement.Facing, 0);
            LayerMask ground = movement.groundLayers;

            cell = default;

            for (int step = 0; step <= reachInTiles; step++)
            {
                Vector2Int floor = standing + ahead * step + Vector2Int.down;

                if (TileGrid.IsSolid(floor + Vector2Int.up, ground))
                    return false;

                if (TileGrid.IsSolid(floor, ground))
                    continue;

                cell = floor;
                return TileGrid.IsSolid(floor - ahead, ground);
            }

            return false;
        }

        Vector2 FindAnchor(Vector2Int cell, float floorTop) =>
            new Vector2(TileGrid.CentreOf(cell).x, floorTop - size.y * Half);

        GameObject SpawnWall(Vector2 anchor)
        {
            GameObject wall = Instantiate(wallPrefab, anchor, Quaternion.identity);
            wall.name = displayName;
            return wall;
        }

        static Transform FindBody(GameObject wall) =>
            wall != null && wall.transform.childCount > 0 ? wall.transform.GetChild(0) : null;

        void ScaleWall(Block block)
        {
            if (block.wall == null)
                return;

            float progress = growTime <= 0f ? 1f : Mathf.Clamp01(block.age / growTime);

            block.wall.transform.localScale = new Vector3(
                Mathf.Max(SmallestScale, size.x * progress),
                Mathf.Max(SmallestScale, size.y * progress), 1f);
        }

        bool HasCrumbled(Block block) => block.wall == null || block.age >= activeDuration;

        static void RemoveBlockAt(List<Block> blocks, int index)
        {
            if (blocks[index].wall != null)
                Destroy(blocks[index].wall);

            blocks.RemoveAt(index);
        }

        void RemoveAllBlocks(PlayerLogic wizard)
        {
            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            for (int i = growth.blocks.Count - 1; i >= 0; i--)
                RemoveBlockAt(growth.blocks, i);

            growth.grown = 0;
        }

        [Serializable]
        public class Tier : ITier
        {
            [Tooltip("How many blocks the spell grows before it has to rest. Each one crumbles " +
                     "Active Duration seconds after it grew, and the cooldown starts once the " +
                     "last one is gone.")]
            [Min(1)] public int blocks = 1;

            public void Validate() => blocks = Mathf.Max(1, blocks);
        }

        class Growth
        {
            public readonly List<Block> blocks = new List<Block>();
            public int grown;
        }

        class Block
        {
            public GameObject wall;
            public float age;
        }
    }
}
