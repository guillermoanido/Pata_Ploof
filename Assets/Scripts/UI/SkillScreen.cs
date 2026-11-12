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
        const int SortingOrder = 220;

        const float BothSides = 2f;
        const float GapsInACard = 2f;

        const float PanelWidth = 1180f;
        const float PanelPadding = 24f;
        const float PanelSpacing = 8f;

        const float RowHeight = 84f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float ActionHeight = 46f;
        const float ActionFontSize = 22f;
        const float SlotSize = 92f;

        const float SlotCaptionHeight = 30f;

        const float TitleFontSize = 44f;
        const float TitleHeight = 52f;
        const float PurseFontSize = 28f;
        const float PurseHeight = 36f;
        const float HintFontSize = 20f;
        const float HintHeight = 28f;
        const float NoticeFontSize = 26f;
        const float NoticeHeight = 44f;

        const float NameFontSize = 24f;
        const float NameHeight = 28f;
        const float BlurbFontSize = 19f;
        const float BlurbHeight = 30f;

        const float GlyphFontSize = 34f;
        const float CaptionFontSize = 17f;
        const float CaptionHeight = 26f;

        const float PipSize = 12f;
        const float RoomForPips = 90f;

        const float DiveWidth = 420f;
        const float DiveHeight = 58f;
        const float DiveFontSize = 28f;

        const float CardHorizontalPadding = 18f;
        const float CardVerticalPadding = 8f;
        const float CardSpacing = 20f;
        const float SlotSpacing = 16f;
        const float CellSpacing = 4f;
        const float WordSpacing = 2f;
        const float HeadingSpacing = 10f;

        const float CardWidthBesideWords = IconSize + ActionWidth +
                                           CardHorizontalPadding * BothSides +
                                           CardSpacing * GapsInACard;

        const float InnerWidth = PanelWidth - PanelPadding * BothSides;

        const float WordsWidth = InnerWidth - CardWidthBesideWords;
        const float WordsHeight = RowHeight - CardVerticalPadding * BothSides;

        static readonly Color UnownedIconTint = new Color(1f, 1f, 1f, 0.3f);

        readonly Dictionary<GameObject, Ability> spellsByCard =
            new Dictionary<GameObject, Ability>();

        AbilityBook book;
        Action onDive;
        Action onClose;

        string diveKey = Loc.Keys.SkillDive;

        RectTransform body;
        InputAction[] slotInputs;

        string focusKey = string.Empty;

        [NonSerialized] int openedOnFrame;

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
            Core.Controls.SchemeChanged -= Redraw;
            Loc.Changed -= Redraw;
        }

        public static SkillScreen Open(Action onDive, string labelKey = null) =>
            CreateScreen(onDive, labelKey, null);

        public static SkillScreen OpenInLevel(Action onClose) =>
            CreateScreen(null, Loc.Keys.SkillBack, onClose);

        static SkillScreen CreateScreen(Action onDive, string labelKey, Action onClose)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Skill Screen", SortingOrder);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

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
            Ui.CreateShroud(transform);

            body = Ui.CreateSheet("Panel", transform, Ui.Panel, PanelWidth, PanelPadding,
                PanelSpacing);

            slotInputs = new InputAction[PlayerLogic.Spellbook.SlotCount];

            for (int i = 0; i < slotInputs.Length; i++)
                slotInputs[i] = Core.Controls.Player(PlayerLogic.Spellbook.SlotActions[i]);

            if (book != null)
                PlayerLogic.Spellbook.Seed(book);

            Core.Controls.SchemeChanged += Redraw;
            Loc.Changed += Redraw;

            Redraw();
        }

        bool ShouldLeave() =>
            Time.frameCount != openedOnFrame &&
            (Core.Controls.WasPausePressed || Core.Controls.WasCancelPressed ||
             Core.Controls.WasLoadoutPressed);

        void Leave()
        {
            Screens.Release();

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

        void Redraw()
        {
            ClearScreen();

            Ui.CreateLabel(Loc.Get(Loc.Keys.SkillTitle), body, TitleFontSize, InnerWidth,
                TitleHeight);

            TextMeshProUGUI purse = Ui.CreateLabel(
                Loc.Format(Loc.Keys.SkillPurse, Progress.Scrap), body, PurseFontSize, InnerWidth,
                PurseHeight);
            purse.color = Ui.Accent;

            DrawSlots();
            DrawHint();

            GameObject cardToFocus = DrawSpells();

            Ui.CreateButton(Loc.Get(diveKey), body, DiveWidth, DiveHeight, DiveFontSize)
                .onClick.AddListener(Leave);

            Ui.Focus(cardToFocus);
        }

        void ClearScreen()
        {
            spellsByCard.Clear();

            for (int i = body.childCount - 1; i >= 0; i--)
            {
                GameObject child = body.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                Destroy(child);
            }
        }

        void DrawSlots()
        {
            RectTransform strip = Ui.CreateRow("Slots", body, InnerWidth,
                SlotSize + SlotCaptionHeight, SlotSpacing, TextAnchor.MiddleCenter);

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
                DrawSlot(strip, i);
        }

        void DrawSlot(RectTransform strip, int slot)
        {
            Ability held = FindSpellInSlot(slot);
            bool locked = held != null && held.locked;

            RectTransform cell = Ui.CreateColumn($"Slot {slot + 1}", strip, SlotSize, CellSpacing,
                TextAnchor.UpperCenter);
            Ui.SetSize(cell.gameObject, SlotSize, SlotSize + SlotCaptionHeight);

            DrawSlotPlate(cell, slot, held, locked);
            DrawSlotCaption(cell, slot, held, locked);
        }

        void DrawSlotPlate(RectTransform cell, int slot, Ability held, bool locked)
        {
            Image plate = Ui.CreatePlate("Plate", cell, held != null ? Ui.CardLit : Ui.Card,
                SlotSize, SlotSize);

            Button button = Ui.MakePressable(plate);
            button.interactable = !locked;
            button.onClick.AddListener(() => Assign(FindFocusedSpell(), slot));

            if (held != null && held.icon != null)
                Ui.CreateIcon(plate.transform, held.icon, IconSize, Color.white);
            else
                Ui.CreateLabel(GetSlotGlyph(slot), plate.transform, GlyphFontSize, SlotSize, SlotSize)
                    .color = Ui.FadedInk;
        }

        void DrawSlotCaption(RectTransform cell, int slot, Ability held, bool locked)
        {
            TextMeshProUGUI caption = Ui.CreateLabel(
                held != null ? $"{GetSlotGlyph(slot)}  {held.Name}" : GetSlotGlyph(slot), cell,
                CaptionFontSize, SlotSize, CaptionHeight);

            if (locked)
                caption.color = Ui.Warning;
            else if (held != null)
                caption.color = Ui.Ink;
            else
                caption.color = Ui.FadedInk;
        }

        void DrawHint()
        {
            Ui.CreateLabel(WriteHint(), body, HintFontSize, InnerWidth, HintHeight).color =
                Ui.FadedInk;
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

        GameObject DrawSpells()
        {
            if (book == null)
            {
                Ui.CreateLabel(Loc.Get(Loc.Keys.SkillNoBook), body,
                    NoticeFontSize, InnerWidth, NoticeHeight).color = Ui.Warning;
                return null;
            }

            GameObject cardToFocus = null;

            foreach (Ability spell in book.spells)
            {
                if (spell == null)
                    continue;

                GameObject card = DrawSpell(spell);

                if (cardToFocus == null || spell.Key == focusKey)
                    cardToFocus = card;
            }

            return cardToFocus;
        }

        GameObject DrawSpell(Ability spell)
        {
            bool owned = Progress.Owns(spell.Key);
            int rank = Progress.Rank(spell.Key);
            int slot = Progress.SlotHolding(spell.Key);
            bool equipped = slot >= 0;

            Image card = DrawCard(spell, owned, equipped);

            Ui.CreateIcon(card.transform, spell.icon, IconSize,
                owned ? Color.white : UnownedIconTint);

            DrawWords(spell, card.transform, owned, rank, slot);
            DrawAction(spell, card.transform, owned, rank);

            return card.gameObject;
        }

        Image DrawCard(Ability spell, bool owned, bool equipped)
        {
            RectTransform row = Ui.CreateRow(spell.displayName, body, InnerWidth, RowHeight,
                CardSpacing);

            Image card = Ui.CreatePlate("Card", row, equipped ? Ui.CardLit : Ui.Card,
                InnerWidth, RowHeight);

            Button button = Ui.MakePressable(card);
            button.interactable = owned;
            button.onClick.AddListener(() => { focusKey = spell.Key; Redraw(); });

            spellsByCard[card.gameObject] = spell;

            ArrangeCard(card);

            return card;
        }

        void ArrangeCard(Image card)
        {
            var cardLayout = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            cardLayout.childAlignment = TextAnchor.MiddleLeft;
            cardLayout.spacing = CardSpacing;
            cardLayout.padding = new RectOffset(
                (int)CardHorizontalPadding, (int)CardHorizontalPadding,
                (int)CardVerticalPadding, (int)CardVerticalPadding);
            Ui.SizeChildrenWithoutStretching(cardLayout);
        }

        void DrawWords(Ability spell, Transform parent, bool owned, int rank, int slot)
        {
            RectTransform words = Ui.CreateColumn("Words", parent, WordsWidth, WordSpacing,
                TextAnchor.MiddleLeft);
            Ui.SetSize(words.gameObject, WordsWidth, WordsHeight);

            RectTransform heading = Ui.CreateRow("Heading", words, WordsWidth, NameHeight,
                HeadingSpacing);

            TextMeshProUGUI nameLabel = Ui.CreateLabel(spell.Name, heading, NameFontSize,
                WordsWidth - (spell.HasUpgrades ? RoomForPips : 0f), NameHeight,
                TextAlignmentOptions.Left);
            nameLabel.color = owned ? Ui.Ink : Ui.FadedInk;

            if (spell.HasUpgrades)
                Ui.CreatePips(heading, rank, spell.MaxRank, PipSize,
                    owned ? Ui.Accent : Ui.FadedInk);

            Ui.CreateLabel(WriteBlurb(spell, owned, rank, slot), words, BlurbFontSize, WordsWidth,
                BlurbHeight, TextAlignmentOptions.TopLeft).color = Ui.FadedInk;
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

        void DrawAction(Ability spell, Transform parent, bool owned, int rank)
        {
            if (!owned)
            {
                DrawBuyButton(spell, parent);
                return;
            }

            Ability.Upgrade nextUpgrade = spell.NextUpgrade(rank);

            if (nextUpgrade == null)
            {
                Ui.CreateLabel(
                    Loc.Get(spell.HasUpgrades ? Loc.Keys.SkillMastered : Loc.Keys.SkillLearned),
                    parent, ActionFontSize, ActionWidth, ActionHeight).color = Ui.FadedInk;
                return;
            }

            DrawUpgradeButton(spell, parent, rank, nextUpgrade);
        }

        void DrawBuyButton(Ability spell, Transform parent)
        {
            bool canAfford = Progress.CanAfford(spell.cost);

            Button buyButton = Ui.CreateButton(
                canAfford ? Loc.Format(Loc.Keys.SkillLearn, spell.cost)
                          : Loc.Format(Loc.Keys.SkillPrice, spell.cost),
                parent, ActionWidth, ActionHeight, ActionFontSize);

            buyButton.interactable = canAfford;

            if (canAfford)
                buyButton.onClick.AddListener(() => LearnSpell(spell));
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

        void DrawUpgradeButton(Ability spell, Transform parent, int rank,
            Ability.Upgrade nextUpgrade)
        {
            bool canAfford = Progress.CanAfford(nextUpgrade.cost);

            Button upgradeButton = Ui.CreateButton(
                canAfford ? $"{spell.UpgradeTitle(rank)} - {nextUpgrade.cost}"
                          : Loc.Format(Loc.Keys.SkillPrice, nextUpgrade.cost),
                parent, ActionWidth, ActionHeight, ActionFontSize);

            upgradeButton.interactable = canAfford;

            if (canAfford)
                upgradeButton.onClick.AddListener(() => BuyUpgrade(spell, nextUpgrade));
        }

        void BuyUpgrade(Ability spell, Ability.Upgrade nextUpgrade)
        {
            if (!Progress.TryUpgrade(spell.Key, nextUpgrade.cost, spell.MaxRank))
                return;

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

            if (selected != null && spellsByCard.TryGetValue(selected, out Ability spell))
            {
                focusKey = spell.Key;
                return spell;
            }

            return FindSpell(focusKey);
        }

        void ApplyChanges(Ability changed)
        {
            focusKey = changed.Key;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.Reload();

            Redraw();
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
                if (openScreen != null || Screens.IsModalOpen || !Core.Controls.WasLoadoutPressed)
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
