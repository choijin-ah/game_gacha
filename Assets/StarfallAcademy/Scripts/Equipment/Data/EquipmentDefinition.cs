using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum EquipmentRarity
    {
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum EquipmentStatType
    {
        Attack,
        Defense,
        MaxHp,
        Speed,
        CritChance,
        CritDamage,
        CombatPower
    }

    [CreateAssetMenu(fileName = "Equipment", menuName = "Starfall/Equipment/Definition")]
    public sealed class EquipmentDefinition : ScriptableObject
    {
        [SerializeField] string equipmentId;
        [SerializeField] string displayName = "새 장비";
        [SerializeField] EquipmentSlot slot;
        [SerializeField] EquipmentRarity rarity = EquipmentRarity.Common;
        [SerializeField] Sprite icon;
        [SerializeField] EquipmentSetDefinition set;
        [SerializeField] EquipmentStatType mainStat = EquipmentStatType.Attack;
        [SerializeField, Min(0f)] float baseValue = 10f;
        [SerializeField, Min(0f)] float valuePerLevel = 2f;
        [SerializeField, Min(1)] int maximumLevel = 20;
        [SerializeField, Min(0)] int enhancementBaseCost = 500;
        [SerializeField, Min(0)] int enhancementCostPerLevel = 250;

        public string Id => string.IsNullOrWhiteSpace(equipmentId) ? name : equipmentId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public EquipmentSlot Slot => slot;
        public EquipmentRarity Rarity => rarity;
        public Sprite Icon => icon;
        public EquipmentSetDefinition Set => set;
        public EquipmentStatType MainStat => mainStat;
        public float BaseValue => Mathf.Max(0f, baseValue);
        public float ValuePerLevel => Mathf.Max(0f, valuePerLevel);
        public int MaximumLevel => Mathf.Max(1, maximumLevel);
        public int EnhancementBaseCost => Mathf.Max(0, enhancementBaseCost);
        public int EnhancementCostPerLevel => Mathf.Max(0, enhancementCostPerLevel);

        public float GetValueAtLevel(int level) => BaseValue
            + Mathf.Max(0, Mathf.Clamp(level, 1, MaximumLevel) - 1) * ValuePerLevel;

        public int GetEnhancementCost(int currentLevel)
        {
            int level = Mathf.Clamp(currentLevel, 1, MaximumLevel);
            long cost = (long)EnhancementBaseCost + (long)(level - 1) * EnhancementCostPerLevel;
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        public int EstimateCombatPower(int level)
        {
            float value = GetValueAtLevel(level);
            float factor = mainStat == EquipmentStatType.CombatPower ? 1f
                : mainStat == EquipmentStatType.MaxHp ? .2f
                : mainStat == EquipmentStatType.Speed ? 12f
                : mainStat == EquipmentStatType.CritChance ? 30f
                : mainStat == EquipmentStatType.CritDamage ? 18f : 4f;
            return Mathf.Max(0, Mathf.RoundToInt(value * factor));
        }

        void OnValidate()
        {
            rarity = (EquipmentRarity)Mathf.Clamp((int)rarity, 1, 4);
            baseValue = Mathf.Max(0f, baseValue);
            valuePerLevel = Mathf.Max(0f, valuePerLevel);
            maximumLevel = Mathf.Max(1, maximumLevel);
            enhancementBaseCost = Mathf.Max(0, enhancementBaseCost);
            enhancementCostPerLevel = Mathf.Max(0, enhancementCostPerLevel);
        }
    }
}
