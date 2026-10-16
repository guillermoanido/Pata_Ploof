using UnityEngine;

namespace FallingWizard.UI
{
    public class FlingArrow : MonoBehaviour
    {
        [Tooltip("Switched on while the throw would carry you through a hazard, and off " +
                 "otherwise - for example a red copy of the arrow placed as a child of this one. " +
                 "Leave empty for no warning.")]
        public GameObject dangerLook;

        public void Show(Vector2 from, Vector2 direction, bool hazard)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                Hide();
                return;
            }

            float turn = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            transform.SetPositionAndRotation(from, Quaternion.Euler(0f, 0f, turn));

            if (dangerLook != null)
                dangerLook.SetActive(hazard);

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
