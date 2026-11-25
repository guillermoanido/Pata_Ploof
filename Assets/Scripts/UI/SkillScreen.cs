using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Localization;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class SkillScreen : MonoBehaviour
    {
        const string PrefabName = "Skill Screen";

        [Header("Rig")]
        public TextMeshProUGUI purse;
        public TextMeshProUGUI hint;

        [Tooltip("Shown only when the spell catalogue cannot be found.")]
        public GameObject noBookNotice;

        public Button diveButton;

        readonly List<SpellSlot> slots = new List<SpellSlot>();
        readonly List<SpellCard> cards = new List<SpellCard>();

        AbilityBook book;
        Action onDive;
        Action onClose;

        string diveKey = Loc.Keys.SkillDive;
        string focusKey = string.Empty;

        InputAction[] slotInputs;

        [NonSerialized] int openedOnFrame;

        void Awake()
        {
            if (diveButton != null)
                diveButton.onClick.AddListener(Leave);
        }

        void Update()
        {
            if (ShouldLeave())
            {
                Leave();
                return;
            }

            AssignToPressedSlot();
        }

        void OnDestroy()
        {
            Core.Controls.SchemeChanged -= Refresh;
            Loc.Changed -= Refresh;
        }

        public static SkillScreen Open(Action onDive, string labelKey = null) =>
            CreateScreen(onDive, labelKey, null);

        public static SkillScreen OpenInLevel(Action onClose) =>
            CreateScreen(null, Loc.Keys.SkillBack, onClose);

        static SkillScreen CreateScreen(Action onDive, string labelKey, Action onClose)
        {
            var screen = Ui.OpenScreen<SkillScreen>(PrefabName);

            if (screen == null)
                return null;

            Game.SetPaused(true);
            Ui.ClaimScreen();

            screen.onDive = onDive;
            screen.diveKey = string.IsNullOrEmpty(labelKey) ? Loc.Keys.SkillDive : labelKey;
            screen.onClose = onClose;
            screen.openedOnFrame = Time.frameCount;
            screen.book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);
            screen.Build();

            return screen;
        }

        void Build()
        {
            slotInputs = new InputAction[PlayerLogic.Spellbook.SlotCount];

            for (int i = 0; i < slotInputs.Length; i++)
                slotInputs[i] = FindSlotAction(i);

            if (book != null)
                PlayerLogic.Spellbook.Seed(book);

            FindSlots();
            FindCards();
            WarnAboutSpellsWithoutCards();

            Core.Controls.SchemeChanged += Refresh;
            Loc.Changed += Refresh;

            Refresh();
            FocusCard();
        }

        void FindSlots()
        {
            GetComponentsInChildren(true, slots);

            while (slots.Count > PlayerLogic.Spellbook.SlotCount)
            {
                slots[slots.Count - 1].gameObject.SetActive(false);
                slots.RemoveAt(slots.Count - 1);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                int index = i;
                slots[i].plate.onClick.AddListener(() => Assign(FindFocusedSpell(), index));
            }
        }

        void FindCards()
        {
            GetComponentsInChildren(true, cards);
            cards.RemoveAll(HideIfItHasNoSpell);

            foreach (SpellCard card in cards)
            {
                Ability spell = card.spell;
                card.button.onClick.AddListener(() => Focus(spell));
                card.action.onClick.AddListener(() => Act(spell));
            }
        }

        static bool HideIfItHasNoSpell(SpellCard card)
        {
            if (card.spell != null)
                return false;

            card.gameObject.SetActive(false);
            return true;
        }

        void WarnAboutSpellsWithoutCards()
        {
            if (book == null)
                return;

            foreach (Ability spell in book.spells)
            {
                if (spell != null && !cards.Exists(card => card.spell == spell))
                    Debug.LogWarning($"{spell.displayName} is in the Spellbook but has no card on " +
                                     "the Skill Screen, so it cannot be bought. Drag a Spell Card " +
                                     "prefab into Cards and set its Spell.", this);
            }
        }

        bool ShouldLeave() =>
            Time.frameCount != openedOnFrame &&
            (Core.Controls.WasPausePressed || Core.Controls.WasCancelPressed ||
             Core.Controls.WasLoadoutPressed);

        void Leave()
        {
            Ui.ReleaseScreen();

            Destroy(gameObject);

            onDive?.Invoke();
            onClose?.Invoke();
        }

        void AssignToPressedSlot()
        {
            Ability picked = FindFocusedSpell();

            if (picked == null || slotInputs == null)
                return;

            for (int i = 0; i < slotInputs.Length; i++)
                if (slotInputs[i] != null && slotInputs[i].WasPressedThisFrame())
                {
                    Assign(picked, i);
                    return;
                }
        }

        void Refresh()
        {
            purse.text = Loc.Format(Loc.Keys.SkillPurse, Progress.Scrap);
            hint.text = WriteHint();
            Ui.Retext(diveButton, Loc.Get(diveKey));

            if (noBookNotice != null)
                noBookNotice.SetActive(book == null);

            for (int i = 0; i < slots.Count; i++)
                ShowSlot(slots[i], i);

            foreach (SpellCard card in cards)
                ShowCard(card);
        }

        void ShowSlot(SpellSlot slot, int index)
        {
            Ability held = FindSpellInSlot(index);
            Sprite picture = held != null ? IconOf(held) : null;

            slot.plate.interactable = held == null || !held.locked;

            slot.icon.gameObject.SetActive(picture != null);
            slot.icon.sprite = picture;

            if (slot.prompt != null)
                slot.prompt.Show(FindSlotAction(index));

            slot.caption.text = held != null ? NameOf(held) : string.Empty;
        }

        void ShowCard(SpellCard card)
        {
            Ability spell = card.spell;
            bool owned = Progress.Owns(spell.Key);
            int rank = Progress.Rank(spell.Key);

            card.button.interactable = owned;

            ShowLook(card.lockedLook, !owned);
            ShowEquipped(card, Progress.SlotHolding(spell.Key));

            card.pips.Show(spell.HasUpgrades ? spell.MaxRank : 0, rank);

            ShowNextUpgrade(card, spell, owned, rank);
            ShowAction(card, spell, owned, rank);
        }

        static void ShowNextUpgrade(SpellCard card, Ability spell, bool owned, int rank)
        {
            if (card.nextUpgrade == null)
                return;

            bool hasOne = owned && spell.NextUpgrade(rank) != null;

            card.nextUpgrade.gameObject.SetActive(hasOne);

            if (hasOne)
                card.nextUpgrade.text = Loc.Format(Loc.Keys.SkillNext, spell.UpgradeDescription(rank));
        }

        void ShowEquipped(SpellCard card, int slot)
        {
            bool isEquipped = slot >= 0;

            ShowLook(card.equippedLook, isEquipped);

            if (isEquipped && card.equippedPrompt != null)
                card.equippedPrompt.Show(FindSlotAction(slot));
        }

        void ShowAction(SpellCard card, Ability spell, bool owned, int rank)
        {
            Ability.Upgrade nextUpgrade = owned ? spell.NextUpgrade(rank) : null;
            bool isFinished = owned && nextUpgrade == null;

            card.action.gameObject.SetActive(!isFinished);
            card.status.gameObject.SetActive(isFinished);

            if (isFinished)
            {
                card.status.text = Loc.Get(spell.HasUpgrades ? Loc.Keys.SkillMastered : Loc.Keys.SkillLearned);
                return;
            }

            int cost = owned ? nextUpgrade.cost : spell.cost;
            bool canAfford = Progress.CanAfford(cost);
            string offer = owned ? $"{spell.UpgradeTitle(rank)} - {cost}" : Loc.Format(Loc.Keys.SkillLearn, cost);

            card.action.interactable = canAfford;
            Ui.Retext(card.action, canAfford ? offer : Loc.Format(Loc.Keys.SkillPrice, cost));
        }

        string WriteHint()
        {
            Ability picked = FindSpell(focusKey);

            if (picked == null)
                return Loc.Get(Loc.Keys.SkillHintPick);

            if (Progress.Owns(picked.Key))
                return Loc.Format(Loc.Keys.SkillHintMove, NameOf(picked));

            return Loc.Format(Loc.Keys.SkillHintLocked, NameOf(picked));
        }

        string NameOf(Ability spell)
        {
            SpellCard card = CardFor(spell);
            return card != null && card.nameLabel != null ? card.nameLabel.text : spell.Name;
        }

        Sprite IconOf(Ability spell)
        {
            SpellCard card = CardFor(spell);
            return card != null && card.icon != null && card.icon.sprite != null ? card.icon.sprite : spell.icon;
        }

        SpellCard CardFor(Ability spell) => cards.Find(card => card.spell == spell);

        void Focus(Ability spell)
        {
            focusKey = spell.Key;
            Refresh();
        }

        void Act(Ability spell)
        {
            if (!Progress.Owns(spell.Key))
            {
                LearnSpell(spell);
                return;
            }

            Ability.Upgrade nextUpgrade = spell.NextUpgrade(Progress.Rank(spell.Key));

            if (nextUpgrade != null && Progress.TryUpgrade(spell.Key, nextUpgrade.cost, spell.MaxRank))
                ApplyChanges(spell);
        }

        void LearnSpell(Ability spell)
        {
            if (!Progress.TryBuy(spell.Key, spell.cost))
                return;

            int freeSlot = Progress.FirstEmptySlot();

            if (freeSlot >= 0 && !spell.locked)
                Progress.Equip(freeSlot, spell.Key);

            ApplyChanges(spell);
        }

        void Assign(Ability spell, int slot)
        {
            if (spell == null || !Progress.Owns(spell.Key) || spell.locked)
                return;

            Ability held = FindSpellInSlot(slot);

            if (held != null && held.locked)
                return;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.TryEquip(spell, slot);
            else
                Progress.Place(slot, spell.Key);

            ApplyChanges(spell);
        }

        Ability FindFocusedSpell()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            foreach (SpellCard card in cards)
            {
                if (card.button.gameObject == selected)
                {
                    focusKey = card.spell.Key;
                    return card.spell;
                }
            }

            return FindSpell(focusKey);
        }

        void ApplyChanges(Ability changed)
        {
            focusKey = changed.Key;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.Reload();

            Refresh();
            FocusCard();
        }

        void FocusCard()
        {
            SpellCard toFocus = null;

            foreach (SpellCard card in cards)
            {
                if (toFocus == null || card.spell.Key == focusKey)
                    toFocus = card;
            }

            if (toFocus != null)
                Ui.Focus(toFocus.button.gameObject);
        }

        Ability FindSpell(string key) => book != null ? book.Find(key) : null;

        Ability FindSpellInSlot(int slot) =>
            book != null ? book.Find(Progress.EquippedIn(slot)) : null;

        static InputAction FindSlotAction(int slot)
        {
            if (slot < 0 || slot >= PlayerLogic.Spellbook.SlotActions.Length)
                return null;

            return Core.Controls.Player(PlayerLogic.Spellbook.SlotActions[slot]);
        }

        static void ShowLook(GameObject look, bool isShown)
        {
            if (look != null)
                look.SetActive(isShown);
        }

        class Door : MonoBehaviour
        {
            static Door instance;

            SkillScreen openScreen;

            bool wasPaused;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            static void Install()
            {
                if (instance != null)
                    return;

                var doorObject = new GameObject("Loadout Door", typeof(Door));
                DontDestroyOnLoad(doorObject);
                instance = doorObject.GetComponent<Door>();
            }

            void Update()
            {
                if (openScreen != null || Ui.IsModalOpen || !Core.Controls.WasLoadoutPressed)
                    return;

                wasPaused = Game.IsPaused;
                openScreen = OpenInLevel(OnScreenClosed);
            }

            void OnScreenClosed()
            {
                openScreen = null;

                if (!wasPaused)
                    Game.SetPaused(false);
            }
        }
    }
}
