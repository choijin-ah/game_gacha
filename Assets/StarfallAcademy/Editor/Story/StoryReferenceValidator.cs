using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class StoryReferenceValidator
    {
        [MenuItem("Starfall/Validate/Story References")]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:StoryDatabase");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[Starfall Story] No StoryDatabase asset was found.");
                return;
            }

            int errors = 0;
            int warnings = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StoryDatabase database = AssetDatabase.LoadAssetAtPath<StoryDatabase>(path);
                if (database != null) Validate(database, ref errors, ref warnings);
            }

            if (errors == 0 && warnings == 0)
                Debug.Log($"[Starfall Story] Reference validation passed ({guids.Length} database(s)).");
            else
                Debug.LogWarning($"[Starfall Story] Reference validation finished: {errors} error(s), {warnings} warning(s). See previous messages.");
        }

        static void Validate(StoryDatabase database, ref int errors, ref int warnings)
        {
            var episodeIds = new HashSet<string>(StringComparer.Ordinal);
            var legacyKeys = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (StoryEpisode episode in database.Episodes)
            {
                if (episode == null) continue;
                string episodeId = episode.Id;
                if (!episodeIds.Add(episodeId))
                {
                    errors++;
                    Debug.LogError($"[Starfall Story] Duplicate episode ID '{episodeId}' in database '{database.name}'.", database);
                }

                string legacyKey = LegacySafe(episodeId);
                if (legacyKeys.TryGetValue(legacyKey, out string existingId) && existingId != episodeId)
                {
                    warnings++;
                    Debug.LogWarning($"[Starfall Story] Episode IDs '{existingId}' and '{episodeId}' shared the old v1 save key. The v2 save format fixes future collisions, but ambiguous old progress cannot be recovered automatically.", episode);
                }
                else legacyKeys[legacyKey] = episodeId;
            }

            foreach (StoryEpisode episode in database.Episodes)
            {
                if (episode == null) continue;
                if (!string.IsNullOrWhiteSpace(episode.PrerequisiteEpisodeId)
                    && database.FindEpisode(episode.PrerequisiteEpisodeId) == null)
                {
                    errors++;
                    Debug.LogError($"[Starfall Story] Episode '{episode.Id}' references missing prerequisite '{episode.PrerequisiteEpisodeId}'.", episode);
                }

                var lineIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (StoryLine line in episode.Lines)
                {
                    if (line == null) continue;
                    if (string.IsNullOrWhiteSpace(line.Id) || !lineIds.Add(line.Id))
                    {
                        errors++;
                        Debug.LogError($"[Starfall Story] Episode '{episode.Id}' contains an empty or duplicate line ID '{line.Id}'.", episode);
                    }
                }

                foreach (StoryLine line in episode.Lines)
                {
                    if (line == null || line.Choices == null) continue;
                    foreach (StoryChoice choice in line.Choices)
                    {
                        if (choice == null) continue;
                        bool hasEpisodeTarget = !string.IsNullOrWhiteSpace(choice.NextEpisodeId);
                        bool hasLineTarget = !string.IsNullOrWhiteSpace(choice.NextLineId);
                        if (hasEpisodeTarget && database.FindEpisode(choice.NextEpisodeId) == null)
                        {
                            errors++;
                            Debug.LogError($"[Starfall Story] {episode.Id}/{line.Id} choice '{choice.Text}' references missing episode '{choice.NextEpisodeId}'.", episode);
                        }
                        if (!hasEpisodeTarget && hasLineTarget && !lineIds.Contains(choice.NextLineId))
                        {
                            errors++;
                            Debug.LogError($"[Starfall Story] {episode.Id}/{line.Id} choice '{choice.Text}' references missing line '{choice.NextLineId}'.", episode);
                        }
                        if (hasEpisodeTarget && hasLineTarget)
                        {
                            warnings++;
                            Debug.LogWarning($"[Starfall Story] {episode.Id}/{line.Id} choice '{choice.Text}' has both targets; runtime will use the episode target.", episode);
                        }
                    }
                }
            }
        }

        static string LegacySafe(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "untitled";
            return value.Trim().Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }
    }
}
