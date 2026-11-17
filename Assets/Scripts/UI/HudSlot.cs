using System;
using FallingWizard.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class HudSlot : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The spell's own icon goes here. Whatever sprite you give it on the template is " +
                 "what an empty slot shows.")]
        public Image icon;

        [Tooltip("Drawn over the icon as the spell runs and cools down. Must be an Image set to " +
                 "Filled, or it will sit there at full and never move.")]
        public Image charge;

        [Tooltip("The button to press, shown as a picture from Resources/Button Icons. Hidden for " +
                 "passive spells, which have no button.")]
        public ButtonPrompt prompt;

        [Tooltip("One blip per cast left this level, for a spell that only has so many. Nothing " +
                 "shows for spells without a limit.")]
        public BlipRow charges = new BlipRow();

        [NonSerialized] InputAction shownAction;
        [NonSerialized] Sprite emptyIcon;

        void Awake()
        {
            if (icon != null)
                emptyIcon = icon.sprite;
        }

        public void Show(PlayerLogic.Spellbook.Slot slot, PlayerHud hud, PlayerLogic wizard)
        {
            bool filled = slot.Ability != null;

            ShowButton(slot, hud.showButtons);

            gameObject.SetActive(filled || hud.showEmptySlots);

            if (!gameObject.activeSelf)
                return;

            ShowIcon(slot, hud, wizard, filled);
            ShowCharges(slot, filled);
            ShowCharge(slot, hud, wizard, filled);
        }

        void ShowButton(PlayerLogic.Spellbook.Slot slot, bool show)
        {
            if (prompt == null)
                return;

            bool passive = slot.Ability != null && slot.Ability.IsPassive;
            bool wanted = show && slot.Action != null && !passive;

            prompt.gameObject.SetActive(wanted);

            if (!wanted || slot.Action == shownAction)
                return;

            shownAction = slot.Action;
            prompt.Show(slot.Action);
        }

        void ShowIcon(PlayerLogic.Spellbook.Slot slot, PlayerHud hud, PlayerLogic wizard,
            bool filled)
        {
            if (icon == null)
                return;

            icon.sprite = filled ? slot.Ability.IconFor(wizard) : emptyIcon;

            Color tint = PickIconTint(slot, hud, filled);

            icon.color = filled ? tint * slot.Ability.IconTintFor(wizard) : tint;
        }

        Color PickIconTint(PlayerLogic.Spellbook.Slot slot, PlayerHud hud, bool filled)
        {
            if (!filled)
                return hud.emptyTint;

            if (slot.IsReady)
                return hud.readyTint;

            return hud.notReadyTint;
        }

        void ShowCharges(PlayerLogic.Spellbook.Slot slot, bool filled)
        {
            bool counted = filled && slot.Ability.usesPerLevel > 0;

            charges.Show(counted ? slot.MaxUses : 0, slot.UsesLeft);
        }

        void ShowCharge(PlayerLogic.Spellbook.Slot slot, PlayerHud hud, PlayerLogic wizard,
            bool filled)
        {
            if (charge == null)
                return;

            float windup = filled ? slot.Ability.ChargeFor(wizard) : Ability.NotCharging;
            bool winding = windup >= 0f;

            float fill = winding ? windup : MeasureRunOrCooldown(slot);

            charge.color = winding ? hud.windupTint : hud.chargeTint;
            charge.enabled = filled && fill > 0f;
            charge.fillAmount = fill;
        }

        float MeasureRunOrCooldown(PlayerLogic.Spellbook.Slot slot)
        {
            if (slot.IsLit)
                return slot.LitProgress;

            if (slot.CooldownLeft > 0f)
                return slot.CooldownProgress;

            return 0f;
        }
    }
}
