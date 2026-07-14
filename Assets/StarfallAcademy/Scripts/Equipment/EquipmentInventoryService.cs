using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class EquipmentInstance
    {
        public string instanceId;
        public string equipmentId;
        public int level = 1;
        public string equippedCharacterId;

        public EquipmentInstance Clone() => new EquipmentInstance
        {
            instanceId = instanceId,
            equipmentId = equipmentId,
            level = level,
            equippedCharacterId = equippedCharacterId
        };
    }

    [Serializable]
    sealed class EquipmentInventoryPayload
    {
        public int version = 1;
        public List<EquipmentInstance> items = new List<EquipmentInstance>();
    }

    internal readonly struct EquipmentInventoryStorageSnapshot
    {
        public EquipmentInventoryStorageSnapshot(bool hadValue, string json)
        {
            HadValue = hadValue;
            Json = json ?? string.Empty;
        }

        public bool HadValue { get; }
        public string Json { get; }
    }

    public sealed class EquipmentInventoryService
    {
        public const string StorageKey = "StarfallAcademy.Equipment.Inventory.v1";
        readonly IMetaStorage storage;

        public EquipmentInventoryService(IMetaStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public static EquipmentInventoryService Default { get; } =
            new EquipmentInventoryService(PlayerPrefsMetaStorage.Shared);

        public IReadOnlyList<EquipmentInstance> GetAll()
        {
            EquipmentInventoryPayload payload = Load();
            return payload.items.Select(item => item.Clone()).ToArray();
        }

        public EquipmentInstance Add(string equipmentId, int level = 1, string instanceId = null)
        {
            if (string.IsNullOrWhiteSpace(equipmentId)) return null;
            EquipmentInventoryPayload payload = Load();
            string id = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId.Trim();
            EquipmentInstance existing = payload.items.Find(item => item != null && item.instanceId == id);
            if (existing != null) return existing.Clone();
            var item = new EquipmentInstance
            {
                instanceId = id,
                equipmentId = equipmentId.Trim(),
                level = Mathf.Max(1, level),
                equippedCharacterId = string.Empty
            };
            payload.items.Add(item);
            Save(payload);
            return item.Clone();
        }

        public bool Equip(string instanceId, CharacterData character, EquipmentDatabase database,
            out string message)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || character == null || database == null)
            {
                message = "장비와 캐릭터를 선택해 주세요.";
                return false;
            }
            EquipmentInventoryPayload payload = Load();
            EquipmentInstance item = payload.items.Find(value => value != null && value.instanceId == instanceId);
            EquipmentDefinition definition = item == null ? null : database.FindEquipment(item.equipmentId);
            if (item == null || definition == null)
            {
                message = "장비 정의를 찾을 수 없습니다.";
                return false;
            }
            for (int i = 0; i < payload.items.Count; i++)
            {
                EquipmentInstance other = payload.items[i];
                EquipmentDefinition otherDefinition = other == null ? null : database.FindEquipment(other.equipmentId);
                if (other != null && otherDefinition != null
                    && other.equippedCharacterId == character.Id
                    && otherDefinition.Slot == definition.Slot)
                    other.equippedCharacterId = string.Empty;
            }
            item.equippedCharacterId = character.Id;
            Save(payload);
            message = character.DisplayName + "에게 " + definition.DisplayName + " 장착";
            return true;
        }

        public IReadOnlyList<EquipmentInstance> GetEquipped(CharacterData character)
        {
            if (character == null) return Array.Empty<EquipmentInstance>();
            return GetAll().Where(item => item != null && item.equippedCharacterId == character.Id).ToArray();
        }

        public bool HasEquippedItems(CharacterData character) => GetEquipped(character).Count > 0;

        public int GetCombatPowerBonus(CharacterData character, EquipmentDatabase database = null)
        {
            if (character == null) return 0;
            database ??= Resources.Load<EquipmentDatabase>("Data/EquipmentDatabase");
            if (database == null) return 0;
            long total = 0;
            IReadOnlyList<EquipmentInstance> items = GetEquipped(character);
            for (int i = 0; i < items.Count; i++)
            {
                EquipmentDefinition definition = database.FindEquipment(items[i].equipmentId);
                if (definition != null) total += definition.EstimateCombatPower(items[i].level);
            }
            total += EquipmentSetEffectService.EstimateCombatPower(items, database);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public string ExportJson(bool pretty = true) => JsonUtility.ToJson(Load(), pretty);

        public bool TryImportJson(string json, EquipmentDatabase database, out string error)
        {
            try
            {
                EquipmentInventoryPayload payload = JsonUtility.FromJson<EquipmentInventoryPayload>(json);
                if (payload == null || payload.items == null) throw new InvalidOperationException("Invalid inventory JSON.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                payload.items.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.instanceId)
                    || string.IsNullOrWhiteSpace(item.equipmentId) || !ids.Add(item.instanceId));
                for (int i = 0; i < payload.items.Count; i++)
                {
                    EquipmentInstance item = payload.items[i];
                    EquipmentDefinition definition = database?.FindEquipment(item.equipmentId);
                    item.level = Mathf.Clamp(item.level, 1, definition != null ? definition.MaximumLevel : 999);
                    item.equippedCharacterId ??= string.Empty;
                }
                Save(payload);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void Reset()
        {
            storage.DeleteKey(StorageKey);
            storage.Save();
        }

        internal EquipmentInventoryPayload LoadMutable() => Load();
        internal void SaveMutable(EquipmentInventoryPayload payload) => Save(payload);

        internal EquipmentInventoryStorageSnapshot CaptureStorageSnapshot() =>
            new EquipmentInventoryStorageSnapshot(storage.HasKey(StorageKey),
                storage.GetString(StorageKey, string.Empty));

        internal void RestoreStorageSnapshot(EquipmentInventoryStorageSnapshot snapshot)
        {
            if (snapshot.HadValue) storage.SetString(StorageKey, snapshot.Json);
            else storage.DeleteKey(StorageKey);
        }

        internal IReadOnlyList<EquipmentInstance> StageAddRange(
            IReadOnlyList<EquipmentInstance> additions)
        {
            if (additions == null || additions.Count == 0)
                return Array.Empty<EquipmentInstance>();

            EquipmentInventoryPayload payload = Load();
            var staged = new List<EquipmentInstance>(additions.Count);
            for (int i = 0; i < additions.Count; i++)
            {
                EquipmentInstance addition = additions[i];
                if (addition == null || string.IsNullOrWhiteSpace(addition.instanceId)
                    || string.IsNullOrWhiteSpace(addition.equipmentId))
                    continue;

                string instanceId = addition.instanceId.Trim();
                EquipmentInstance existing = payload.items.Find(item => item != null
                    && string.Equals(item.instanceId, instanceId, StringComparison.Ordinal));
                if (existing != null)
                {
                    staged.Add(existing.Clone());
                    continue;
                }

                var item = new EquipmentInstance
                {
                    instanceId = instanceId,
                    equipmentId = addition.equipmentId.Trim(),
                    level = Mathf.Max(1, addition.level),
                    equippedCharacterId = addition.equippedCharacterId ?? string.Empty
                };
                payload.items.Add(item);
                staged.Add(item.Clone());
            }

            storage.SetString(StorageKey, JsonUtility.ToJson(payload));
            return staged;
        }

        internal void CommitStaged() => storage.Save();

        EquipmentInventoryPayload Load()
        {
            string json = storage.GetString(StorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json)) return new EquipmentInventoryPayload();
            try
            {
                EquipmentInventoryPayload payload = JsonUtility.FromJson<EquipmentInventoryPayload>(json);
                if (payload != null && payload.items != null) return payload;
            }
            catch (Exception) { }
            return new EquipmentInventoryPayload();
        }

        void Save(EquipmentInventoryPayload payload)
        {
            payload ??= new EquipmentInventoryPayload();
            payload.items ??= new List<EquipmentInstance>();
            storage.SetString(StorageKey, JsonUtility.ToJson(payload));
            storage.Save();
        }
    }
}
