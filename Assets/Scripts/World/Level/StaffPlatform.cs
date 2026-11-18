using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class StaffPlatform : MonoBehaviour
    {
        Collider2D planks;

        public float Left => planks.bounds.min.x;

        public float Right => planks.bounds.max.x;

        public float Bottom => planks.bounds.min.y;

        void Awake() => planks = GetComponent<Collider2D>();
    }
}
