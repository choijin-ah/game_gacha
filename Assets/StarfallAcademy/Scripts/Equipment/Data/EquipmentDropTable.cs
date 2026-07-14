using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class EquipmentDropEntry
    {
        [SerializeField] EquipmentDefinition equipment;
        [SerializeField, Min(0f)] float weight = 1f;

        public EquipmentDefinition Equipment => equipment;
        public float Weight => Mathf.Max(0f, weight);
    }

    [CreateAssetMenu(fileName = "EquipmentDropTable", menuName = "Starfall/Equipment/Drop Table")]
    public sealed class EquipmentDropTable : ScriptableObject
    {
        [SerializeField] string dropTableId;
        [SerializeField] List<EquipmentDropEntry> candidates = new List<EquipmentDropEntry>();
        [SerializeField, Min(0)] int minimumDrops = 1;
        [SerializeField, Min(0)] int maximumDrops = 1;
        [SerializeField] EquipmentRarity minimumRarity = EquipmentRarity.Common;
        [SerializeField] EquipmentRarity maximumRarity = EquipmentRarity.Legendary;

        public string Id => string.IsNullOrWhiteSpace(dropTableId) ? name : dropTableId.Trim();
        public IReadOnlyList<EquipmentDropEntry> Candidates => candidates
            ?? (IReadOnlyList<EquipmentDropEntry>)Array.Empty<EquipmentDropEntry>();
        public int MinimumDrops => Mathf.Max(0, minimumDrops);
        public int MaximumDrops => Mathf.Max(MinimumDrops, maximumDrops);
        public EquipmentRarity MinimumRarity => minimumRarity;
        public EquipmentRarity MaximumRarity => maximumRarity;
        public float TotalWeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Candidates.Count; i++)
                    if (Candidates[i] != null && IsAllowed(Candidates[i].Equipment))
                        total += Candidates[i].Weight;
                return total;
            }
        }

        public EquipmentDefinition Roll(System.Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            float total = TotalWeight;
            if (total <= 0f) return null;
            double roll = random.NextDouble() * total;
            for (int i = 0; i < Candidates.Count; i++)
            {
                EquipmentDropEntry entry = Candidates[i];
                if (entry == null || !IsAllowed(entry.Equipment) || entry.Weight <= 0f) continue;
                roll -= entry.Weight;
                if (roll <= 0d) return entry.Equipment;
            }
            return null;
        }

        bool IsAllowed(EquipmentDefinition equipment) => equipment != null
            && equipment.Rarity >= MinimumRarity && equipment.Rarity <= MaximumRarity;

        void OnValidate()
        {
            candidates ??= new List<EquipmentDropEntry>();
            minimumDrops = Mathf.Max(0, minimumDrops);
            maximumDrops = Mathf.Max(minimumDrops, maximumDrops);
            if (maximumRarity < minimumRarity) maximumRarity = minimumRarity;
        }
    }
}
