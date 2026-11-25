using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Camera))]
    public class IntroZoom : MonoBehaviour
    {
        [Tooltip("How close the camera starts when the level begins: 5 is the usual close-up, " +
                 "the size every other level uses. It then eases out to the Size set on this " +
                 "Camera, so that Size is still where the level ends up.")]
        [Min(0.1f)] public float startSize = 5f;

        [Tooltip("Seconds the camera holds the start size before it begins to pull back.")]
        [Min(0f)] public float holdSeconds = 0.3f;

        [Tooltip("Seconds the pull back takes. It eases in and out, so it never jolts.")]
        [Min(0f)] public float zoomSeconds = 1.5f;

        Camera view;
        float endSize;
        float elapsed;

        void Awake()
        {
            view = GetComponent<Camera>();
            endSize = view.orthographicSize;
            view.orthographicSize = startSize;
        }

        void Update()
        {
            elapsed += Time.deltaTime;

            float progress = zoomSeconds <= 0f ? 1f : Mathf.Clamp01((elapsed - holdSeconds) / zoomSeconds);
            view.orthographicSize = Mathf.Lerp(startSize, endSize, Mathf.SmoothStep(0f, 1f, progress));

            if (progress >= 1f)
                enabled = false;
        }
    }
}
