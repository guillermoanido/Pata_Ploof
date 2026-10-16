using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

        static readonly HashSet<InputAction> WatchedActions = new HashSet<InputAction>();

        static InputAction pause;
        static InputAction skip;
        static InputAction cancel;
        static InputAction loadout;

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

            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
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

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool isLost = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;

            if (device is Gamepad && isLost && Scheme == GamepadScheme)
                Scheme = KeyboardScheme;

            SchemeChanged?.Invoke();
        }
    }
}
