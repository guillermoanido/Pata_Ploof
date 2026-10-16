using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        [Serializable]
        public class Spellbook
        {
            public const int SlotCount = Progress.SlotCount;

            const float ExplainAfter = 0.35f;

            const float NotReleased = -1f;

            public static readonly string[] SlotActions =
                { "Spell1", "Spell2", "Spell3", "Spell4" };

            [Header("Spells")]
            [Tooltip("The catalogue every slot draws from. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

            [Tooltip("Print a line to the console whenever a press comes to nothing, saying " +
                     "which spell refused and why. Editor only. Leave it on while building a " +
                     "level - a spell that silently does nothing is the hardest kind to chase.")]
            public bool explainRefusals = true;

            [NonSerialized] Modifiers stats = new Modifiers();
            [NonSerialized] Slot[] slots = Array.Empty<Slot>();
            [NonSerialized] PlayerLogic owner;

            [NonSerialized] readonly Dictionary<Ability, object> spellStates =
                new Dictionary<Ability, object>();

            public int Version { get; private set; }

            public IReadOnlyList<Slot> Slots => slots;

            public Modifiers Stats => stats;

            public void Attach(PlayerLogic player)
            {
                owner = player;

                if (book == null)
                    book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

                if (book == null)
                {
                    Debug.LogError("No spellbook found. Create Assets/Resources/Spellbook.asset " +
                                   "from Assets > Create > Falling Wizard > Spellbook, and put " +
                                   "the Staff in both of its lists.");
                    slots = Array.Empty<Slot>();
                    return;
                }

                slots = new Slot[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                    slots[i] = new Slot(Controls.Player(SlotActions[i]));

                Seed(book);
                Reload();
            }

            public static void Seed(AbilityBook book)
            {
                if (book == null)
                    return;

                foreach (Ability spell in book.known)
                    if (spell != null)
                        Progress.Grant(spell.Key);

                foreach (Ability spell in book.spells)
                    if (spell != null && spell.locked && spell.fixedSlot >= 0 &&
                        Progress.Owns(spell.Key) && Progress.SlotHolding(spell.Key) < 0)
                        Progress.Equip(spell.fixedSlot, spell.Key);
            }

            public void Reload()
            {
                if (slots.Length == 0)
                    return;

                Seed(book);

                for (int i = 0; i < SlotCount; i++)
                {
                    Ability next = book.Find(Progress.EquippedIn(i));

                    if (next != null && !Progress.Owns(next.Key))
                        next = null;

                    ReloadSlot(slots[i], next);
                }

                Version++;
            }

            public bool TryEquip(Ability spell, int slotIndex)
            {
                if (slotIndex < 0 || slotIndex >= SlotCount)
                    return false;

                if (spell != null && (!Progress.Owns(spell.Key) || spell.locked))
                    return false;

                Ability leaving = book.Find(Progress.EquippedIn(slotIndex));

                if (leaving != null && leaving.locked)
                    return false;

                Progress.Place(slotIndex, spell != null ? spell.Key : string.Empty);
                Reload();
                return true;
            }

            public T StateOf<T>(Ability spell) where T : class, new()
            {
                if (spell == null)
                    return null;

                if (spellStates.TryGetValue(spell, out object stored) && stored is T state)
                    return state;

                var fresh = new T();
                spellStates[spell] = fresh;
                return fresh;
            }

            public void Extinguish(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsLit)
                    return;

                PutOut(slot);
            }

            public int RankOf(Ability spell)
            {
                Slot slot = SlotOf(spell);
                return slot != null ? slot.Rank : 0;
            }

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (paused || slot.Action == null)
                        ForgetInput(slot);
                    else if (slot.Ability == null)
                        RefuseEmptySlot(i, slot);
                    else
                        ReadButton(i, slot, deltaTime);
                }
            }

            public void TryCast(float fixedDeltaTime)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (slot.Ability == null)
                        continue;

                    if (slot.Ability.chargesOnHold)
                        AdvanceCharge(i, slot, fixedDeltaTime);
                    else
                        TryCastBufferedPress(slot);
                }
            }

            public bool TryFire(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsReady)
                    return false;

                slot.BeginCast();
                return true;
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Ability != null)
                        slot.Ability.ModifyStats(owner, stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit)
                        slot.Ability.ModifyStatsWhileLit(owner, stats);
            }

            public void TickTimers(float fixedDeltaTime)
            {
                foreach (Slot slot in slots)
                {
                    if (slot.CooldownLeft > 0f)
                        slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.Ability.OnLit(owner, fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.LitLeft -= fixedDeltaTime;

                    if (slot.LitLeft <= 0f)
                        PutOut(slot);
                }
            }

            public void ResetForRun()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Fill(slot.Ability);

                    if (slot.Ability != null)
                        slot.Ability.OnRunReset(owner);
                }
            }

            void ReloadSlot(Slot slot, Ability next)
            {
                slot.Rank = next != null ? Progress.Rank(next.Key) : 0;

                if (slot.Ability == next)
                    return;

                if (slot.Ability != null)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Ability.OnUnequipped(owner);
                }

                slot.Fill(next);

                if (next != null)
                    next.OnEquipped(owner);
            }

            Slot SlotOf(Ability spell)
            {
                if (spell == null)
                    return null;

                foreach (Slot slot in slots)
                {
                    if (slot.Ability == spell)
                        return slot;
                }

                return null;
            }

            void PutOut(Slot slot)
            {
                slot.LitLeft = 0f;
                slot.CooldownLeft = slot.Ability.cooldown;
                slot.Ability.OnEnded(owner);
            }

            void ForgetInput(Slot slot)
            {
                slot.BufferLeft = 0f;

                if (slot.HeldFor > 0f && slot.Ability != null)
                {
                    slot.Ability.OnChargeLost(owner);
                    slot.DropCharge();
                }
            }

            void RefuseEmptySlot(int slotIndex, Slot slot)
            {
                if (slot.Action.WasPressedThisFrame())
                    Explain(slotIndex, null, "there is nothing in that slot");

                slot.BufferLeft = 0f;
            }

            void Explain(int slotIndex, Ability spell, string reason)
            {
#if UNITY_EDITOR
                if (!explainRefusals || string.IsNullOrEmpty(reason))
                    return;

                string spellName = spell != null ? spell.Name : $"Slot {slotIndex + 1}";

                Debug.LogWarning($"{spellName} did not cast: {reason}.");
#endif
            }

            void ReadButton(int slotIndex, Slot slot, float deltaTime)
            {
                bool pressed = slot.Action.WasPressedThisFrame();

                slot.IsHeld = slot.Action.IsPressed();

                if (slot.Action.WasReleasedThisFrame() && slot.HeldFor > 0f)
                    slot.ReleasedAfter = slot.HeldFor;

                if (pressed)
                {
                    slot.BufferLeft = slot.Ability.pressBuffer;
                    slot.HasFired = false;
                    slot.WasExplained = false;
                    return;
                }

                if (slot.Ability.chargesOnHold)
                    return;

                CountDownBuffer(slotIndex, slot, deltaTime);
            }

            void CountDownBuffer(int slotIndex, Slot slot, float deltaTime)
            {
                float bufferBefore = slot.BufferLeft;
                slot.BufferLeft -= deltaTime;

                if (bufferBefore > 0f && slot.BufferLeft <= 0f && !slot.HasFired)
                    Explain(slotIndex, slot.Ability, DescribeRefusal(slot));
            }

            string DescribeRefusal(Slot slot)
            {
                if (slot.CooldownLeft > 0f)
                    return $"it is still cooling down, {slot.CooldownLeft:0.0}s to go";

                if (!slot.HasUsesLeft)
                    return "it has no casts left in this level";

                return slot.Ability.WhyNot(owner);
            }

            void AdvanceCharge(int slotIndex, Slot slot, float fixedDeltaTime)
            {
                if (slot.ReleasedAfter >= 0f)
                {
                    ReleaseCharge(slot);
                    return;
                }

                if (!slot.IsHeld || !slot.IsReady)
                {
                    slot.HeldFor = 0f;
                    return;
                }

                slot.HeldFor += fixedDeltaTime;
                slot.Ability.OnHeld(owner, slot.HeldFor, fixedDeltaTime);

                ExplainRefusedHold(slotIndex, slot);
            }

            void ReleaseCharge(Slot slot)
            {
                float heldSeconds = slot.ReleasedAfter;

                slot.ReleasedAfter = NotReleased;
                slot.HeldFor = 0f;

                slot.Ability.OnReleased(owner, heldSeconds);
            }

            void ExplainRefusedHold(int slotIndex, Slot slot)
            {
                if (slot.WasExplained || slot.HeldFor < ExplainAfter || slot.Ability.CanCast(owner))
                    return;

                slot.WasExplained = true;
                Explain(slotIndex, slot.Ability, DescribeRefusal(slot));
            }

            void TryCastBufferedPress(Slot slot)
            {
                if (slot.BufferLeft <= 0f || !slot.IsReady)
                    return;

                if (!slot.Ability.CanCast(owner))
                    return;

                if (!slot.Ability.OnCast(owner))
                    return;

                slot.BufferLeft = 0f;
                slot.BeginCast();
            }

            public class Slot
            {
                public float BufferLeft;
                public bool HasFired;

                public int Rank;

                public bool IsHeld;
                public float HeldFor;

                public bool WasExplained;

                public float ReleasedAfter = NotReleased;
                public float LitLeft;
                public float CooldownLeft;

                public Ability Ability { get; private set; }

                public InputAction Action { get; private set; }

                public int UsesLeft { get; private set; }

                public bool IsLit => LitLeft > 0f;

                public bool HasUsesLeft =>
                    Ability == null || Ability.usesPerLevel <= 0 || UsesLeft > 0;

                public bool IsReady => Ability != null && CooldownLeft <= 0f && HasUsesLeft;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;

                public Slot(InputAction action) => Action = action;

                public void Fill(Ability spell)
                {
                    Ability = spell;
                    BufferLeft = 0f;
                    LitLeft = 0f;
                    CooldownLeft = 0f;
                    UsesLeft = spell != null ? spell.usesPerLevel : 0;

                    DropCharge();
                }

                public void DropCharge()
                {
                    IsHeld = false;
                    HeldFor = 0f;
                    ReleasedAfter = NotReleased;
                    WasExplained = false;
                }

                public void BeginCast()
                {
                    HasFired = true;
                    LitLeft = Ability.activeDuration;

                    if (Ability.usesPerLevel > 0)
                        UsesLeft = Mathf.Max(0, UsesLeft - 1);

                    if (LitLeft <= 0f)
                        CooldownLeft = Ability.cooldown;
                }
            }
        }
    }
}
