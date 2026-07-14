using System;
using System.Globalization;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum ScheduleState
    {
        Invalid,
        Upcoming,
        Active,
        Ended
    }

    [Serializable]
    public sealed class ScheduleRange
    {
        const string RoundTripFormat = "O";

        [SerializeField, Tooltip("ISO-8601 UTC. Empty means no start limit.")]
        string startUtc;
        [SerializeField, Tooltip("ISO-8601 UTC. Empty means no end limit.")]
        string endUtc;

        public ScheduleRange()
        {
        }

        public ScheduleRange(DateTime? start, DateTime? end)
        {
            Set(start, end);
        }

        public string StartUtcText => startUtc ?? string.Empty;
        public string EndUtcText => endUtc ?? string.Empty;
        public DateTime? StartUtc => Parse(startUtc);
        public DateTime? EndUtc => Parse(endUtc);
        public bool HasStart => !string.IsNullOrWhiteSpace(startUtc);
        public bool HasEnd => !string.IsNullOrWhiteSpace(endUtc);

        public bool IsValid
        {
            get
            {
                if (HasStart && !StartUtc.HasValue) return false;
                if (HasEnd && !EndUtc.HasValue) return false;
                return !StartUtc.HasValue || !EndUtc.HasValue || EndUtc.Value > StartUtc.Value;
            }
        }

        public TimeSpan? Duration => IsValid && StartUtc.HasValue && EndUtc.HasValue
            ? EndUtc.Value - StartUtc.Value : (TimeSpan?)null;

        public ScheduleState GetState(DateTime utcNow)
        {
            if (!IsValid) return ScheduleState.Invalid;
            DateTime now = NormalizeUtc(utcNow);
            if (StartUtc.HasValue && now < StartUtc.Value) return ScheduleState.Upcoming;
            if (EndUtc.HasValue && now >= EndUtc.Value) return ScheduleState.Ended;
            return ScheduleState.Active;
        }

        public bool Contains(DateTime utcNow) => GetState(utcNow) == ScheduleState.Active;

        public bool Overlaps(ScheduleRange other)
        {
            if (other == null || !IsValid || !other.IsValid) return false;
            DateTime startA = StartUtc ?? DateTime.MinValue;
            DateTime endA = EndUtc ?? DateTime.MaxValue;
            DateTime startB = other.StartUtc ?? DateTime.MinValue;
            DateTime endB = other.EndUtc ?? DateTime.MaxValue;
            return startA < endB && startB < endA;
        }

        public void Set(DateTime? start, DateTime? end)
        {
            startUtc = Format(start);
            endUtc = Format(end);
        }

        public static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            if (value.Kind == DateTimeKind.Local) return value.ToUniversalTime();
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        static string Format(DateTime? value) => value.HasValue
            ? NormalizeUtc(value.Value).ToString(RoundTripFormat, CultureInfo.InvariantCulture)
            : string.Empty;

        static DateTime? Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (!DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed)) return null;
            return NormalizeUtc(parsed);
        }
    }

    /// <summary>Single UTC source used by all scheduled content and editor simulations.</summary>
    public static class ContentTime
    {
        const string OverrideKey = "StarfallAcademy.Debug.UtcOverride.v1";

        public static DateTime UtcNow
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                string stored = PlayerPrefs.GetString(OverrideKey, string.Empty);
                if (!string.IsNullOrEmpty(stored)
                    && long.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out long ticks)
                    && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks)
                    return new DateTime(ticks, DateTimeKind.Utc);
#endif
                return DateTime.UtcNow;
            }
        }

        public static bool IsOverridden
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return PlayerPrefs.HasKey(OverrideKey);
#else
                return false;
#endif
            }
        }

        public static bool TrySetOverride(DateTime utc)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DateTime normalized = ScheduleRange.NormalizeUtc(utc);
            PlayerPrefs.SetString(OverrideKey,
                normalized.Ticks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            return true;
#else
            return false;
#endif
        }

        public static void ClearOverride()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PlayerPrefs.DeleteKey(OverrideKey);
            PlayerPrefs.Save();
#endif
        }
    }

    public sealed class ContentUtcClock : IUtcClock
    {
        public static ContentUtcClock Shared { get; } = new ContentUtcClock();
        public DateTime UtcNow => ContentTime.UtcNow;
    }
}
