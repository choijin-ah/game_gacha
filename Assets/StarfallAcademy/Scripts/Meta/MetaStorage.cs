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

        public bool HasKey(string key) => PlayerPrefs.HasKey(key);

        public int GetInt(string key, int defaultValue = 0) => PlayerPrefs.GetInt(key, defaultValue);

        public string GetString(string key, string defaultValue = "") =>
            PlayerPrefs.GetString(key, defaultValue);

        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);

        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);

        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);

        public void Save() => PlayerPrefs.Save();
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
}
