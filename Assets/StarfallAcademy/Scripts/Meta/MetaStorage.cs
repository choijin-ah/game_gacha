using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 메타 서비스가 PlayerPrefs에 직접 묶이지 않도록 하는 최소 저장 계약입니다.
    public interface IMetaStorage
    {
        bool HasKey(string key);
        int GetInt(string key, int defaultValue = 0);
        string GetString(string key, string defaultValue = "");
        void SetInt(string key, int value);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsMetaStorage : IMetaStorage
    {
        public static PlayerPrefsMetaStorage Shared { get; } = new PlayerPrefsMetaStorage();

        public bool HasKey(string key)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            return PlayerPrefs.HasKey(key);
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public string GetString(string key, string defaultValue = "")
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public void SetInt(string key, int value)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.SetInt(key, value);
        }

        public void SetString(string key, string value)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.SetString(key, value ?? string.Empty);
        }

        public void DeleteKey(string key)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.DeleteKey(key);
        }

        public void Save()
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR
        public static bool VerifyTransactionJournal(out string error)
        {
            const string firstKey = "StarfallAcademy.Diagnostics.Transaction.First";
            const string secondKey = "StarfallAcademy.Diagnostics.Transaction.Second";
            const string textKey = "StarfallAcademy.Diagnostics.Transaction.Text";
            MetaPlayerPrefsTransaction.RecoverPending();
            bool hadFirst = PlayerPrefs.HasKey(firstKey);
            bool hadSecond = PlayerPrefs.HasKey(secondKey);
            bool hadText = PlayerPrefs.HasKey(textKey);
            int previousFirst = PlayerPrefs.GetInt(firstKey, 0);
            int previousSecond = PlayerPrefs.GetInt(secondKey, 0);
            string previousText = PlayerPrefs.GetString(textKey, string.Empty);
            try
            {
                bool committed = MetaPlayerPrefsTransaction.Commit(new[]
                {
                    new MetaIntWrite(firstKey, 314159),
                    new MetaIntWrite(secondKey, 271828)
                }, new[]
                {
                    new MetaStringWrite(textKey, "recoverable-history")
                });
                if (!committed || PlayerPrefs.GetInt(firstKey, 0) != 314159
                    || PlayerPrefs.GetInt(secondKey, 0) != 271828
                    || PlayerPrefs.GetString(textKey, string.Empty) != "recoverable-history")
                {
                    error = "The isolated PlayerPrefs journal transaction did not commit all mixed values.";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
            finally
            {
                if (hadFirst) PlayerPrefs.SetInt(firstKey, previousFirst);
                else PlayerPrefs.DeleteKey(firstKey);
                if (hadSecond) PlayerPrefs.SetInt(secondKey, previousSecond);
                else PlayerPrefs.DeleteKey(secondKey);
                if (hadText) PlayerPrefs.SetString(textKey, previousText);
                else PlayerPrefs.DeleteKey(textKey);
                PlayerPrefs.Save();
            }
        }
#endif
    }

    // 에디터 진단과 단위 테스트에서 실제 사용자 저장값을 건드리지 않는 저장소입니다.
    public sealed class InMemoryMetaStorage : IMetaStorage
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();

        public int Count => values.Count;
        public int SaveCount { get; private set; }

        public bool HasKey(string key) => values.ContainsKey(RequireKey(key));

        public int GetInt(string key, int defaultValue = 0)
        {
            if (!values.TryGetValue(RequireKey(key), out string value)) return defaultValue;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : defaultValue;
        }

        public string GetString(string key, string defaultValue = "")
        {
            return values.TryGetValue(RequireKey(key), out string value) ? value : defaultValue;
        }

        public void SetInt(string key, int value)
        {
            values[RequireKey(key)] = value.ToString(CultureInfo.InvariantCulture);
        }

        public void SetString(string key, string value)
        {
            values[RequireKey(key)] = value ?? string.Empty;
        }

        public void DeleteKey(string key) => values.Remove(RequireKey(key));

        public void Save()
        {
            SaveCount++;
        }

        public void Clear()
        {
            values.Clear();
            SaveCount = 0;
        }

        static string RequireKey(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("저장 키가 비어 있습니다.", nameof(key));
            return key;
        }
    }

    /// <summary>
    /// PlayerPrefs cannot enumerate keys on every supported platform. Dynamic player-data
    /// producers therefore keep a prefix-validated manifest so editor backup/reset tools can
    /// discover exactly the keys created by the game without touching unrelated preferences.
    /// Manifest entries are intentionally allowed to outlive a deleted value; stale entries are
    /// harmless and make rollback/recovery safer than accidentally forgetting a committed key.
    /// </summary>
    public static class PlayerDataKeyManifest
    {
        public const string ItemKeysManifest =
            "StarfallAcademy.Meta.KeyManifest.ItemInventory.v1";
        public const string RewardTransactionKeysManifest =
            "StarfallAcademy.Meta.KeyManifest.RewardTransactions.v1";
        public const string ItemKeyPrefix = "StarfallAcademy.Inventory.Item.";
        public const string RewardTransactionKeyPrefix =
            "StarfallAcademy.Meta.Reward.Transaction.";

        static readonly object SyncRoot = new object();

        [Serializable]
        sealed class Manifest
        {
            public int version = 1;
            public List<string> keys = new List<string>();
        }

        public static void TrackItemKey(IMetaStorage storage, string key) =>
            Track(storage, ItemKeysManifest, ItemKeyPrefix, key);

        public static void TrackRewardTransactionKey(IMetaStorage storage, string key) =>
            Track(storage, RewardTransactionKeysManifest, RewardTransactionKeyPrefix, key);

        public static IReadOnlyList<string> GetItemKeys(IMetaStorage storage) =>
            Read(storage, ItemKeysManifest, ItemKeyPrefix);

        public static IReadOnlyList<string> GetRewardTransactionKeys(IMetaStorage storage) =>
            Read(storage, RewardTransactionKeysManifest, RewardTransactionKeyPrefix);

        public static void ReplaceItemKeys(IMetaStorage storage, IEnumerable<string> keys) =>
            Replace(storage, ItemKeysManifest, ItemKeyPrefix, keys);

        public static void ReplaceRewardTransactionKeys(IMetaStorage storage,
            IEnumerable<string> keys) =>
            Replace(storage, RewardTransactionKeysManifest,
                RewardTransactionKeyPrefix, keys);

        static void Track(IMetaStorage storage, string manifestKey, string requiredPrefix,
            string key)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (!IsAllowed(key, requiredPrefix))
                throw new ArgumentException("The dynamic player-data key has an invalid prefix.",
                    nameof(key));

            lock (SyncRoot)
            {
                List<string> keys = ReadMutable(storage, manifestKey, requiredPrefix);
                if (keys.Contains(key)) return;
                keys.Add(key);
                Write(storage, manifestKey, keys);
            }
        }

        static IReadOnlyList<string> Read(IMetaStorage storage, string manifestKey,
            string requiredPrefix)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            lock (SyncRoot)
                return ReadMutable(storage, manifestKey, requiredPrefix).ToArray();
        }

        static void Replace(IMetaStorage storage, string manifestKey, string requiredPrefix,
            IEnumerable<string> keys)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            lock (SyncRoot)
            {
                var filtered = new SortedSet<string>(StringComparer.Ordinal);
                if (keys != null)
                {
                    foreach (string key in keys)
                        if (IsAllowed(key, requiredPrefix)) filtered.Add(key);
                }
                if (filtered.Count == 0) storage.DeleteKey(manifestKey);
                else Write(storage, manifestKey, new List<string>(filtered));
            }
        }

        static List<string> ReadMutable(IMetaStorage storage, string manifestKey,
            string requiredPrefix)
        {
            string json = storage.GetString(manifestKey, string.Empty);
            Manifest manifest = null;
            if (!string.IsNullOrWhiteSpace(json))
            {
                try { manifest = JsonUtility.FromJson<Manifest>(json); }
                catch (Exception) { }
            }

            var result = new SortedSet<string>(StringComparer.Ordinal);
            if (manifest?.keys != null)
            {
                for (int i = 0; i < manifest.keys.Count; i++)
                {
                    string key = manifest.keys[i];
                    if (IsAllowed(key, requiredPrefix)) result.Add(key);
                }
            }
            return new List<string>(result);
        }

        static void Write(IMetaStorage storage, string manifestKey, List<string> keys)
        {
            keys.Sort(StringComparer.Ordinal);
            storage.SetString(manifestKey, JsonUtility.ToJson(new Manifest { keys = keys }));
        }

        static bool IsAllowed(string key, string requiredPrefix) =>
            !string.IsNullOrWhiteSpace(key)
            && key.StartsWith(requiredPrefix, StringComparison.Ordinal);
    }

    public interface IUtcClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemUtcClock : IUtcClock
    {
        public static SystemUtcClock Shared { get; } = new SystemUtcClock();

        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class ManualUtcClock : IUtcClock
    {
        DateTime utcNow;

        public ManualUtcClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow
        {
            get => utcNow;
            set => utcNow = NormalizeUtc(value);
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            utcNow = utcNow.Add(duration);
        }

        static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    internal readonly struct MetaIntWrite
    {
        public MetaIntWrite(string key, int value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A PlayerPrefs key is required.", nameof(key));
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public int Value { get; }
    }

    internal readonly struct MetaStringWrite
    {
        public MetaStringWrite(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A PlayerPrefs key is required.", nameof(key));
            Key = key;
            Value = value ?? string.Empty;
        }

        public string Key { get; }
        public string Value { get; }
    }

    // PlayerPrefs cannot atomically commit multiple keys. This write-ahead journal makes
    // multi-key meta transactions recoverable: once the journal is durable, recovery
    // always rolls the complete transaction forward before any meta value is read.
    internal static class MetaPlayerPrefsTransaction
    {
        const string JournalKey = "StarfallAcademy.Meta.Transaction.Pending.v1";
        static readonly object SyncRoot = new object();
        static bool recovering;

        [Serializable]
        sealed class Journal
        {
            public string transactionId;
            public List<Entry> entries = new List<Entry>();
        }

        [Serializable]
        sealed class Entry
        {
            public string key;
            public int value;
            public bool isString;
            public string stringValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RecoverOnStartup()
        {
            RecoverPending();
        }

        public static void RecoverPending()
        {
            lock (SyncRoot)
            {
                if (recovering) return;
                recovering = true;
                try
                {
                    RecoverPendingWithoutLock();
                }
                finally
                {
                    recovering = false;
                }
            }
        }

        public static bool Commit(IReadOnlyList<MetaIntWrite> writes) =>
            Commit(writes, null);

        public static bool Commit(IReadOnlyList<MetaIntWrite> intWrites,
            IReadOnlyList<MetaStringWrite> stringWrites)
        {
            if ((intWrites == null || intWrites.Count == 0)
                && (stringWrites == null || stringWrites.Count == 0))
                return true;

            lock (SyncRoot)
            {
                Journal journal = null;
                bool journalPersisted = false;
                try
                {
                    RecoverPendingWithoutLock();
                    journal = CreateJournal(intWrites, stringWrites);
                    if (journal.entries.Count == 0) return true;

                    PlayerPrefs.SetString(JournalKey, JsonUtility.ToJson(journal));
                    PlayerPrefs.Save();
                    journalPersisted = true;
                    Apply(journal);
                    PlayerPrefs.Save();
                    if (!MatchesFinalValues(journal))
                        throw new InvalidOperationException("The committed meta values did not match the journal.");
                    PlayerPrefs.DeleteKey(JournalKey);
                    PlayerPrefs.Save();
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Starfall Meta] Failed to commit a PlayerPrefs transaction: " + exception.Message);
                    try
                    {
                        RecoverPendingWithoutLock();
                        if (journal != null && !PlayerPrefs.HasKey(JournalKey)
                            && MatchesFinalValues(journal))
                            return true;
                    }
                    catch (Exception recoveryException)
                    {
                        Debug.LogError("[Starfall Meta] Failed to recover the pending transaction: "
                            + recoveryException.Message);
                    }

                    // Once the write-ahead journal is durable the operation is accepted,
                    // even if this process could not finish applying it. Returning success
                    // prevents a retry from charging twice; startup/read-boundary recovery
                    // will idempotently roll the accepted transaction forward.
                    if (journalPersisted)
                    {
                        Debug.LogWarning("[Starfall Meta] Transaction " + journal.transactionId
                            + " remains pending and will be recovered before the next meta access.");
                        return true;
                    }
                    return false;
                }
            }
        }

        static Journal CreateJournal(IReadOnlyList<MetaIntWrite> intWrites,
            IReadOnlyList<MetaStringWrite> stringWrites)
        {
            var entriesByKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            var keyOrder = new List<string>();
            if (intWrites != null)
            {
                for (int i = 0; i < intWrites.Count; i++)
                {
                    MetaIntWrite write = intWrites[i];
                    if (string.IsNullOrEmpty(write.Key)) continue;
                    if (!entriesByKey.ContainsKey(write.Key)) keyOrder.Add(write.Key);
                    entriesByKey[write.Key] = new Entry
                    {
                        key = write.Key,
                        value = write.Value,
                        isString = false,
                        stringValue = string.Empty
                    };
                }
            }
            if (stringWrites != null)
            {
                for (int i = 0; i < stringWrites.Count; i++)
                {
                    MetaStringWrite write = stringWrites[i];
                    if (string.IsNullOrEmpty(write.Key)) continue;
                    if (!entriesByKey.ContainsKey(write.Key)) keyOrder.Add(write.Key);
                    entriesByKey[write.Key] = new Entry
                    {
                        key = write.Key,
                        isString = true,
                        stringValue = write.Value ?? string.Empty
                    };
                }
            }

            var journal = new Journal { transactionId = Guid.NewGuid().ToString("N") };
            for (int i = 0; i < keyOrder.Count; i++)
                journal.entries.Add(entriesByKey[keyOrder[i]]);
            return journal;
        }

        static void RecoverPendingWithoutLock()
        {
            if (!PlayerPrefs.HasKey(JournalKey)) return;
            string json = PlayerPrefs.GetString(JournalKey, string.Empty);
            Journal journal = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Journal>(json);
            if (journal == null || journal.entries == null)
                throw new InvalidOperationException("The pending meta transaction journal is invalid.");

            Apply(journal);
            PlayerPrefs.Save();
            if (!MatchesFinalValues(journal))
                throw new InvalidOperationException("Recovered meta values did not match the journal.");
            PlayerPrefs.DeleteKey(JournalKey);
            PlayerPrefs.Save();
        }

        static bool MatchesFinalValues(Journal journal)
        {
            if (journal == null || journal.entries == null) return false;
            for (int i = 0; i < journal.entries.Count; i++)
            {
                Entry entry = journal.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                if (!PlayerPrefs.HasKey(entry.key)) return false;
                if (entry.isString)
                {
                    if (PlayerPrefs.GetString(entry.key, string.Empty)
                        != (entry.stringValue ?? string.Empty))
                        return false;
                }
                else if (PlayerPrefs.GetInt(entry.key, int.MinValue) != entry.value)
                {
                    return false;
                }
            }
            return true;
        }

        static void Apply(Journal journal)
        {
            for (int i = 0; i < journal.entries.Count; i++)
            {
                Entry entry = journal.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                if (entry.isString)
                    PlayerPrefs.SetString(entry.key, entry.stringValue ?? string.Empty);
                else
                    PlayerPrefs.SetInt(entry.key, entry.value);
            }
        }
    }
}
