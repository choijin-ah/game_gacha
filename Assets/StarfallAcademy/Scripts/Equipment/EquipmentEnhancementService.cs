using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class EquipmentEnhancementService
    {
        const string CreditsKey = "StarfallAcademy.Credits";
        readonly IMetaStorage storage;
        readonly EquipmentInventoryService inventory;

        public EquipmentEnhancementService(IMetaStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            inventory = new EquipmentInventoryService(storage);
        }

        public bool TryEnhance(string instanceId, EquipmentDatabase database, out string message)
        {
            EquipmentInventoryPayload payload = inventory.LoadMutable();
            EquipmentInstance item = payload.items.Find(value => value != null && value.instanceId == instanceId);
            EquipmentDefinition definition = item == null ? null : database?.FindEquipment(item.equipmentId);
            if (item == null || definition == null)
            {
                message = "장비 인스턴스 또는 정의를 찾을 수 없습니다.";
                return false;
            }
            if (item.level >= definition.MaximumLevel)
            {
                message = "최대 강화 레벨입니다.";
                return false;
            }
            int cost = definition.GetEnhancementCost(item.level);
            int credits = Mathf.Max(0, storage.GetInt(CreditsKey, PlayerWallet.DefaultCredits));
            if (credits < cost)
            {
                message = "크레딧이 부족합니다.";
                return false;
            }
            int previousLevel = item.level;
            bool hadCredits = storage.HasKey(CreditsKey);
            bool hadInventory = storage.HasKey(EquipmentInventoryService.StorageKey);
            string previousInventory = storage.GetString(EquipmentInventoryService.StorageKey,
                string.Empty);
            try
            {
                item.level++;
                storage.SetInt(CreditsKey, credits - cost);
                storage.SetString(EquipmentInventoryService.StorageKey, JsonUtility.ToJson(payload));
                storage.Save();
                try { MissionService.RecordEnhancement(); }
                catch (Exception) { }
                message = definition.DisplayName + " LV." + item.level;
                return true;
            }
            catch (Exception exception)
            {
                item.level = previousLevel;
                if (hadCredits) storage.SetInt(CreditsKey, credits);
                else storage.DeleteKey(CreditsKey);
                if (hadInventory)
                    storage.SetString(EquipmentInventoryService.StorageKey, previousInventory);
                else storage.DeleteKey(EquipmentInventoryService.StorageKey);
                try { storage.Save(); } catch (Exception) { }
                message = "강화 저장에 실패했습니다: " + exception.Message;
                return false;
            }
        }
    }
}
