using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum EquipmentSlot
    {
        Weapon = 0,
        Armor = 1,
        Accessory = 2,
        AuxiliaryDevice = 3
    }

    // 기본 장비 MVP 저장소입니다. 장비 인벤토리가 도입되기 전까지 슬롯별 장착/강화를 관리합니다.
    public static class EquipmentService
    {
        const string LevelKeyPrefix = "StarfallAcademy.Equipment.Level.";
        public const int DefaultEquipmentLevel = 1;
        public const int MaxEquipmentLevel = 20;

        static readonly EquipmentSlot[] SlotOrder =
        {
            EquipmentSlot.Weapon,
            EquipmentSlot.Armor,
            EquipmentSlot.Accessory,
            EquipmentSlot.AuxiliaryDevice
        };

        public static IReadOnlyList<EquipmentSlot> Slots => SlotOrder;
        public static bool IsUnlocked =>
            PlayerProfileService.Default.IsUnlocked(AccountFeature.Equipment);

        public static string GetSlotDisplayName(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon: return "무기";
                case EquipmentSlot.Armor: return "방어구";
                case EquipmentSlot.Accessory: return "장신구";
                case EquipmentSlot.AuxiliaryDevice: return "보조 장치";
                default: return "장비";
            }
        }

        public static int GetLevel(CharacterData character, EquipmentSlot slot)
        {
            if (character == null || !IsValidSlot(slot)) return 0;
            MetaPlayerPrefsTransaction.RecoverPending();
            return Mathf.Clamp(PlayerPrefs.GetInt(LevelKey(character, slot), 0),
                0, MaxEquipmentLevel);
        }

        public static bool IsEquipped(CharacterData character, EquipmentSlot slot) =>
            GetLevel(character, slot) >= DefaultEquipmentLevel;

        public static int GetUpgradeCost(CharacterData character, EquipmentSlot slot)
        {
            int level = GetLevel(character, slot);
            if (level < DefaultEquipmentLevel || level >= MaxEquipmentLevel) return 0;
            GetSlotBalance(slot, out int basePower, out int powerGrowth,
                out int baseCost, out int costGrowth);
            long cost = (long)baseCost + (long)(level - DefaultEquipmentLevel) * costGrowth;
            return cost >= int.MaxValue ? int.MaxValue : (int)cost;
        }

        public static int GetSlotCombatPowerBonus(CharacterData character, EquipmentSlot slot)
        {
            int level = GetLevel(character, slot);
            if (level < DefaultEquipmentLevel) return 0;
            GetSlotBalance(slot, out int basePower, out int powerGrowth,
                out int baseCost, out int costGrowth);
            long power = (long)basePower + (long)(level - DefaultEquipmentLevel) * powerGrowth;
            return power >= int.MaxValue ? int.MaxValue : (int)power;
        }

        public static int GetCombatPowerBonus(CharacterData character)
        {
            if (!IsUnlocked) return 0;
            if (character != null && EquipmentInventoryService.Default.HasEquippedItems(character))
                return EquipmentInventoryService.Default.GetCombatPowerBonus(character);
            long total = 0;
            for (int i = 0; i < SlotOrder.Length; i++)
                total += GetSlotCombatPowerBonus(character, SlotOrder[i]);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public static bool TryEquipRecommended(CharacterData character, out string message)
        {
            if (!IsUnlocked)
            {
                message = "장비는 계정 LV."
                    + PlayerProfileService.GetRequiredLevel(AccountFeature.Equipment)
                    + "에 해금됩니다";
                return false;
            }
            if (character == null)
            {
                message = "캐릭터를 선택해 주세요";
                return false;
            }
            if (!CharacterProgressionService.IsOwned(character))
            {
                message = "미보유 캐릭터는 장비를 장착할 수 없습니다";
                return false;
            }

            int equippedCount = 0;
            var writes = new List<MetaIntWrite>(SlotOrder.Length);
            for (int i = 0; i < SlotOrder.Length; i++)
            {
                EquipmentSlot slot = SlotOrder[i];
                if (IsEquipped(character, slot)) continue;
                writes.Add(new MetaIntWrite(LevelKey(character, slot), DefaultEquipmentLevel));
                equippedCount++;
            }

            if (equippedCount == 0)
            {
                message = "기본 장비 4종이 모두 장착되어 있습니다";
                return false;
            }

            if (!MetaPlayerPrefsTransaction.Commit(writes))
            {
                message = "Save failed. Please try again.";
                return false;
            }
            message = character.DisplayName + "  기본 장비 " + equippedCount + "개 장착";
            return true;
        }

        public static bool TryUpgradeSlot(CharacterData character, EquipmentSlot slot,
            out string message)
        {
            if (!IsUnlocked)
            {
                message = "장비는 계정 LV."
                    + PlayerProfileService.GetRequiredLevel(AccountFeature.Equipment)
                    + "에 해금됩니다";
                return false;
            }
            if (character == null || !IsValidSlot(slot))
            {
                message = "강화할 장비를 선택해 주세요";
                return false;
            }
            if (!CharacterProgressionService.IsOwned(character))
            {
                message = "미보유 캐릭터는 장비를 강화할 수 없습니다";
                return false;
            }

            int level = GetLevel(character, slot);
            string slotName = GetSlotDisplayName(slot);
            if (level < DefaultEquipmentLevel)
            {
                message = slotName + "을 먼저 추천 장착해 주세요";
                return false;
            }
            if (level >= MaxEquipmentLevel)
            {
                message = slotName + "이 최대 레벨입니다";
                return false;
            }

            int cost = GetUpgradeCost(character, slot);
            var writes = new List<MetaIntWrite>(2);
            if (!PlayerWallet.TryStageCreditsSpend(cost, writes))
            {
                message = "크레딧이 부족합니다";
                return false;
            }

            writes.Add(new MetaIntWrite(LevelKey(character, slot), level + 1));
            if (!MetaPlayerPrefsTransaction.Commit(writes))
            {
                message = "Save failed. Please try again.";
                return false;
            }
            MissionService.RecordEnhancement();
            message = slotName + "  LV. " + (level + 1);
            return true;
        }

        public static void ResetLegacy(CharacterData character)
        {
            if (character == null) return;
            MetaPlayerPrefsTransaction.RecoverPending();
            for (int i = 0; i < SlotOrder.Length; i++)
                PlayerPrefs.DeleteKey(LevelKey(character, SlotOrder[i]));
            PlayerPrefs.Save();
        }

        public static string GetLegacyStorageKey(CharacterData character, EquipmentSlot slot) =>
            character == null || !IsValidSlot(slot) ? string.Empty : LevelKey(character, slot);

        static string LevelKey(CharacterData character, EquipmentSlot slot) =>
            LevelKeyPrefix + character.Id + "." + slot;

        static bool IsValidSlot(EquipmentSlot slot) =>
            slot >= EquipmentSlot.Weapon && slot <= EquipmentSlot.AuxiliaryDevice;

        static void GetSlotBalance(EquipmentSlot slot, out int basePower, out int powerGrowth,
            out int baseCost, out int costGrowth)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    basePower = 120; powerGrowth = 55; baseCost = 1000; costGrowth = 500;
                    return;
                case EquipmentSlot.Armor:
                    basePower = 110; powerGrowth = 50; baseCost = 900; costGrowth = 450;
                    return;
                case EquipmentSlot.Accessory:
                    basePower = 90; powerGrowth = 45; baseCost = 800; costGrowth = 400;
                    return;
                default:
                    basePower = 80; powerGrowth = 40; baseCost = 750; costGrowth = 350;
                    return;
            }
        }
    }
}
