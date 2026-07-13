using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Keeps visual-novel progress independent from the authoring assets.  Story assets can be
    /// replaced or re-imported without erasing what the player has read.
    /// </summary>
    public static class StoryProgressService
    {
        const string LegacyPrefix = "Starfall.Story.v1.";
        const string Prefix = "Starfall.Story.v2.";
        static bool dirty;

        public static bool IsUnlocked(StoryDatabase database, StoryEpisode episode)
        {
            if (episode == null) return false;
            if (GetInt("unlocked", episode.Id, 0) == 1) return true;
            if (!string.IsNullOrWhiteSpace(episode.UnlockKey))
                return GetInt("condition", episode.UnlockKey, 0) == 1;
            if (episode.IsInitiallyUnlocked) return true;
            if (!string.IsNullOrWhiteSpace(episode.PrerequisiteEpisodeId))
            {
                StoryEpisode prerequisite = database != null
                    ? database.FindEpisode(episode.PrerequisiteEpisodeId) : null;
                return prerequisite != null && IsCompleted(prerequisite);
            }

            // A database made before unlock rules were added must remain usable.  The first
            // episode in each category is therefore available by default.
            StoryEpisode first = FirstInCategory(database, episode.Category);
            return first == episode;
        }

        public static bool IsCompleted(StoryEpisode episode)
        {
            return episode != null && GetInt("completed", episode.Id, 0) == 1;
        }

        public static int GetLastLine(StoryEpisode episode)
        {
            if (episode == null || episode.Lines == null || episode.Lines.Count == 0) return 0;
            int legacyLine = GetLegacyInt("line", episode.Id, 0);
            return Mathf.Clamp(GetInt("cursor", episode.Id, legacyLine, "line"), 0, episode.Lines.Count - 1);
        }

        public static float GetReadProgress(StoryEpisode episode)
        {
            if (episode == null || episode.Lines == null || episode.Lines.Count == 0) return 0f;
            if (IsCompleted(episode)) return 1f;
            bool hasLegacyProgress = PlayerPrefs.HasKey(LegacyKey("line", episode.Id));
            bool hasCurrentProgress = PlayerPrefs.HasKey(Key("furthest", episode.Id));
            if (!hasLegacyProgress && !hasCurrentProgress) return 0f;
            int legacyLine = GetLegacyInt("line", episode.Id, 0);
            int furthest = Mathf.Clamp(GetInt("furthest", episode.Id, legacyLine, "line"),
                0, episode.Lines.Count - 1);
            return Mathf.Clamp01((furthest + 1f) / episode.Lines.Count);
        }

        public static void SaveLine(StoryEpisode episode, int lineIndex)
        {
            if (episode == null) return;
            int cursor = Mathf.Max(0, lineIndex);
            int previousFurthest = GetInt("furthest", episode.Id,
                GetLegacyInt("line", episode.Id, 0), "line");
            SetInt("cursor", episode.Id, cursor, "line");
            SetInt("furthest", episode.Id, Mathf.Max(previousFurthest, cursor));
            SetInt("unlocked", episode.Id, 1);
        }

        public static void MarkCompleted(StoryDatabase database, StoryEpisode episode)
        {
            if (episode == null) return;
            SetInt("completed", episode.Id, 1);
            SetInt("unlocked", episode.Id, 1);

            StoryEpisode next = NextInCategory(database, episode);
            // Explicit gates must never be bypassed by simple sequential completion.
            if (next != null && string.IsNullOrWhiteSpace(next.UnlockKey)
                && string.IsNullOrWhiteSpace(next.PrerequisiteEpisodeId))
                SetInt("unlocked", next.Id, 1);
            Flush();
        }

        public static void SetUnlocked(StoryEpisode episode, bool unlocked = true)
        {
            if (episode == null) return;
            SetInt("unlocked", episode.Id, unlocked ? 1 : 0);
            Flush();
        }

        public static void SetCondition(string conditionKey, bool value = true)
        {
            if (string.IsNullOrWhiteSpace(conditionKey)) return;
            SetInt("condition", conditionKey, value ? 1 : 0);
            Flush();
        }

        public static bool IsChoiceAvailable(StoryChoice choice)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.ConditionKey)) return true;
            return GetInt("condition", choice.ConditionKey, 0) == 1;
        }

        /// <summary>
        /// Persists pending story progress. Line changes intentionally stay in memory until a
        /// checkpoint, pause, completion, or close so skip mode cannot synchronously write every frame.
        /// </summary>
        public static void Flush()
        {
            if (!dirty) return;
            PlayerPrefs.Save();
            dirty = false;
        }

        static StoryEpisode FirstInCategory(StoryDatabase database, StoryCategory category)
        {
            if (database == null) return null;
            StoryEpisode first = null;
            foreach (StoryEpisode candidate in database.GetEpisodes(category))
            {
                if (candidate == null) continue;
                if (first == null || candidate.SortOrder < first.SortOrder) first = candidate;
            }
            return first;
        }

        static StoryEpisode NextInCategory(StoryDatabase database, StoryEpisode episode)
        {
            if (database == null || episode == null) return null;
            StoryEpisode next = null;
            foreach (StoryEpisode candidate in database.GetEpisodes(episode.Category))
            {
                if (candidate == null || candidate == episode || candidate.SortOrder <= episode.SortOrder) continue;
                if (next == null || candidate.SortOrder < next.SortOrder) next = candidate;
            }
            return next;
        }

        static string Key(string type, string episodeId)
        {
            return Prefix + type + "." + Safe(episodeId);
        }

        static string Safe(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? "untitled" : value.Trim();
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(normalized))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        static string LegacyKey(string type, string value)
        {
            return LegacyPrefix + type + "." + LegacySafe(value);
        }

        static string LegacySafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "untitled";
            return value.Trim().Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }

        static int GetInt(string type, string value, int defaultValue, string legacyType = null)
        {
            string key = Key(type, value);
            if (PlayerPrefs.HasKey(key)) return PlayerPrefs.GetInt(key, defaultValue);

            string oldKey = LegacyKey(legacyType ?? type, value);
            if (!PlayerPrefs.HasKey(oldKey)) return defaultValue;
            int migrated = PlayerPrefs.GetInt(oldKey, defaultValue);
            PlayerPrefs.SetInt(key, migrated);
            dirty = true;
            return migrated;
        }

        static int GetLegacyInt(string type, string value, int defaultValue)
        {
            return PlayerPrefs.GetInt(LegacyKey(type, value), defaultValue);
        }

        static void SetInt(string type, string value, int newValue, string legacyType = null)
        {
            string key = Key(type, value);
            if (!PlayerPrefs.HasKey(key) || PlayerPrefs.GetInt(key) != newValue)
            {
                PlayerPrefs.SetInt(key, newValue);
                dirty = true;
            }

            if (legacyType != null) SetLegacyInt(legacyType, value, newValue);
            else if (type == "completed" || type == "unlocked" || type == "condition")
                SetLegacyInt(type, value, newValue);
        }

        static void SetLegacyInt(string type, string value, int newValue)
        {
            string key = LegacyKey(type, value);
            if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == newValue) return;
            PlayerPrefs.SetInt(key, newValue);
            dirty = true;
        }
    }
}
