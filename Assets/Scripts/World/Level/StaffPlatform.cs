using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class StaffPlatform : MonoBehaviour
    {
        [Header("Opening")]
        [Tooltip("Longest the planks stay open once the wizard pushes through with the staff, in " +
                 "seconds. They normally shut the moment the wizard is clear underneath; this is " +
                 "only a safety net, and they still wait until nobody is inside them.")]
        [Min(0f)] public float mostSecondsOpen = 1.5f;

        [Header("Looks")]
        [Tooltip("Shown while the planks are shut. A child object. Leave it empty for none.")]
        public GameObject closedLook;

        [Tooltip("Shown while the wizard is passing through. A child object. Leave it empty and " +
                 "the closed look simply stays.")]
        public GameObject openLook;

        Collider2D floor;
        Collider2D passenger;
        Bounds shutBounds;
        float giveUpAt;

        public bool IsOpen => passenger != null;

        void Awake()
        {
            floor = GetComponent<Collider2D>();
            ShowOpen(false);
        }

        void FixedUpdate()
        {
            if (IsOpen && CanShut())
                Shut();
        }

        public void LetThrough(Collider2D wizardHull)
        {
            if (IsOpen || wizardHull == null)
                return;

            shutBounds = floor.bounds;
            passenger = wizardHull;
            giveUpAt = Time.time + mostSecondsOpen;

            floor.enabled = false;
            ShowOpen(true);
        }

        bool CanShut()
        {
            Bounds wizard = passenger.bounds;

            if (wizard.max.y < shutBounds.min.y)
                return true;

            return Time.time >= giveUpAt && !wizard.Intersects(shutBounds);
        }

        void Shut()
        {
            passenger = null;
            floor.enabled = true;
            ShowOpen(false);
        }

        void ShowOpen(bool isOpen)
        {
            if (openLook == null)
                return;

            openLook.SetActive(isOpen);

            if (closedLook != null)
                closedLook.SetActive(!isOpen);
        }
    }
}
