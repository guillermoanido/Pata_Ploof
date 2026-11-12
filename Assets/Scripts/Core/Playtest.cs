using System;
using System.Collections.Generic;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.Core
{
    [DefaultExecutionOrder(RunBeforeTheWizardWakes)]
    public class Playtest : MonoBehaviour
    {
        const int RunBeforeTheWizardWakes = -100;

        [Header("Playtest")]
        [Tooltip("Tick a spell and press Play with it already learned and in a slot, so you can " +
                 "try it without earning it first. Untick this whole component to play the game " +
                 "properly. Nothing here is ever written to the save - the real one is left " +
                 "exactly as it was.")]
        public bool active = true;

        [Tooltip("Deal the kit again every time a level loads. OFF - the default - deals it " +
                 "once when you press Play and then leaves it alone, so a loadout you rearranged " +
                 "at the skill screen survives going on to the next level. That is what this " +
                 "being on by accident used to break. A death wipes the playtest too, so the " +
                 "ticks are dealt afresh after one either way. Turn it on while you are tuning " +
                 "the ticks.")]
        public bool redealOnEveryLoad = false;

        [Tooltip("The catalogue the list below is built from. Filled in when the component is " +
                 "added; it is a field rather than something looked up on the fly so the list " +
                 "can be kept in step from OnValidate, where changes actually get saved.")]
        public AbilityBook book;

        [Tooltip("Every spell in the book, with a box each. The list fills itself in and keeps " +
                 "itself in step with the catalogue, so a spell added later turns up on its own.")]
        public List<Pick> spells = new List<Pick>();

        [Header("Purse")]
        [Tooltip("Magical Scrap to start with, for trying the skill screen without going and " +
                 "earning it.")]
        [Min(0)] public int scrap = 5;

        [Tooltip("Extra hearts to start with, on top of the base bar.")]
        [Min(0)] public int bonusHearts = 0;

        [Header("Everything")]
        [Tooltip("Learn every spell in the book, whether or not its box is ticked. The ticks " +
                 "still decide which ones are actually IN a slot - there are only four of those, " +
                 "and one of them is the Staff.")]
        public bool learnEverything = false;

        [Tooltip("Say in the console what was handed out. Worth leaving on: if a slot comes up " +
                 "empty this is how you tell whether it was this component or something later.")]
        public bool announce = true;

        void Reset()
        {
            book = LoadBook();
            SyncWithTheBook();
        }

        void OnValidate()
        {
            if (book != null)
            {
                SyncWithTheBook();
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += LoadTheMissingBook;
#endif
        }

        void Awake()
        {
            if (!active || !TryFindBook())
                return;

            if (Progress.IsSandboxSeeded && !redealOnEveryLoad)
            {
                if (announce)
                    Debug.Log("Playtest already dealt this session - keeping the loadout you " +
                              "left with. Tick 'Redeal On Every Load' to start from the ticks " +
                              "each time.", this);
                return;
            }

            Progress.BeginSandbox(reseed: true);
            Progress.AddScrap(scrap);
            Progress.SetHearts(bonusHearts);

            EquipLockedSpells();
            BringTickedSpells();
        }

        static AbilityBook LoadBook() => Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

#if UNITY_EDITOR
        void LoadTheMissingBook()
        {
            if (this == null || book != null)
                return;

            book = LoadBook();

            if (book == null)
                return;

            SyncWithTheBook();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        bool TryFindBook()
        {
            if (book == null)
                book = LoadBook();

            if (book != null)
                return true;

            Debug.LogError("Playtest has no spellbook, so it cannot hand anything out. Put " +
                           "Assets/Resources/Spellbook.asset in its Book field.", this);
            return false;
        }

        void EquipLockedSpells()
        {
            foreach (Ability spell in book.spells)
            {
                if (spell == null || !spell.locked || spell.fixedSlot < 0)
                    continue;

                Progress.Grant(spell.Key);
                Progress.Equip(spell.fixedSlot, spell.Key);
            }
        }

        void BringTickedSpells()
        {
            var brought = new List<string>();
            var missed = new List<string>();

            foreach (Pick pick in spells)
            {
                if (pick.spell == null)
                    continue;

                if (pick.bring || learnEverything)
                    pick.Learn();

                if (!pick.bring || pick.spell.locked)
                    continue;

                PutInFirstEmptySlot(pick, brought, missed);
            }

            LogWhatWasBrought(brought, missed);
        }

        void PutInFirstEmptySlot(Pick pick, List<string> brought, List<string> missed)
        {
            int slot = Progress.FirstEmptySlot();

            if (slot == Progress.NoSlot)
            {
                missed.Add(pick.spell.displayName);
                return;
            }

            Progress.Equip(slot, pick.spell.Key);
            brought.Add($"{pick.spell.displayName} in slot {slot + 1}");
        }

        void LogWhatWasBrought(List<string> brought, List<string> missed)
        {
            if (missed.Count > 0)
                Debug.LogWarning($"Playtest could not find a slot for {string.Join(", ", missed)}. " +
                                 "There are four buttons and the Staff is welded to one of them, " +
                                 "so three ticks is the most that can be carried at once.", this);

            if (!announce)
                return;

            if (spells.Count == 0)
                Debug.LogWarning("Playtest has an empty spell list. Press the Book field's picker " +
                                 "or re-add the component to fill it in.", this);
            else if (brought.Count == 0)
                Debug.Log("Playtest brought nothing - no boxes are ticked.", this);
            else
                Debug.Log($"Playtest brought {string.Join(", ", brought)}.", this);
        }

        void SyncWithTheBook()
        {
            if (book == null)
                return;

            var kept = new List<Pick>(book.spells.Count);

            foreach (Ability spell in book.spells)
            {
                if (spell == null)
                    continue;

                Pick existing = FindPickFor(spell);

                kept.Add(existing ?? new Pick { spell = spell });
            }

            spells = kept;
        }

        Pick FindPickFor(Ability spell)
        {
            foreach (Pick pick in spells)
                if (pick.spell == spell)
                    return pick;

            return null;
        }

        [Serializable]
        public class Pick
        {
            [Tooltip("Bring this one down with you.")]
            public bool bring;

            [Tooltip("Rank to start it at, for trying an upgrade without buying it. 1 is just " +
                     "learning it. Clamped to the spell's own top rank.")]
            [Min(1)] public int rank = 1;

            public Ability spell;

            public void Learn()
            {
                Progress.Grant(spell.Key);

                if (rank > 1)
                    Progress.SetRank(spell.Key,
                        Mathf.Min(rank, spell.MaxRank));
            }
        }
    }
}
