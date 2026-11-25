using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Furniture : MonoBehaviour
    {
        [Tooltip("Upward speed a Star Slap adds, in boxes per second, so the piece hops as it " +
                 "goes instead of only sliding. How far it goes is the Rigidbody 2D's Mass: a " +
                 "heavier piece is knocked a shorter way.")]
        [Min(0f)] public float hop = 2f;

        [Tooltip("How fast a slap sets it spinning, in degrees per second, tipping the way it " +
                 "was slapped. 0 knocks it along upright. Tick Freeze Rotation on the Rigidbody " +
                 "2D to keep it upright whatever happens.")]
        [Min(0f)] public float spin = 180f;

        Rigidbody2D body;

        void Awake() => body = GetComponent<Rigidbody2D>();

        public void TakeSlap(Vector2 impulse)
        {
            body.AddForce(impulse, ForceMode2D.Impulse);
            body.linearVelocityY += hop;
            body.angularVelocity = -Mathf.Sign(impulse.x) * spin;
        }
    }
}
