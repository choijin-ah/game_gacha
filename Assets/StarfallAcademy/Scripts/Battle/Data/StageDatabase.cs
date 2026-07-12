using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "StageDatabase", menuName = "Starfall Academy/Stage Database")]
    public sealed class StageDatabase : ScriptableObject
    {
        [SerializeField] List<StageData> stages = new List<StageData>();
        public IReadOnlyList<StageData> Stages => stages;

        public StageData Find(string id)
        {
            foreach (StageData stage in stages)
                if (stage != null && stage.Id == id) return stage;
            return null;
        }

#if UNITY_EDITOR
        public void Add(StageData stage)
        {
            if (stage != null && !stages.Contains(stage)) stages.Add(stage);
        }
#endif
    }
}
