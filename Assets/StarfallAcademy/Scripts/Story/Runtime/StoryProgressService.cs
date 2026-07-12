using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Keeps visual-novel progress independent from the authoring assets.  Story assets can be
    /// replaced or re-imported without erasing what the player has read.
    /// </summary>
    public static class StoryProgressService
    {
        const string Prefix = "Starfall.Story.v1.";

        public static bool IsUnlocked(StoryDatabase database, StoryEpisode episode)
        {
            if (episode == null) return false;
            if (PlayerPrefs.GetInt(Key("unlocked", episode.Id), 0) == 1) return true;
            if (!string.IsNullOrWhiteSpace(episode.UnlockKey))
                return PlayerPrefs.GetInt(Prefix + "condition." + Safe(episode.UnlockKey), 0) == 1;
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
            return episode != null && PlayerPrefs.GetInt(Key("completed", episode.Id), 0) == 1;
        }

        public static int GetLastLine(StoryEpisode episode)
        {
            if (episode == null || episode.Lines == null || episode.Lines.Count == 0) return 0;
            return Mathf.Clamp(PlayerPrefs.GetInt(Key("line", episode.Id), 0), 0, episode.Lines.Count - 1);
        }

        public static float GetReadProgress(StoryEpisode episode)
        {
            if (episode == null || episode.Lines == null || episode.Lines.Count == 0) return 0f;
            if (IsCompleted(episode)) return 1f;
            if (!PlayerPrefs.HasKey(Key("line", episode.Id))) return 0f;
            return Mathf.Clamp01((GetLastLine(episode) + 1f) / episode.Lines.Count);
        }

        public static void SaveLine(StoryEpisode episode, int lineIndex)
        {
            if (episode == null) return;
            PlayerPrefs.SetInt(Key("line", episode.Id), Mathf.Max(0, lineIndex));
            PlayerPrefs.SetInt(Key("unlocked", episode.Id), 1);
            PlayerPrefs.Save();
        }

        public static void MarkCompleted(StoryDatabase database, StoryEpisode episode)
        {
            if (episode == null) return;
            PlayerPrefs.SetInt(Key("completed", episode.Id), 1);
            PlayerPrefs.SetInt(Key("unlocked", episode.Id), 1);

            StoryEpisode next = NextInCategory(database, episode);
            // Explicit gates must never be bypassed by simple sequential completion.
            if (next != null && string.IsNullOrWhiteSpace(next.UnlockKey)
                && string.IsNullOrWhiteSpace(next.PrerequisiteEpisodeId))
                PlayerPrefs.SetInt(Key("unlocked", next.Id), 1);
            PlayerPrefs.Save();
        }

        public static void SetUnlocked(StoryEpisode episode, bool unlocked = true)
        {
            if (episode == null) return;
            PlayerPrefs.SetInt(Key("unlocked", episode.Id), unlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void SetCondition(string conditionKey, bool value = true)
        {
            if (string.IsNullOrWhiteSpace(conditionKey)) return;
            PlayerPrefs.SetInt(Prefix + "condition." + Safe(conditionKey), value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool IsChoiceAvailable(StoryChoice choice)
        {
            if (choice == null || string.IsNullOrWhiteSpace(choice.ConditionKey)) return true;
            return PlayerPrefs.GetInt(Prefix + "condition." + Safe(choice.ConditionKey), 0) == 1;
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
            if (string.IsNullOrWhiteSpace(value)) return "untitled";
            return value.Trim().Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
