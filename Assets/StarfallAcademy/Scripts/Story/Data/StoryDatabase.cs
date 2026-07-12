using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "StoryDatabase", menuName = "Starfall/Story/Database")]
    public sealed class StoryDatabase : ScriptableObject
    {
        [SerializeField] List<StoryEpisode> episodes = new List<StoryEpisode>();

        public IReadOnlyList<StoryEpisode> Episodes => episodes;

        public StoryEpisode FindEpisode(string episodeId)
        {
            if (string.IsNullOrWhiteSpace(episodeId) || episodes == null) return null;
            return episodes.Find(episode => episode != null && episode.Id == episodeId);
        }

        public IReadOnlyList<StoryEpisode> GetEpisodes(StoryCategory category)
        {
            var result = new List<StoryEpisode>();
            if (episodes == null) return result;
            foreach (StoryEpisode episode in episodes)
                if (episode != null && episode.Category == category)
                    result.Add(episode);
            result.Sort((left, right) =>
            {
                int order = left.SortOrder.CompareTo(right.SortOrder);
                return order != 0 ? order : string.Compare(left.Title, right.Title, System.StringComparison.Ordinal);
            });
            return result;
        }

        public bool AddEpisode(StoryEpisode episode)
        {
            if (episode == null) return false;
            if (episodes == null) episodes = new List<StoryEpisode>();
            if (episodes.Contains(episode)) return false;
            episodes.Add(episode);
            return true;
        }

        public bool RemoveEpisode(StoryEpisode episode)
        {
            return episode != null && episodes != null && episodes.Remove(episode);
        }

        public bool MoveEpisode(int fromIndex, int toIndex)
        {
            if (episodes == null || fromIndex < 0 || fromIndex >= episodes.Count || toIndex < 0 || toIndex >= episodes.Count)
                return false;
            StoryEpisode episode = episodes[fromIndex];
            episodes.RemoveAt(fromIndex);
            episodes.Insert(toIndex, episode);
            return true;
        }

        public void ReplaceEpisodes(IEnumerable<StoryEpisode> newEpisodes)
        {
            episodes = newEpisodes != null ? new List<StoryEpisode>(newEpisodes) : new List<StoryEpisode>();
        }

        void OnValidate()
        {
            if (episodes == null) episodes = new List<StoryEpisode>();
            episodes.RemoveAll(episode => episode == null);
        }
    }
}
