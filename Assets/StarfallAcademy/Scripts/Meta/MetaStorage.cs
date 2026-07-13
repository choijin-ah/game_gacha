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
            MetaPlayerPrefsTransaction.RecoverPending();
            bool hadFirst = PlayerPrefs.HasKey(firstKey);
            bool hadSecond = PlayerPrefs.HasKey(secondKey);
            int previousFirst = PlayerPrefs.GetInt(firstKey, 0);
            int previousSecond = PlayerPrefs.GetInt(secondKey, 0);
            try
            {
                bool committed = MetaPlayerPrefsTransaction.Commit(new[]
                {
                    new MetaIntWrite(firstKey, 314159),
                    new MetaIntWrite(secondKey, 271828)
                });
                if (!committed || PlayerPrefs.GetInt(firstKey, 0) != 314159
                    || PlayerPrefs.GetInt(secondKey, 0) != 271828)
                {
                    error = "The isolated PlayerPrefs journal transaction did not commit both values.";
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

        public static bool Commit(IReadOnlyList<MetaIntWrite> writes)
        {
            if (writes == null || writes.Count == 0) return true;

            lock (SyncRoot)
            {
                Journal journal = null;
                bool journalPersisted = false;
                try
                {
                    RecoverPendingWithoutLock();
                    journal = CreateJournal(writes);
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

        static Journal CreateJournal(IReadOnlyList<MetaIntWrite> writes)
        {
            var valuesByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            var keyOrder = new List<string>();
            for (int i = 0; i < writes.Count; i++)
            {
                MetaIntWrite write = writes[i];
                if (string.IsNullOrEmpty(write.Key)) continue;
                if (!valuesByKey.ContainsKey(write.Key)) keyOrder.Add(write.Key);
                valuesByKey[write.Key] = write.Value;
            }

            var journal = new Journal { transactionId = Guid.NewGuid().ToString("N") };
            for (int i = 0; i < keyOrder.Count; i++)
            {
                string key = keyOrder[i];
                journal.entries.Add(new Entry { key = key, value = valuesByKey[key] });
            }
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
                if (!PlayerPrefs.HasKey(entry.key)
                    || PlayerPrefs.GetInt(entry.key, int.MinValue) != entry.value)
                    return false;
            }
            return true;
        }

        static void Apply(Journal journal)
        {
            for (int i = 0; i < journal.entries.Count; i++)
            {
                Entry entry = journal.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                PlayerPrefs.SetInt(entry.key, entry.value);
            }
        }
    }
}
