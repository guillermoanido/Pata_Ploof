using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.Core
{
    public static class Controls
    {
        public const string KeyboardScheme = "Keyboard&Mouse";
        public const string GamepadScheme = "Gamepad";

        const string PausePath = "UI/Pause";
        const string SkipPath = "UI/Skip";
        const string CancelPath = "UI/Cancel";

        const string LoadoutPath = "UI/Loadout";
        const string NavigatePath = "UI/Navigate";
        const string SubmitPath = "UI/Submit";

        static readonly HashSet<InputAction> WatchedActions = new HashSet<InputAction>();

        static InputAction pause;
        static InputAction skip;
        static InputAction cancel;
        static InputAction loadout;
        static InputAction navigate;
        static InputAction submit;

        public static event Action SchemeChanged;

        public static string Scheme { get; private set; } = KeyboardScheme;

        public static bool WasPausePressed => pause != null && pause.WasPressedThisFrame();

        public static bool WasSkipPressed => skip != null && skip.WasPressedThisFrame();

        public static bool WasCancelPressed => cancel != null && cancel.WasPressedThisFrame();

        public static bool WasLoadoutPressed => loadout != null && loadout.WasPressedThisFrame();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            WatchedActions.Clear();
            Scheme = Gamepad.current != null && Keyboard.current == null ? GamepadScheme : KeyboardScheme;

            pause = FindAndWatch(PausePath);
            skip = FindAndWatch(SkipPath);
            cancel = FindAndWatch(CancelPath);
            loadout = FindAndWatch(LoadoutPath);
            navigate = FindAndWatch(NavigatePath);
            submit = FindAndWatch(SubmitPath);

            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
            InputSystem.onAfterUpdate -= KeepAMenuButtonSelected;
            InputSystem.onAfterUpdate += KeepAMenuButtonSelected;
        }

        public static InputAction Player(string action) => FindAndWatch($"Player/{action}");

        public static string Glyph(InputAction action) => GlyphFor(action, Scheme);

        public static string GlyphFor(InputAction action, string scheme)
        {
            if (action == null)
                return string.Empty;

            return action.GetBindingDisplayString(
                InputBinding.MaskByGroup(scheme),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        public static InputAction Lookup(string path)
        {
            InputActionAsset actions = InputSystem.actions;
            return actions != null ? actions.FindAction(path) : null;
        }

        static InputAction FindAndWatch(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            InputAction action = Lookup(path);

            if (action == null)
            {
                Debug.LogError($"Input action '{path}' is missing from the project-wide actions asset.");
                return null;
            }

            action.Enable();
            WatchedActions.Add(action);
            return action;
        }

        static void OnActionChange(object subject, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed)
                return;

            var action = subject as InputAction;

            if (action == null || !WatchedActions.Contains(action))
                return;

            InputDevice device = action.activeControl != null ? action.activeControl.device : null;
            string nextScheme = PickSchemeFor(device);

            if (nextScheme == Scheme)
                return;

            Scheme = nextScheme;
            SchemeChanged?.Invoke();
        }

        static string PickSchemeFor(InputDevice device)
        {
            if (device is Gamepad)
                return GamepadScheme;

            if (device is Keyboard || device is Mouse)
                return KeyboardScheme;

            return Scheme;
        }

        static void KeepAMenuButtonSelected()
        {
            EventSystem events = EventSystem.current;

            if (!Application.isPlaying || events == null || IsUsable(events.currentSelectedGameObject))
                return;

            bool wantsAMenu = (navigate != null && navigate.WasPressedThisFrame()) ||
                              (submit != null && submit.WasPressedThisFrame());

            if (!wantsAMenu)
                return;

            Selectable top = FindTopButtonOfTheFrontMenu();

            if (top != null)
                events.SetSelectedGameObject(top.gameObject);
        }

        static bool IsUsable(GameObject selected)
        {
            if (selected == null || !selected.activeInHierarchy)
                return false;

            var button = selected.GetComponent<Selectable>();
            return button == null || button.IsInteractable();
        }

        static Selectable FindTopButtonOfTheFrontMenu()
        {
            Selectable best = null;

            foreach (Selectable candidate in Selectable.allSelectablesArray)
            {
                if (candidate.IsInteractable() && candidate.navigation.mode != Navigation.Mode.None &&
                    (best == null || IsBefore(candidate, best)))
                    best = candidate;
            }

            return best;
        }

        static bool IsBefore(Selectable candidate, Selectable best)
        {
            int candidateLayer = MenuLayerOf(candidate);
            int bestLayer = MenuLayerOf(best);

            if (candidateLayer != bestLayer)
                return candidateLayer > bestLayer;

            Vector3 a = candidate.transform.position;
            Vector3 b = best.transform.position;

            return !Mathf.Approximately(a.y, b.y) ? a.y > b.y : a.x < b.x;
        }

        static int MenuLayerOf(Selectable button)
        {
            Canvas canvas = button.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.rootCanvas.sortingOrder : 0;
        }

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool isLost = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;

            if (device is Gamepad && isLost && Scheme == GamepadScheme)
                Scheme = KeyboardScheme;

            SchemeChanged?.Invoke();
        }
    }
}
