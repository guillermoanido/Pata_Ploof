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

        [Tooltip("One spell button slot, copied once per button and placed where it sits. It " +
                 "stays switched off itself, so restyle it and every slot follows.")]
        public SlotParts slotTemplate = new SlotParts();

        [Tooltip("One spell card, copied once per spell in the catalogue and placed where it " +
                 "sits. It stays switched off itself, so restyle it and every card follows.")]
        public CardParts cardTemplate = new CardParts();

        readonly List<SlotParts> slots = new List<SlotParts>();
        readonly List<CardParts> cards = new List<CardParts>();

        AbilityBook book;
        Action onDive;
        Action onClose;

        string diveKey = Loc.Keys.SkillDive;
        string focusKey = string.Empty;

        InputAction[] slotInputs;

        [NonSerialized] int openedOnFrame;

        void Awake()
        {
            if (slotTemplate.root != null)
                slotTemplate.root.SetActive(false);

            if (cardTemplate.root != null)
                cardTemplate.root.SetActive(false);

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

            BuildSlots();
            BuildCards();

            Core.Controls.SchemeChanged += Refresh;
            Loc.Changed += Refresh;

            Refresh();
            FocusCard();
        }

        void BuildSlots()
        {
            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
            {
                SlotParts slot = slotTemplate.Copy();
                int index = i;
                slot.plate.onClick.AddListener(() => Assign(FindFocusedSpell(), index));
                slots.Add(slot);
            }
        }

        void BuildCards()
        {
            if (book == null)
                return;

            foreach (Ability spell in book.spells)
            {
                if (spell == null)
                    continue;

                CardParts card = cardTemplate.Copy();
                card.spell = spell;
                card.root.name = spell.displayName;
                card.button.onClick.AddListener(() => Focus(spell));
                card.action.onClick.AddListener(() => Act(spell));
                cards.Add(card);
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

            foreach (CardParts card in cards)
                ShowCard(card);
        }

        void ShowSlot(SlotParts slot, int index)
        {
            Ability held = FindSpellInSlot(index);
            bool hasIcon = held != null && held.icon != null;

            slot.plate.interactable = held == null || !held.locked;

            slot.icon.gameObject.SetActive(hasIcon);
            slot.icon.sprite = hasIcon ? held.icon : null;

            if (slot.prompt != null)
                slot.prompt.Show(FindSlotAction(index));

            slot.caption.text = held != null ? held.Name : string.Empty;
        }

        void ShowCard(CardParts card)
        {
            Ability spell = card.spell;
            bool owned = Progress.Owns(spell.Key);
            int rank = Progress.Rank(spell.Key);

            card.button.interactable = owned;

            card.icon.sprite = spell.icon;
            card.icon.enabled = spell.icon != null;
            ShowLook(card.lockedLook, !owned);
            ShowEquipped(card, Progress.SlotHolding(spell.Key));

            card.nameLabel.text = spell.Name;
            card.pips.Show(spell.HasUpgrades ? spell.MaxRank : 0, rank);
            card.blurb.text = WriteBlurb(spell, owned, rank);

            ShowAction(card, spell, owned, rank);
        }

        void ShowEquipped(CardParts card, int slot)
        {
            bool isEquipped = slot >= 0;

            ShowLook(card.equippedLook, isEquipped);

            if (isEquipped && card.equippedPrompt != null)
                card.equippedPrompt.Show(FindSlotAction(slot));
        }

        void ShowAction(CardParts card, Ability spell, bool owned, int rank)
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
                return Loc.Format(Loc.Keys.SkillHintMove, picked.Name);

            return Loc.Format(Loc.Keys.SkillHintLocked, picked.Name);
        }

        static string WriteBlurb(Ability spell, bool owned, int rank)
        {
            Ability.Upgrade nextUpgrade = owned ? spell.NextUpgrade(rank) : null;

            if (nextUpgrade == null)
                return spell.Description;

            return spell.Description + "\n" + Loc.Format(Loc.Keys.SkillNext, spell.UpgradeDescription(rank));
        }

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

            foreach (CardParts card in cards)
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
            CardParts toFocus = null;

            foreach (CardParts card in cards)
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

        [Serializable]
        public class SlotParts
        {
            public GameObject root;
            public Button plate;

            [Tooltip("The spell in the slot. Hidden while the slot is empty.")]
            public Image icon;

            [Tooltip("Which button this slot is, as a picture.")]
            public ButtonPrompt prompt;

            [Tooltip("The name of the spell in the slot.")]
            public TextMeshProUGUI caption;

            public SlotParts Copy()
            {
                GameObject copy = Ui.CopyOf(root);

                return new SlotParts
                {
                    root = copy,
                    plate = Ui.SameChildIn(copy, root, plate),
                    icon = Ui.SameChildIn(copy, root, icon),
                    prompt = Ui.SameChildIn(copy, root, prompt),
                    caption = Ui.SameChildIn(copy, root, caption),
                };
            }
        }

        [Serializable]
        public class CardParts
        {
            public GameObject root;

            [Tooltip("The card itself. Selecting it picks the spell to move between slots.")]
            public Button button;

            public Image icon;

            [Tooltip("Shown while the spell is not learned yet, for example a padlock over the " +
                     "icon. A child object, switched on and off.")]
            public GameObject lockedLook;

            [Tooltip("Shown while the spell sits in one of the slots. A child object, switched on " +
                     "and off.")]
            public GameObject equippedLook;

            [Tooltip("Inside the Equipped Look: which button the spell is on, as a picture.")]
            public ButtonPrompt equippedPrompt;

            public TextMeshProUGUI nameLabel;

            [Tooltip("One pip per rank the spell can reach, filled up to the rank it has.")]
            public BlipRow pips = new BlipRow();

            public TextMeshProUGUI blurb;

            [Tooltip("Learn or upgrade button.")]
            public Button action;

            [Tooltip("Shown instead of the button once there is nothing left to buy.")]
            public TextMeshProUGUI status;

            [NonSerialized] public Ability spell;

            public CardParts Copy()
            {
                GameObject copy = Ui.CopyOf(root);

                return new CardParts
                {
                    root = copy,
                    button = Ui.SameChildIn(copy, root, button),
                    icon = Ui.SameChildIn(copy, root, icon),
                    lockedLook = SameObjectIn(copy, lockedLook),
                    equippedLook = SameObjectIn(copy, equippedLook),
                    equippedPrompt = Ui.SameChildIn(copy, root, equippedPrompt),
                    nameLabel = Ui.SameChildIn(copy, root, nameLabel),
                    pips = pips.CopyInto(copy, root),
                    blurb = Ui.SameChildIn(copy, root, blurb),
                    action = Ui.SameChildIn(copy, root, action),
                    status = Ui.SameChildIn(copy, root, status),
                };
            }

            GameObject SameObjectIn(GameObject copy, GameObject part)
            {
                if (part == null)
                    return null;

                Transform match = Ui.SameChildIn(copy, root, part.transform);
                return match != null ? match.gameObject : null;
            }
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
