using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class MagicDoor : MonoBehaviour
    {
        [Header("Looks")]
        [Tooltip("Shown while the door is shut. A child object with your door art. Leave it " +
                 "empty for none.")]
        public GameObject closedLook;

        [Tooltip("Shown once Telekinesis has opened the door. A child object again; a play-once " +
                 "clip of it swinging open fits here. Leave it empty and the closed look simply " +
                 "switches off.")]
        public GameObject openLook;

        Collider2D shut;

        public bool IsOpen { get; private set; }

        void Awake()
        {
            shut = GetComponent<Collider2D>();
            ShowOpen(false);
        }

        public bool TryOpen()
        {
            if (IsOpen)
                return false;

            IsOpen = true;
            shut.enabled = false;
            ShowOpen(true);
            return true;
        }

        void ShowOpen(bool isOpen)
        {
            if (closedLook != null)
                closedLook.SetActive(!isOpen);

            if (openLook != null)
                openLook.SetActive(isOpen);
        }
    }
}
