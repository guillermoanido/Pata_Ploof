using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FallingWizard.Core
{
    public enum StaysTaken
    {
        ForThisRun,
        OnceBanked,
        ForGood,
    }

    public static class Progress
    {
        public const int SlotCount = 4;
        public const int NoSlot = -1;

        const string FileName = "progress.json";

        const int FormatVersion = 1;

        const string LegacyKeyPrefix = "FallingWizard.";
        const string LegacyWispsKey = LegacyKeyPrefix + "Wisps";
        const string LegacyRanksKey = LegacyKeyPrefix + "Ranks";
        const string LegacyLoadoutKey = LegacyKeyPrefix + "Loadout";
        const string LegacySpentKey = LegacyKeyPrefix + "Spent";
        const string LegacyHeartsKey = LegacyKeyPrefix + "Hearts";
        const char LegacyListSeparator = ';';
        const char LegacyRankSeparator = ':';

        static readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
        static readonly string[] equipped = new string[SlotCount];

        static readonly HashSet<string> found = new HashSet<string>();
        static readonly HashSet<string> carried = new HashSet<string>();
        static readonly HashSet<string> spent = new HashSet<string>();
        static readonly HashSet<string> completedLevels = new HashSet<string>();

        static bool isSandbox;
        static string checkpointScene = string.Empty;

        static bool isSaveFileUnreadable;
        static bool hasWarnedAboutUnreadable;

        public static bool IsSandboxSeeded { get; private set; }

        public static int Wisps { get; private set; }

        public static int CarriedWisps { get; private set; }

        public static int BonusHearts { get; private set; }

        public static bool HasCheckpoint { get; private set; }
        public static Vector2 CheckpointPoint { get; private set; }

        public static bool IsCheckpointHere =>
            HasCheckpoint && checkpointScene == SceneManager.GetActiveScene().name;

        public static bool IsSpendingFree { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnPlay() => Load();

        public static int Rank(string key)
        {
            if (string.IsNullOrEmpty(key))
                return 0;

            if (ranks.TryGetValue(key, out int rank))
                return rank;

            return 0;
        }

        public static bool Owns(string key) => Rank(key) > 0;

        public static void Grant(string key)
        {
            if (string.IsNullOrEmpty(key) || Owns(key))
                return;

            ranks[key] = 1;
            Save();
        }

        public static bool CanAfford(int cost) => IsSpendingFree || Wisps >= cost;

        public static bool TryBuy(string key, int cost)
        {
            if (string.IsNullOrEmpty(key) || Owns(key) || !CanAfford(cost))
                return false;

            PayWisps(cost);
            Grant(key);
            return true;
        }

        public static string EquippedIn(int slot) =>
            IsValidSlot(slot) ? equipped[slot] : string.Empty;

        public static int SlotHolding(string key)
        {
            if (string.IsNullOrEmpty(key))
                return NoSlot;

            for (int i = 0; i < SlotCount; i++)
                if (equipped[i] == key)
                    return i;

            return NoSlot;
        }

        public static void Equip(int slot, string key)
        {
            if (!IsValidSlot(slot))
                return;

            if (key == null)
                key = string.Empty;

            if (key.Length > 0)
                ClearSlotsHolding(key);

            equipped[slot] = key;
            Save();
        }

        public static void Place(int slot, string key)
        {
            if (!IsValidSlot(slot))
                return;

            if (key == null)
                key = string.Empty;

            int from = SlotHolding(key);

            if (key.Length > 0 && from != NoSlot && from != slot)
            {
                SwapSlots(from, slot);
                Save();
                return;
            }

            Equip(slot, key);
        }

        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (string.IsNullOrEmpty(equipped[i]))
                    return i;

            return NoSlot;
        }

        public static bool TryUpgrade(string key, int cost, int cap)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            int rank = Rank(key);

            if (rank < 1 || rank >= Mathf.Max(1, cap) || !CanAfford(cost))
                return false;

            PayWisps(cost);
            ranks[key] = rank + 1;
            Save();
            return true;
        }

        public static void SetRank(string key, int rank)
        {
            if (string.IsNullOrEmpty(key))
                return;

            if (rank <= 0)
                ranks.Remove(key);
            else
                ranks[key] = rank;

            Save();
        }

        public static bool IsGone(string id) =>
            !string.IsNullOrEmpty(id) && (found.Contains(id) || spent.Contains(id));

        public static void MarkFound(string id, StaysTaken staysTaken)
        {
            if (string.IsNullOrEmpty(id))
                return;

            found.Add(id);

            switch (staysTaken)
            {
                case StaysTaken.OnceBanked:
                    carried.Add(id);
                    break;

                case StaysTaken.ForGood:
                    spent.Add(id);
                    Save();
                    break;
            }
        }

        public static void GiveWisps(int amount)
        {
            if (amount > 0)
                Wisps += amount;
        }

        public static void CarryWisps(int amount)
        {
            if (amount > 0)
                CarriedWisps += amount;
        }

        public static void TakeHearts(int amount)
        {
            if (amount <= 0)
                return;

            BonusHearts += amount;
            Save();
        }

        public static void SetHearts(int amount) => BonusHearts = Mathf.Max(0, amount);

        public static void LoseCarried()
        {
            CarriedWisps = 0;

            found.ExceptWith(carried);
            carried.Clear();
        }

        public static void BankCarried()
        {
            Wisps += CarriedWisps;
            CarriedWisps = 0;

            spent.UnionWith(carried);
            carried.Clear();

            Save();
        }

        public static void MarkCheckpoint(Vector2 point)
        {
            CheckpointPoint = point;
            checkpointScene = SceneManager.GetActiveScene().name;
            HasCheckpoint = true;
        }

        public static void ClearCheckpoint()
        {
            HasCheckpoint = false;
            checkpointScene = string.Empty;
            CheckpointPoint = Vector2.zero;
        }

        public static bool HasCompletedLevel(string sceneName) => completedLevels.Contains(sceneName);

        public static void CompleteLevel(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !completedLevels.Add(sceneName))
                return;

            Save();
        }

        public static void EndRun()
        {
            BankCarried();

            found.Clear();
            ClearCheckpoint();
        }

        public static void ResetSave()
        {
            Clear();

            if (SaveFile.Delete(FileName))
                UnblockSaving();

            DeleteLegacyPlayerPrefs();
        }

        public static void BeginSandbox(bool reseed = false)
        {
            if (isSandbox && IsSandboxSeeded && !reseed)
                return;

            Clear();
            isSandbox = true;
            IsSandboxSeeded = true;
        }

        static void PayWisps(int cost)
        {
            if (!IsSpendingFree)
                Wisps -= cost;
        }

        static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        static void ClearSlotsHolding(string key)
        {
            for (int i = 0; i < SlotCount; i++)
                if (equipped[i] == key)
                    equipped[i] = string.Empty;
        }

        static void SwapSlots(int first, int second)
        {
            string firstKey = equipped[first];
            equipped[first] = equipped[second];
            equipped[second] = firstKey;
        }

        static void Save()
        {
            if (isSandbox)
                return;

            if (isSaveFileUnreadable)
            {
                WarnOnceAboutNotSaving();
                return;
            }

            SaveFile.Write(FileName, Pack());
        }

        static void WarnOnceAboutNotSaving()
        {
            if (hasWarnedAboutUnreadable)
                return;

            hasWarnedAboutUnreadable = true;
            Debug.LogWarning($"Not saving: the save file at {SaveFile.PathFor(FileName)} " +
                             "could not be read when the game started, and writing a fresh " +
                             "one over it would throw away whatever is in there. Nothing " +
                             "earned this session will be kept. Relaunch once whatever is " +
                             "holding that file has let go of it.");
        }

        static SaveData Pack()
        {
            var data = new SaveData
            {
                version = FormatVersion,
                wisps = Wisps,
                bonusHearts = BonusHearts,
                loadout = new string[SlotCount],
            };

            for (int i = 0; i < SlotCount; i++)
                data.loadout[i] = equipped[i] ?? string.Empty;

            data.ranks = PackRanks();
            data.spent = MakeSortedCopy(spent);
            data.completedLevels = MakeSortedCopy(completedLevels);

            return data;
        }

        static List<RankEntry> PackRanks()
        {
            List<string> keys = MakeSortedCopy(ranks.Keys);
            var entries = new List<RankEntry>(keys.Count);

            foreach (string key in keys)
                entries.Add(new RankEntry { key = key, rank = ranks[key] });

            return entries;
        }

        static List<string> MakeSortedCopy(IEnumerable<string> values)
        {
            var sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);
            return sorted;
        }

        static void Load()
        {
            SaveFile.Announce();

            UnblockSaving();

            switch (SaveFile.Read(FileName, out SaveData data))
            {
                case SaveRead.Loaded:
                    Unpack(data);
                    return;

                case SaveRead.Unreadable:
                    isSaveFileUnreadable = true;
                    return;
            }

            if (!TryImportLegacyPlayerPrefs())
                Unpack(null);
        }

        static void UnblockSaving()
        {
            isSaveFileUnreadable = false;
            hasWarnedAboutUnreadable = false;
        }

        static void Unpack(SaveData data)
        {
            if (data == null)
                data = new SaveData();

            Wisps = Mathf.Max(0, data.wisps);
            BonusHearts = Mathf.Max(0, data.bonusHearts);

            UnpackRanks(data.ranks);
            FillLoadout(data.loadout);
            Refill(spent, data.spent);
            Refill(completedLevels, data.completedLevels);
        }

        static void UnpackRanks(List<RankEntry> entries)
        {
            ranks.Clear();

            if (entries == null)
                return;

            foreach (RankEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key))
                    continue;

                ranks[entry.key] = Mathf.Max(1, entry.rank);
            }
        }

        static void FillLoadout(string[] keys)
        {
            for (int i = 0; i < SlotCount; i++)
                equipped[i] = keys != null && i < keys.Length && keys[i] != null
                    ? keys[i]
                    : string.Empty;
        }

        static void Refill(HashSet<string> target, List<string> values)
        {
            target.Clear();

            if (values == null)
                return;

            foreach (string value in values)
                if (!string.IsNullOrEmpty(value))
                    target.Add(value);
        }

        static bool TryImportLegacyPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LegacyWispsKey))
                return false;

            ReadLegacyPlayerPrefs();

            if (!SaveFile.Write(FileName, Pack()))
            {
                Debug.LogWarning("Old progress was found in PlayerPrefs but could not be written to " +
                                 $"{SaveFile.PathFor(FileName)}. It has been left where it is and " +
                                 "the import will be tried again on the next launch.");
                return true;
            }

            DeleteLegacyPlayerPrefs();

            Debug.Log($"Progress saved by an older build was imported into {SaveFile.PathFor(FileName)} " +
                      "and the PlayerPrefs it came from have been deleted.");
            return true;
        }

        static void ReadLegacyPlayerPrefs()
        {
            Wisps = PlayerPrefs.GetInt(LegacyWispsKey, 0);
            BonusHearts = PlayerPrefs.GetInt(LegacyHeartsKey, 0);

            ReadLegacyRanks();
            FillLoadout(PlayerPrefs.GetString(LegacyLoadoutKey, string.Empty).Split(LegacyListSeparator));
            Refill(spent, SplitLegacy(PlayerPrefs.GetString(LegacySpentKey, string.Empty)));
        }

        static void ReadLegacyRanks()
        {
            ranks.Clear();

            foreach (string entry in SplitLegacy(PlayerPrefs.GetString(LegacyRanksKey, string.Empty)))
            {
                int separatorIndex = entry.LastIndexOf(LegacyRankSeparator);

                if (separatorIndex <= 0 || !int.TryParse(entry.Substring(separatorIndex + 1), out int rank))
                    continue;

                ranks[entry.Substring(0, separatorIndex)] = Mathf.Max(1, rank);
            }
        }

        static List<string> SplitLegacy(string packed)
        {
            var pieces = new List<string>();

            foreach (string piece in packed.Split(LegacyListSeparator))
                if (!string.IsNullOrEmpty(piece))
                    pieces.Add(piece);

            return pieces;
        }

        static void DeleteLegacyPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(LegacyWispsKey);
            PlayerPrefs.DeleteKey(LegacyRanksKey);
            PlayerPrefs.DeleteKey(LegacyLoadoutKey);
            PlayerPrefs.DeleteKey(LegacySpentKey);
            PlayerPrefs.DeleteKey(LegacyHeartsKey);
            PlayerPrefs.Save();
        }

        static void Clear()
        {
            ranks.Clear();
            found.Clear();
            carried.Clear();
            spent.Clear();
            completedLevels.Clear();

            for (int i = 0; i < SlotCount; i++)
                equipped[i] = string.Empty;

            Wisps = 0;
            CarriedWisps = 0;
            BonusHearts = 0;
            isSandbox = false;
            IsSandboxSeeded = false;

            ClearCheckpoint();
        }

        [Serializable]
        class SaveData
        {
            public int version;
            public int wisps;
            public int bonusHearts;
            public string[] loadout;
            public List<RankEntry> ranks;
            public List<string> spent;
            public List<string> completedLevels;
        }

        [Serializable]
        class RankEntry
        {
            public string key;
            public int rank;
        }
    }
}
