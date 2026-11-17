using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace FallingWizard.UI
{
    [CreateAssetMenu(menuName = "Falling Wizard/Button Icons", fileName = "Button Icons")]
    public class ButtonIcons : ScriptableObject
    {
        public const string ResourcePath = "Button Icons";

        const string CompositePrefix = "composite:";
        const string SchemeSeparator = " ";

        [Tooltip("One row per key or pad button: the control, and the picture shown for it " +
                 "everywhere the game names a button - the HUD, the skill screen, the controls " +
                 "sheet and any Button Prompt you drop into a scene. Swap a Sprite here and every " +
                 "prompt follows.\n\nControl is written the way the Input Actions window writes " +
                 "paths: <Keyboard>/q, <Gamepad>/buttonSouth. A combined binding such as the WASD " +
                 "keys is composite: and its name, like composite:WASD. Put the scheme and a " +
                 "space in front to pin a row to one device, which is what the menu buttons " +
                 "need: Keyboard&Mouse */{Submit} and Gamepad */{Submit}.")]
        public List<Entry> icons = new List<Entry>();

        static ButtonIcons loaded;

        public static Sprite For(InputAction action, string scheme)
        {
            ButtonIcons table = Load();

            if (table == null || action == null)
                return null;

            InputBinding mask = InputBinding.MaskByGroup(scheme);
            ReadOnlyArray<InputBinding> bindings = action.bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                Sprite icon = table.Find(NameControl(bindings, i, mask), scheme);

                if (icon != null)
                    return icon;
            }

            return null;
        }

        static ButtonIcons Load()
        {
            if (loaded == null)
                loaded = Resources.Load<ButtonIcons>(ResourcePath);

            return loaded;
        }

        static string NameControl(ReadOnlyArray<InputBinding> bindings, int index, InputBinding mask)
        {
            InputBinding binding = bindings[index];

            if (binding.isPartOfComposite)
                return null;

            if (binding.isComposite)
                return HasPartFor(bindings, index, mask) ? CompositePrefix + binding.name : null;

            return mask.Matches(binding) ? binding.effectivePath : null;
        }

        static bool HasPartFor(ReadOnlyArray<InputBinding> bindings, int index, InputBinding mask)
        {
            for (int i = index + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
            {
                if (mask.Matches(bindings[i]))
                    return true;
            }

            return false;
        }

        Sprite Find(string control, string scheme)
        {
            if (string.IsNullOrEmpty(control))
                return null;

            Sprite pinned = FindExactly(scheme + SchemeSeparator + control);

            return pinned != null ? pinned : FindExactly(control);
        }

        Sprite FindExactly(string control)
        {
            foreach (Entry entry in icons)
            {
                if (entry != null && entry.icon != null &&
                    string.Equals(entry.control, control, StringComparison.OrdinalIgnoreCase))
                    return entry.icon;
            }

            return null;
        }

        [Serializable]
        public class Entry
        {
            [Tooltip("The key or button, like <Keyboard>/q or <Gamepad>/leftShoulder.")]
            public string control = "";

            [Tooltip("The picture shown for it.")]
            public Sprite icon;
        }
    }
}
