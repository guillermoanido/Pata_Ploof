using UnityEngine;

namespace FallingWizard.Core
{
    public static class Layers
    {
        public const int Default = 0;
        public const int Ground = 6;
        public const int Player = 7;
        public const int Hazard = 8;

        public const int GroundMask = 1 << Ground;
        public const int HazardMask = 1 << Hazard;

        public static ContactFilter2D MakeSolidOnlyFilter(LayerMask mask) => new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = mask,
            useTriggers = false,
        };
    }
}
