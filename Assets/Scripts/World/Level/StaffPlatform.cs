using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class StaffPlatform : MonoBehaviour
    {
        const int LeftSide = -1;
        const int RightSide = 1;

        Collider2D planks;

        public float Bottom => planks.bounds.min.y;

        public float Middle => planks.bounds.center.y;

        void Awake() => planks = GetComponent<Collider2D>();

        public int NearestSideTo(float x)
        {
            Bounds box = planks.bounds;
            return x - box.min.x <= box.max.x - x ? LeftSide : RightSide;
        }

        public float EdgeOn(int side) => side < 0 ? planks.bounds.min.x : planks.bounds.max.x;
    }
}
