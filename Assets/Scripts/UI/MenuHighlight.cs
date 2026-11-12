using FallingWizard.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FallingWizard.UI
{
    public class MenuHighlight : MonoBehaviour
    {
        const string PrefabName = "Menu Highlight";

        const int BottomLeft = 0;
        const int TopRight = 2;
        const int CornerCount = 4;

        [Tooltip("The frame drawn around the selected button. It is moved and stretched to fit " +
                 "each button, so use a Sliced image to keep its corners crisp.")]
        public RectTransform frame;

        [Tooltip("How far the frame sits outside the button, on every side.")]
        [Min(0f)] public float padding = 10f;

        [Tooltip("How quickly the frame slides to the next button. 0 jumps straight there.")]
        [Min(0f)] public float glideSpeed = 25f;

        [Tooltip("Also show the frame while playing with keyboard and mouse. Off, it only shows " +
                 "while a controller is in use.")]
        public bool showWithKeyboard = false;

        static MenuHighlight instance;

        readonly Vector3[] corners = new Vector3[CornerCount];

        RectTransform area;
        GameObject framedButton;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (instance != null)
                return;

            var prefab = Resources.Load<MenuHighlight>(PrefabName);

            if (prefab == null)
            {
                Debug.LogWarning($"There is no '{PrefabName}' prefab in a Resources folder, so " +
                                 "menus show no controller highlight.");
                return;
            }

            instance = Instantiate(prefab);
            DontDestroyOnLoad(instance.gameObject);
        }

        void Awake()
        {
            area = (RectTransform)transform;

            if (frame != null)
                frame.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (frame == null)
                return;

            RectTransform button = FindSelectedButton();
            frame.gameObject.SetActive(button != null);

            if (button == null)
            {
                framedButton = null;
                return;
            }

            bool isNewButton = framedButton == null;
            framedButton = button.gameObject;
            FitAround(button, isNewButton);
        }

        RectTransform FindSelectedButton()
        {
            bool isWanted = showWithKeyboard || Controls.Scheme == Controls.GamepadScheme;
            EventSystem events = EventSystem.current;

            if (!isWanted || events == null)
                return null;

            GameObject selected = events.currentSelectedGameObject;

            if (selected == null || !selected.activeInHierarchy)
                return null;

            return selected.transform as RectTransform;
        }

        void FitAround(RectTransform button, bool isNewButton)
        {
            button.GetWorldCorners(corners);
            Camera buttonCamera = CameraOf(button);

            Vector2 min = ToLocal(corners[BottomLeft], buttonCamera);
            Vector2 max = ToLocal(corners[TopRight], buttonCamera);

            Vector2 targetCentre = (min + max) / 2f;
            Vector2 targetSize = max - min + Vector2.one * padding * 2f;

            float blend = isNewButton || glideSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-glideSpeed * Time.unscaledDeltaTime);

            frame.anchoredPosition = Vector2.Lerp(frame.anchoredPosition, targetCentre, blend);
            frame.sizeDelta = Vector2.Lerp(frame.sizeDelta, targetSize, blend);
        }

        Camera CameraOf(RectTransform button)
        {
            Canvas canvas = button.GetComponentInParent<Canvas>();

            if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.rootCanvas.worldCamera;
        }

        Vector2 ToLocal(Vector3 worldCorner, Camera buttonCamera)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(buttonCamera, worldCorner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPoint, null,
                out Vector2 localPoint);
            return localPoint;
        }
    }
}
