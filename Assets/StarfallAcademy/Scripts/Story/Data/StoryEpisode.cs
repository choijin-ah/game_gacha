using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "StoryEpisode", menuName = "Starfall/Story/Episode")]
    public sealed class StoryEpisode : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string episodeId;
        [SerializeField] string title = "새 에피소드";
        [SerializeField] StoryCategory category;
        [SerializeField] int sortOrder;

        [Header("Archive Presentation")]
        [SerializeField] Sprite thumbnail;
        [SerializeField] Sprite banner;
        [SerializeField] CharacterData focusCharacter;
        [SerializeField, TextArea(2, 5)] string summary;

        [Header("Unlock")]
        [SerializeField] bool initiallyUnlocked;
        [SerializeField] string unlockKey;
        [SerializeField] string prerequisiteEpisodeId;

        [Header("Visual Novel Lines")]
        [SerializeField] List<StoryLine> lines = new List<StoryLine>();

        public string Id { get => string.IsNullOrWhiteSpace(episodeId) ? name : episodeId; set => episodeId = value ?? string.Empty; }
        public string Title { get => string.IsNullOrWhiteSpace(title) ? name : title; set => title = value ?? string.Empty; }
        public StoryCategory Category { get => category; set => category = value; }
        public int SortOrder { get => sortOrder; set => sortOrder = value; }
        public Sprite Thumbnail { get => thumbnail; set => thumbnail = value; }
        public Sprite Banner { get => banner; set => banner = value; }
        public CharacterData FocusCharacter { get => focusCharacter; set => focusCharacter = value; }
        public string Summary { get => summary; set => summary = value ?? string.Empty; }
        public bool IsInitiallyUnlocked { get => initiallyUnlocked; set => initiallyUnlocked = value; }
        public string UnlockKey { get => unlockKey; set => unlockKey = value ?? string.Empty; }
        public string PrerequisiteEpisodeId { get => prerequisiteEpisodeId; set => prerequisiteEpisodeId = value ?? string.Empty; }
        public IReadOnlyList<StoryLine> Lines => lines;

        public StoryLine FindLine(string lineId)
        {
            if (string.IsNullOrWhiteSpace(lineId) || lines == null) return null;
            return lines.Find(line => line != null && line.Id == lineId);
        }

        public void AddLine(StoryLine line)
        {
            if (line == null) return;
            if (lines == null) lines = new List<StoryLine>();
            lines.Add(line);
        }

        public void InsertLine(int index, StoryLine line)
        {
            if (line == null) return;
            if (lines == null) lines = new List<StoryLine>();
            lines.Insert(Mathf.Clamp(index, 0, lines.Count), line);
        }

        public bool RemoveLineAt(int index)
        {
            if (lines == null || index < 0 || index >= lines.Count) return false;
            lines.RemoveAt(index);
            return true;
        }

        public bool MoveLine(int fromIndex, int toIndex)
        {
            if (lines == null || fromIndex < 0 || fromIndex >= lines.Count || toIndex < 0 || toIndex >= lines.Count)
                return false;
            StoryLine line = lines[fromIndex];
            lines.RemoveAt(fromIndex);
            lines.Insert(toIndex, line);
            return true;
        }

        public void ReplaceLines(IEnumerable<StoryLine> newLines)
        {
            lines = newLines != null ? new List<StoryLine>(newLines) : new List<StoryLine>();
        }

        void OnValidate()
        {
            if (lines == null) lines = new List<StoryLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null) lines[i] = new StoryLine();
                if (string.IsNullOrWhiteSpace(lines[i].Id)) lines[i].Id = $"line_{i + 1:000}";
            }
        }
    }
}
