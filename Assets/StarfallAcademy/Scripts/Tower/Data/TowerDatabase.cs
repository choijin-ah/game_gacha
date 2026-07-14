using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class TowerStarRewardTier
    {
        [SerializeField, Min(1)] int requiredTotalStars = 3;
        [SerializeField] RewardPackage reward = new RewardPackage();
        public int RequiredTotalStars => Mathf.Max(1, requiredTotalStars);
        public RewardPackage Reward => reward;
    }

    [CreateAssetMenu(fileName = "TowerDatabase", menuName = "Starfall/Challenge Tower Database")]
    public sealed class TowerDatabase : ScriptableObject
    {
        [SerializeField] List<TowerFloorData> floors = new List<TowerFloorData>();
        [SerializeField] List<TowerStarRewardTier> cumulativeStarRewards = new List<TowerStarRewardTier>();
        public IReadOnlyList<TowerFloorData> Floors => floors;
        public IReadOnlyList<TowerStarRewardTier> CumulativeStarRewards => cumulativeStarRewards;

        public TowerFloorData FindFloor(int floorNumber)
        {
            for (int i = 0; i < floors.Count; i++)
                if (floors[i] != null && floors[i].FloorNumber == floorNumber) return floors[i];
            return null;
        }

#if UNITY_EDITOR
        public void Add(TowerFloorData floor)
        {
            if (floor != null && !floors.Contains(floor)) floors.Add(floor);
        }
#endif
    }
}
