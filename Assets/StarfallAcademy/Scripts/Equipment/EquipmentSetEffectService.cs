using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public static class EquipmentSetEffectService
    {
        public static int EstimateCombatPower(IReadOnlyList<EquipmentInstance> items,
            EquipmentDatabase database)
        {
            if (items == null || database == null) return 0;
            var counts = new Dictionary<EquipmentSetDefinition, int>();
            for (int i = 0; i < items.Count; i++)
            {
                EquipmentDefinition definition = database.FindEquipment(items[i]?.equipmentId);
                if (definition?.Set == null) continue;
                counts.TryGetValue(definition.Set, out int count);
                counts[definition.Set] = count + 1;
            }
            float total = 0f;
            foreach (KeyValuePair<EquipmentSetDefinition, int> pair in counts)
            {
                if (pair.Value >= 2) total += Estimate(pair.Key.TwoPieceEffect);
                if (pair.Value >= 4) total += Estimate(pair.Key.FourPieceEffect);
            }
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public static void Apply(CharacterData character, BattleBaseStats stats,
            EquipmentInventoryService inventory = null, EquipmentDatabase database = null)
        {
            if (character == null || stats == null) return;
            inventory ??= EquipmentInventoryService.Default;
            database ??= UnityEngine.Resources.Load<EquipmentDatabase>("Data/EquipmentDatabase");
            if (database == null) return;
            IReadOnlyList<EquipmentInstance> items = inventory.GetEquipped(character);
            var counts = new Dictionary<EquipmentSetDefinition, int>();
            for (int i = 0; i < items.Count; i++)
            {
                EquipmentDefinition definition = database.FindEquipment(items[i].equipmentId);
                if (definition == null) continue;
                ApplyMainStat(stats, definition.MainStat,
                    definition.GetValueAtLevel(items[i].level), false);
                if (definition.Set == null) continue;
                counts.TryGetValue(definition.Set, out int count);
                counts[definition.Set] = count + 1;
            }
            foreach (KeyValuePair<EquipmentSetDefinition, int> pair in counts)
            {
                if (pair.Value >= 2) ApplyEffect(stats, pair.Key.TwoPieceEffect);
                if (pair.Value >= 4) ApplyEffect(stats, pair.Key.FourPieceEffect);
            }
            stats.Sanitize();
        }

        static float Estimate(EquipmentSetEffect effect) => effect == null ? 0f
            : System.Math.Max(0f, effect.Value) * (effect.Percentage ? 12f : 4f);

        static void ApplyEffect(BattleBaseStats stats, EquipmentSetEffect effect)
        {
            if (effect != null) ApplyMainStat(stats, effect.Stat, effect.Value, effect.Percentage);
        }

        static void ApplyMainStat(BattleBaseStats stats, EquipmentStatType stat, float value,
            bool percentage)
        {
            float multiplier = 1f + value / 100f;
            switch (stat)
            {
                case EquipmentStatType.Attack:
                    if (percentage) stats.Attack *= multiplier; else stats.Attack += value;
                    break;
                case EquipmentStatType.Defense:
                    if (percentage) stats.Defense *= multiplier; else stats.Defense += value;
                    break;
                case EquipmentStatType.MaxHp:
                    if (percentage) stats.MaxHp *= multiplier; else stats.MaxHp += value;
                    break;
                case EquipmentStatType.Speed:
                    if (percentage) stats.Speed *= multiplier; else stats.Speed += value;
                    break;
                case EquipmentStatType.CritChance: stats.CritChance += value / 100f; break;
                case EquipmentStatType.CritDamage: stats.CritDamage += value / 100f; break;
            }
        }
    }
}
