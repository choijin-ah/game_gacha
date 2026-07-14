using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class GachaHistoryEntry
    {
        [SerializeField] string entryId;
        [SerializeField] string bannerId;
        [SerializeField] string characterId;
        [SerializeField] string characterName;
        [SerializeField] int rarity;
        [SerializeField] bool featured;
        [SerializeField] bool newCharacter;
        [SerializeField] int pityBefore;
        [SerializeField] int pityAfter;
        [SerializeField] long pulledAtUtcTicks;

        public GachaHistoryEntry(string bannerId, CharacterData character, int rarity,
            bool featured, bool newCharacter, int pityBefore, int pityAfter, DateTime pulledAtUtc)
        {
            entryId = Guid.NewGuid().ToString("N");
            this.bannerId = bannerId ?? string.Empty;
            characterId = character != null ? character.Id : string.Empty;
            characterName = character != null ? character.DisplayName : string.Empty;
            this.rarity = Mathf.Max(0, rarity);
            this.featured = featured;
            this.newCharacter = newCharacter;
            this.pityBefore = Mathf.Max(0, pityBefore);
            this.pityAfter = Mathf.Max(0, pityAfter);
            pulledAtUtcTicks = ScheduleRange.NormalizeUtc(pulledAtUtc).Ticks;
        }

        public string EntryId => entryId ?? string.Empty;
        public string BannerId => bannerId ?? string.Empty;
        public string CharacterId => characterId ?? string.Empty;
        public string CharacterName => characterName ?? string.Empty;
        public int Rarity => Mathf.Max(0, rarity);
        public bool IsFeatured => featured;
        public bool IsNew => newCharacter;
        public int PityBefore => Mathf.Max(0, pityBefore);
        public int PityAfter => Mathf.Max(0, pityAfter);
        public DateTime PulledAtUtc => pulledAtUtcTicks >= DateTime.MinValue.Ticks
            && pulledAtUtcTicks <= DateTime.MaxValue.Ticks
                ? new DateTime(pulledAtUtcTicks, DateTimeKind.Utc)
                : DateTime.MinValue;
    }

    public static class GachaHistoryService
    {
        public const string PlayerPrefsKey = "StarfallAcademy.Gacha.History.v1";
        public const int MaximumEntries = 200;

        [Serializable]
        sealed class HistoryStore
        {
            public int version = 1;
            public List<GachaHistoryEntry> entries = new List<GachaHistoryEntry>();
        }

        public static IReadOnlyList<GachaHistoryEntry> Load()
        {
            HistoryStore store = ReadStore();
            return store.entries.ToArray();
        }

        public static void RecordPull(string bannerId, IReadOnlyList<GachaResult> results,
            DateTime pulledAtUtc)
        {
            if (results == null || results.Count == 0) return;
            try
            {
                MetaStringWrite write = CreateRecordWrite(bannerId, results, pulledAtUtc);
                if (!MetaPlayerPrefsTransaction.Commit(Array.Empty<MetaIntWrite>(),
                    new[] { write }))
                    throw new InvalidOperationException("The pull history transaction was rejected.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Starfall Gacha] Failed to save pull history: "
                    + exception.Message);
            }
        }

        internal static MetaStringWrite CreateRecordWrite(string bannerId,
            IReadOnlyList<GachaResult> results, DateTime pulledAtUtc)
        {
            HistoryStore store = ReadStore();
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    GachaResult result = results[i];
                    if (result == null) continue;
                    store.entries.Insert(0, new GachaHistoryEntry(bannerId,
                        result.Character, result.Rarity, result.IsFeatured, result.IsNew,
                        result.PityBefore, result.PityAfter, pulledAtUtc));
                }
            }
            if (store.entries.Count > MaximumEntries)
                store.entries.RemoveRange(MaximumEntries,
                    store.entries.Count - MaximumEntries);
            return new MetaStringWrite(PlayerPrefsKey, JsonUtility.ToJson(store));
        }

        public static string ExportJson(bool prettyPrint = true) =>
            JsonUtility.ToJson(ReadStore(), prettyPrint);

        public static bool TryImportJson(string json, out string error)
        {
            try
            {
                HistoryStore imported = string.IsNullOrWhiteSpace(json)
                    ? null : JsonUtility.FromJson<HistoryStore>(json);
                if (imported == null || imported.entries == null)
                {
                    error = "The gacha history JSON is invalid.";
                    return false;
                }
                imported.version = 1;
                imported.entries.RemoveAll(entry => entry == null);
                if (imported.entries.Count > MaximumEntries)
                    imported.entries.RemoveRange(MaximumEntries,
                        imported.entries.Count - MaximumEntries);
                WriteStore(imported);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static void Clear()
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
            PlayerPrefs.Save();
        }

        static HistoryStore ReadStore()
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return new HistoryStore();
            try
            {
                HistoryStore store = JsonUtility.FromJson<HistoryStore>(json);
                if (store == null || store.entries == null) return new HistoryStore();
                store.entries.RemoveAll(entry => entry == null);
                return store;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Starfall Gacha] Ignoring invalid pull history: "
                    + exception.Message);
                return new HistoryStore();
            }
        }

        static void WriteStore(HistoryStore store)
        {
            var write = new MetaStringWrite(PlayerPrefsKey, JsonUtility.ToJson(store));
            if (!MetaPlayerPrefsTransaction.Commit(Array.Empty<MetaIntWrite>(),
                new[] { write }))
                throw new InvalidOperationException("The gacha history transaction was rejected.");
        }
    }
}
