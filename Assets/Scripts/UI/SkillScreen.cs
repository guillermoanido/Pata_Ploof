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

        static readonly Color UnownedIconTint = new Color(1f, 1f, 1f, 0.3f);

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
                slotInputs[i] = Core.Controls.Player(PlayerLogic.Spellbook.SlotActions[i]);

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
                card.BuildPips(spell.HasUpgrades ? spell.MaxRank : 0);
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
            bool locked = held != null && held.locked;
            bool hasIcon = held != null && held.icon != null;

            slot.plate.interactable = !locked;
            slot.plate.image.color = held != null ? Ui.CardLit : Ui.Card;

            slot.icon.gameObject.SetActive(hasIcon);
            slot.icon.sprite = hasIcon ? held.icon : null;

            slot.glyph.gameObject.SetActive(!hasIcon);
            slot.glyph.text = GetSlotGlyph(index);

            slot.caption.text = held != null ? $"{GetSlotGlyph(index)}  {held.Name}" : GetSlotGlyph(index);
            slot.caption.color = locked ? Ui.Warning : held != null ? Ui.Ink : Ui.FadedInk;
        }

        void ShowCard(CardParts card)
        {
            Ability spell = card.spell;
            bool owned = Progress.Owns(spell.Key);
            int rank = Progress.Rank(spell.Key);
            int slot = Progress.SlotHolding(spell.Key);

            card.button.interactable = owned;
            card.button.image.color = slot >= 0 ? Ui.CardLit : Ui.Card;

            card.icon.sprite = spell.icon;
            card.icon.color = owned ? Color.white : UnownedIconTint;

            card.nameLabel.text = spell.Name;
            card.nameLabel.color = owned ? Ui.Ink : Ui.FadedInk;
            card.ShowPips(rank, owned ? Ui.Accent : Ui.FadedInk);

            card.blurb.text = WriteBlurb(spell, owned, rank, slot);

            ShowAction(card, spell, owned, rank);
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
                return Loc.Format(Loc.Keys.SkillHintMove, picked.Name, ListFreeButtons());

            return Loc.Format(Loc.Keys.SkillHintLocked, picked.Name);
        }

        string ListFreeButtons()
        {
            var freeGlyphs = new List<string>();

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
            {
                Ability held = FindSpellInSlot(i);

                if (held == null || !held.locked)
                    freeGlyphs.Add(GetSlotGlyph(i));
            }

            return string.Join(" ", freeGlyphs);
        }

        string WriteBlurb(Ability spell, bool owned, int rank, int slot)
        {
            if (!owned)
                return spell.Description;

            Ability.Upgrade nextUpgrade = spell.NextUpgrade(rank);

            string placement = slot >= 0
                ? Loc.Format(Loc.Keys.SkillOn, GetSlotGlyph(slot))
                : Loc.Get(Loc.Keys.SkillBench);

            return nextUpgrade != null
                ? Loc.Format(Loc.Keys.SkillNext, placement, spell.UpgradeTitle(rank),
                             spell.UpgradeDescription(rank))
                : $"{placement}  {spell.Description}";
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

        static string GetSlotGlyph(int slot)
        {
            if (slot < 0 || slot >= PlayerLogic.Spellbook.SlotActions.Length)
                return string.Empty;

            return Core.Controls.Glyph(
                Core.Controls.Player(PlayerLogic.Spellbook.SlotActions[slot]));
        }

        [Serializable]
        public class SlotParts
        {
            public GameObject root;
            public Button plate;
            public Image icon;

            [Tooltip("Shows the button's name when the slot holds no spell.")]
            public TextMeshProUGUI glyph;

            public TextMeshProUGUI caption;

            public SlotParts Copy()
            {
                GameObject copy = Ui.CopyOf(root);

                return new SlotParts
                {
                    root = copy,
                    plate = Ui.SameChildIn(copy, root, plate),
                    icon = Ui.SameChildIn(copy, root, icon),
                    glyph = Ui.SameChildIn(copy, root, glyph),
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
            public TextMeshProUGUI nameLabel;

            [Tooltip("One upgrade pip, copied once per rank the spell can reach. It stays " +
                     "switched off itself.")]
            public Image pipTemplate;

            public TextMeshProUGUI blurb;

            [Tooltip("Learn or upgrade button.")]
            public Button action;

            [Tooltip("Shown instead of the button once there is nothing left to buy.")]
            public TextMeshProUGUI status;

            [NonSerialized] public Ability spell;

            readonly List<Image> pips = new List<Image>();

            public CardParts Copy()
            {
                GameObject copy = Ui.CopyOf(root);

                return new CardParts
                {
                    root = copy,
                    button = Ui.SameChildIn(copy, root, button),
                    icon = Ui.SameChildIn(copy, root, icon),
                    nameLabel = Ui.SameChildIn(copy, root, nameLabel),
                    pipTemplate = Ui.SameChildIn(copy, root, pipTemplate),
                    blurb = Ui.SameChildIn(copy, root, blurb),
                    action = Ui.SameChildIn(copy, root, action),
                    status = Ui.SameChildIn(copy, root, status),
                };
            }

            public void BuildPips(int count)
            {
                if (pipTemplate == null)
                    return;

                pipTemplate.gameObject.SetActive(false);

                for (int i = 0; i < count; i++)
                    pips.Add(Ui.CopyOf(pipTemplate.gameObject).GetComponent<Image>());
            }

            public void ShowPips(int filled, Color lit)
            {
                for (int i = 0; i < pips.Count; i++)
                    pips[i].color = i < filled ? lit : Ui.PipEmpty;
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
