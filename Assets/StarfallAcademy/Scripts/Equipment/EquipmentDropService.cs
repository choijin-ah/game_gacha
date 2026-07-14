using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class EquipmentDropService
    {
        readonly EquipmentInventoryService inventory;

        public EquipmentDropService(EquipmentInventoryService inventory = null)
        {
            this.inventory = inventory ?? EquipmentInventoryService.Default;
        }

        public IReadOnlyList<EquipmentDefinition> Roll(EquipmentDropTable table,
            int? deterministicSeed = null)
        {
            if (table == null || table.TotalWeight <= 0f)
                return Array.Empty<EquipmentDefinition>();
            var random = deterministicSeed.HasValue
                ? new System.Random(deterministicSeed.Value) : new System.Random();
            int count = table.MaximumDrops <= table.MinimumDrops ? table.MinimumDrops
                : random.Next(table.MinimumDrops, table.MaximumDrops + 1);
            var results = new List<EquipmentDefinition>(count);
            for (int i = 0; i < count; i++)
            {
                EquipmentDefinition drop = table.Roll(random);
                if (drop != null) results.Add(drop);
            }
            return results;
        }

        public IReadOnlyList<EquipmentInstance> Grant(string transactionId,
            EquipmentDropTable table, int? deterministicSeed = null)
        {
            IReadOnlyList<EquipmentDefinition> rolled = Roll(table, deterministicSeed);
            EquipmentInventoryStorageSnapshot snapshot = inventory.CaptureStorageSnapshot();
            try
            {
                IReadOnlyList<EquipmentInstance> granted = StageGrant(transactionId, rolled);
                inventory.CommitStaged();
                return granted;
            }
            catch
            {
                try
                {
                    inventory.RestoreStorageSnapshot(snapshot);
                    inventory.CommitStaged();
                }
                catch (Exception) { }
                throw;
            }
        }

        internal IReadOnlyList<EquipmentInstance> StageGrant(string transactionId,
            IReadOnlyList<EquipmentDefinition> rolled)
        {
            if (rolled == null || rolled.Count == 0)
                return Array.Empty<EquipmentInstance>();

            var additions = new List<EquipmentInstance>(rolled.Count);
            for (int i = 0; i < rolled.Count; i++)
            {
                EquipmentDefinition definition = rolled[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                string instanceId = StableInstanceId(transactionId, i);
                additions.Add(new EquipmentInstance
                {
                    instanceId = instanceId,
                    equipmentId = definition.Id,
                    level = 1,
                    equippedCharacterId = string.Empty
                });
            }
            return inventory.StageAddRange(additions);
        }

        static string StableInstanceId(string transactionId, int index)
        {
            string value = (transactionId ?? "equipment_drop") + ":" + index;
            return "drop_" + StableHash(value).ToString("x8") + "_" + index;
        }

        static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return hash;
            }
        }
    }
}
