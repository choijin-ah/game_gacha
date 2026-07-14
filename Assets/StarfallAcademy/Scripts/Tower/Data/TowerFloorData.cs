using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum TowerModifierType
    {
        EnemyMaxHp,
        EnemyAttack,
        EnemySpeed,
        PlayerDamage,
        ElementDamage,
        Healing
    }

    [Serializable]
    public sealed class TowerModifierDefinition
    {
        [SerializeField] string modifierId = "modifier";
        [SerializeField] TowerModifierType type;
        [SerializeField] BattleElement element = BattleElement.Auto;
        [SerializeField, Range(-.9f, 5f)] float value = .2f;
        [SerializeField] string description;

        public string Id => string.IsNullOrWhiteSpace(modifierId) ? type.ToString() : modifierId.Trim();
        public TowerModifierType Type => type;
        public BattleElement Element => element;
        public float Value => Mathf.Clamp(value, -.9f, 5f);
        public string Description => string.IsNullOrWhiteSpace(description)
            ? type + " " + (Value >= 0f ? "+" : string.Empty) + (Value * 100f).ToString("0") + "%"
            : description;
    }

    public enum TowerStarConditionType
    {
        Clear,
        TurnLimit,
        MaximumDefeatedAllies
    }

    [Serializable]
    public sealed class TowerStarCondition
    {
        [SerializeField] TowerStarConditionType type = TowerStarConditionType.Clear;
        [SerializeField, Min(0)] int threshold;

        public TowerStarConditionType Type => type;
        public int Threshold => Mathf.Max(0, threshold);

        public bool IsMet(BattleResult result)
        {
            if (result == null || !result.IsSuccessful) return false;
            switch (type)
            {
                case TowerStarConditionType.TurnLimit:
                    return result.RegularTurns <= Threshold;
                case TowerStarConditionType.MaximumDefeatedAllies:
                    return result.DefeatedAllies <= Threshold;
                default:
                    return result.EnemiesDefeated;
            }
        }
    }

    [CreateAssetMenu(fileName = "TowerFloor", menuName = "Starfall/Tower Floor")]
    public sealed class TowerFloorData : ScriptableObject
    {
        [SerializeField, Min(1)] int floorNumber = 1;
        [SerializeField] StageData baseStage;
        [SerializeField, Min(0)] int recommendedPowerOverride;
        [SerializeField] List<TowerModifierDefinition> modifiers = new List<TowerModifierDefinition>();
        [SerializeField] RewardPackage firstClearReward = new RewardPackage();
        [SerializeField] List<TowerStarCondition> starConditions = new List<TowerStarCondition>();

        public int FloorNumber => Mathf.Max(1, floorNumber);
        public StageData BaseStage => baseStage;
        public int RecommendedPower => recommendedPowerOverride > 0 ? recommendedPowerOverride
            : baseStage != null ? baseStage.RecommendedPower : 0;
        public IReadOnlyList<TowerModifierDefinition> Modifiers => modifiers;
        public RewardPackage FirstClearReward => firstClearReward;
        public IReadOnlyList<TowerStarCondition> StarConditions => starConditions;

        public int EvaluateStars(BattleResult result)
        {
            if (result == null || !result.IsSuccessful || !result.EnemiesDefeated) return 0;
            if (starConditions == null || starConditions.Count == 0) return 1;
            int stars = 0;
            for (int i = 0; i < starConditions.Count; i++)
                if (starConditions[i] != null && starConditions[i].IsMet(result)) stars++;
            return Mathf.Clamp(stars, 1, 3);
        }

        void OnValidate()
        {
            floorNumber = Mathf.Max(1, floorNumber);
            recommendedPowerOverride = Mathf.Max(0, recommendedPowerOverride);
            if (modifiers == null) modifiers = new List<TowerModifierDefinition>();
            if (starConditions == null) starConditions = new List<TowerStarCondition>();
        }
    }
}
