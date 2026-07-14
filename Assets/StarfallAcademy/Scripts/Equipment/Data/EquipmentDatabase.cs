using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "EquipmentDatabase", menuName = "Starfall/Equipment/Database")]
    public sealed class EquipmentDatabase : ScriptableObject
    {
        [SerializeField] List<EquipmentDefinition> equipment = new List<EquipmentDefinition>();
        [SerializeField] List<EquipmentSetDefinition> sets = new List<EquipmentSetDefinition>();
        [SerializeField] List<EquipmentDropTable> dropTables = new List<EquipmentDropTable>();

        public IReadOnlyList<EquipmentDefinition> Equipment => equipment
            ?? (IReadOnlyList<EquipmentDefinition>)Array.Empty<EquipmentDefinition>();
        public IReadOnlyList<EquipmentSetDefinition> Sets => sets
            ?? (IReadOnlyList<EquipmentSetDefinition>)Array.Empty<EquipmentSetDefinition>();
        public IReadOnlyList<EquipmentDropTable> DropTables => dropTables
            ?? (IReadOnlyList<EquipmentDropTable>)Array.Empty<EquipmentDropTable>();

        public EquipmentDefinition FindEquipment(string id)
        {
            for (int i = 0; i < Equipment.Count; i++)
                if (Equipment[i] != null && string.Equals(Equipment[i].Id, id,
                    StringComparison.OrdinalIgnoreCase)) return Equipment[i];
            return null;
        }

        public EquipmentSetDefinition FindSet(string id)
        {
            for (int i = 0; i < Sets.Count; i++)
                if (Sets[i] != null && string.Equals(Sets[i].Id, id,
                    StringComparison.OrdinalIgnoreCase)) return Sets[i];
            return null;
        }

        public EquipmentDropTable FindDropTable(string id)
        {
            for (int i = 0; i < DropTables.Count; i++)
                if (DropTables[i] != null && string.Equals(DropTables[i].Id, id,
                    StringComparison.OrdinalIgnoreCase)) return DropTables[i];
            return null;
        }

        public void Add(EquipmentDefinition value)
        {
            equipment ??= new List<EquipmentDefinition>();
            if (value != null && !equipment.Contains(value)) equipment.Add(value);
        }

        public void Add(EquipmentSetDefinition value)
        {
            sets ??= new List<EquipmentSetDefinition>();
            if (value != null && !sets.Contains(value)) sets.Add(value);
        }

        public void Add(EquipmentDropTable value)
        {
            dropTables ??= new List<EquipmentDropTable>();
            if (value != null && !dropTables.Contains(value)) dropTables.Add(value);
        }

        public void Remove(UnityEngine.Object value)
        {
            if (value is EquipmentDefinition definition) equipment?.Remove(definition);
            else if (value is EquipmentSetDefinition set) sets?.Remove(set);
            else if (value is EquipmentDropTable table) dropTables?.Remove(table);
        }
    }
}
