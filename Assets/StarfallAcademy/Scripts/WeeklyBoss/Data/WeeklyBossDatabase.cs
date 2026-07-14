using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "WeeklyBossDatabase", menuName = "Starfall/Weekly Boss Database")]
    public sealed class WeeklyBossDatabase : ScriptableObject
    {
        [SerializeField] List<WeeklyBossDefinition> bosses = new List<WeeklyBossDefinition>();
        public IReadOnlyList<WeeklyBossDefinition> Bosses => bosses;

        public WeeklyBossDefinition Find(string id)
        {
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i] != null && bosses[i].Id == id) return bosses[i];
            return null;
        }

        public WeeklyBossDefinition FindActive(System.DateTime utcNow)
        {
            for (int i = 0; i < bosses.Count; i++)
                if (bosses[i] != null && bosses[i].IsAvailable(utcNow)) return bosses[i];
            return null;
        }

#if UNITY_EDITOR
        public void Add(WeeklyBossDefinition definition)
        {
            if (definition != null && !bosses.Contains(definition)) bosses.Add(definition);
        }
#endif
    }
}
