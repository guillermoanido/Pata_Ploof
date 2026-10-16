using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParallaxLayer : MonoBehaviour
    {
        const float Half = 0.5f;

        const int BothSides = 2;

        [Header("Distance")]
        [Tooltip("How much this sticks to the camera as it pans sideways. 0 is world-locked and " +
                 "slides fully past you; 1 is welded to the camera and never appears to move.")]
        [Range(0f, 1f)] public float parallax = 0.5f;

        [Tooltip("The same, for the camera moving up and down. Levels are usually far taller " +
                 "than a background sheet, so this wants to be high or the sky runs out.")]
        [Range(0f, 1f)] public float verticalParallax = 0.9f;

        [Header("Repeat")]
        [Tooltip("Shuffle the sheet along by whole widths so it never runs out sideways, however " +
                 "far you walk. The art has to tile seamlessly left to right for this to be " +
                 "invisible.")]
        public bool repeatForever = true;

        [Tooltip("Copies laid side by side to fill the gap the shuffling leaves. Three covers a " +
                 "sheet wider than the screen; go up if yours is narrow.")]
        [Range(1, 9)] public int copies = 3;

        Camera mainCamera;
        SpriteRenderer art;

        Vector3 origin;
        Vector3 cameraOrigin;
        float width;

        bool IsRepeating => repeatForever && width > 0f;

        void Awake()
        {
            art = GetComponent<SpriteRenderer>();
            origin = transform.position;

            width = art.sprite != null ? art.bounds.size.x : 0f;

            if (IsRepeating)
                BuildCopies();
        }

        void LateUpdate()
        {
            if (!TryFindCamera())
                return;

            Vector3 cameraPosition = mainCamera.transform.position;
            Vector3 travel = cameraPosition - cameraOrigin;

            float newX = origin.x + travel.x * parallax;
            float newY = origin.y + travel.y * verticalParallax;

            if (IsRepeating)
            {
                float halfWidth = width * Half;
                float drift = Mathf.Repeat(cameraPosition.x - newX + halfWidth, width) - halfWidth;
                newX = cameraPosition.x - drift;
            }

            transform.position = new Vector3(newX, newY, origin.z);
        }

        void BuildCopies()
        {
            int copiesPerSide = Mathf.Max(0, (copies - 1) / BothSides);

            for (int step = -copiesPerSide; step <= copiesPerSide; step++)
            {
                if (step == 0)
                    continue;

                BuildCopy(step);
            }
        }

        void BuildCopy(int step)
        {
            var copyObject = new GameObject($"{name} {step:+#;-#}");
            copyObject.transform.SetParent(transform, false);
            copyObject.transform.localPosition = new Vector3(width * step, 0f, 0f);

            var copyArt = copyObject.AddComponent<SpriteRenderer>();
            copyArt.sprite = art.sprite;
            copyArt.color = art.color;
            copyArt.flipX = art.flipX;
            copyArt.flipY = art.flipY;
            copyArt.material = art.sharedMaterial;
            copyArt.sortingLayerID = art.sortingLayerID;
            copyArt.sortingOrder = art.sortingOrder;
        }

        bool TryFindCamera()
        {
            if (mainCamera != null)
                return true;

            mainCamera = Camera.main;

            if (mainCamera == null)
                return false;

            cameraOrigin = mainCamera.transform.position;
            return true;
        }
    }
}
