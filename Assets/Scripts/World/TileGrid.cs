using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public static class TileGrid
    {
        const float Inset = 0.45f;

        const float OffTheLine = 0.1f;

        const int LookDown = 6;

        const int StepOver = 2;

        const float HalfCell = 0.5f;

        const float BothSides = 2f;

        const float RayStartHeight = 0.95f;

        const int MaxOverlaps = 4;
        const int MaxHitsBelow = 2;

        static readonly Collider2D[] Overlaps = new Collider2D[MaxOverlaps];
        static readonly RaycastHit2D[] HitsBelow = new RaycastHit2D[MaxHitsBelow];

        public static Vector2 CentreOf(Vector2Int cell) =>
            new Vector2(cell.x + HalfCell, cell.y + HalfCell);

        public static Vector2Int StandingCell(PlayerLogic.Movement movement)
        {
            Vector2 footing = movement.Footing;

            return FindCellOf(new Vector2(footing.x, footing.y + OffTheLine));
        }

        public static bool IsSolid(Vector2Int cell, LayerMask groundLayers) =>
            Physics2D.OverlapBox(CentreOf(cell), Vector2.one * (Inset * BothSides), 0f,
                Layers.MakeSolidOnlyFilter(groundLayers), Overlaps) > 0;

        public static bool TryFindRestingCell(int column, int fromRow, LayerMask groundLayers,
            bool mustLand, out Vector2Int cell)
        {
            int row = fromRow;

            for (int climb = 0; climb < StepOver &&
                 IsSolid(new Vector2Int(column, row), groundLayers); climb++)
                row++;

            cell = new Vector2Int(column, row);

            if (IsSolid(cell, groundLayers))
                return false;

            if (TryFindFloorRowUnder(new Vector2Int(column, row - 1), groundLayers, out int floorRow))
            {
                cell = new Vector2Int(column, floorRow + 1);
                return true;
            }

            cell = new Vector2Int(column, fromRow);
            return !mustLand && IsFree(cell, groundLayers);
        }

        public static bool TryFindSurfaceUnder(Vector2Int cell, LayerMask groundLayers, out float top)
        {
            var rayStart = new Vector2(cell.x + HalfCell, cell.y + RayStartHeight);

            if (Physics2D.Raycast(rayStart, Vector2.down,
                    Layers.MakeSolidOnlyFilter(groundLayers), HitsBelow, LookDown) > 0)
            {
                top = HitsBelow[0].point.y;
                return true;
            }

            top = cell.y;
            return false;
        }

        static Vector2Int FindCellOf(Vector2 worldPoint) =>
            new Vector2Int(Mathf.FloorToInt(worldPoint.x), Mathf.FloorToInt(worldPoint.y));

        static bool TryFindFloorRowUnder(Vector2Int from, LayerMask groundLayers, out int row)
        {
            for (int drop = 0; drop < LookDown; drop++)
            {
                row = from.y - drop;

                if (IsSolid(new Vector2Int(from.x, row), groundLayers))
                    return true;
            }

            row = from.y - 1;
            return false;
        }

        static bool IsFree(Vector2Int cell, LayerMask groundLayers) =>
            !IsSolid(cell, groundLayers);
    }
}
