using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    // 프로토타입용 캐릭터 보유/성장 저장소입니다. 서비스 단계에서는 서버 저장으로 교체하세요.
    public static class CharacterProgressionService
    {
        const string OwnedPrefix = "StarfallAcademy.Character.Owned.";
        const string LevelPrefix = "StarfallAcademy.Character.Level.";
        const string SkillLevelPrefix = "StarfallAcademy.Character.SkillLevel.";

        public static bool IsOwned(CharacterData character)
        {
            MetaPlayerPrefsTransaction.RecoverPending();
            return character != null && PlayerPrefs.GetInt(OwnedPrefix + character.Id, 0) == 1;
        }

        public static bool RegisterPull(CharacterData character)
        {
            if (character == null) return false;
            bool isNew = !IsOwned(character);
            var writes = new List<MetaIntWrite>(3);
            AppendPullRegistrationWrites(character, writes);
            if (!MetaPlayerPrefsTransaction.Commit(writes)) return false;
            return isNew;
        }

        internal static void AppendPullRegistrationWrites(CharacterData character,
            ICollection<MetaIntWrite> writes)
        {
            if (character == null || writes == null) return;
            string id = character.Id;
            writes.Add(new MetaIntWrite(OwnedPrefix + id, 1));
            if (!PlayerPrefs.HasKey(LevelPrefix + id))
                writes.Add(new MetaIntWrite(LevelPrefix + id, character.Level));
            if (!PlayerPrefs.HasKey(SkillLevelPrefix + id))
                writes.Add(new MetaIntWrite(SkillLevelPrefix + id, 1));
        }

        public static int GetLevel(CharacterData character)
        {
            if (character == null) return 1;
            MetaPlayerPrefsTransaction.RecoverPending();
            return Mathf.Clamp(PlayerPrefs.GetInt(LevelPrefix + character.Id, character.Level),
                character.Level, character.MaxLevel);
        }

        public static int GetSkillLevel(CharacterData character)
        {
            if (character == null) return 1;
            MetaPlayerPrefsTransaction.RecoverPending();
            return Mathf.Clamp(PlayerPrefs.GetInt(SkillLevelPrefix + character.Id, 1), 1,
                character.SkillMaxLevel);
        }

        public static int GetLevelUpCost(CharacterData character)
        {
            int progress = Mathf.Max(0, GetLevel(character) - character.Level);
            return character.LevelUpBaseCreditCost + progress * character.LevelUpCreditCostGrowth;
        }

        public static int GetSkillUpCost(CharacterData character)
        {
            int progress = Mathf.Max(0, GetSkillLevel(character) - 1);
            return character.SkillBaseMaterialCost + progress * character.SkillMaterialCostGrowth;
        }

        public static int GetCombatPower(CharacterData character)
        {
            if (character == null) return 0;
            int levelPower = Mathf.Max(0, GetLevel(character) - character.Level) * character.CombatPowerPerLevel;
            int skillPower = Mathf.Max(0, GetSkillLevel(character) - 1) * character.CombatPowerPerSkillLevel;
            long total = (long)character.CombatPower + levelPower + skillPower
                + EquipmentService.GetCombatPowerBonus(character)
                + CharacterAwakeningService.Default.GetCombatPowerBonus(character);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public static void Reset(CharacterData character)
        {
            if (character == null) return;
            MetaPlayerPrefsTransaction.RecoverPending();
            PlayerPrefs.DeleteKey(OwnedPrefix + character.Id);
            PlayerPrefs.DeleteKey(LevelPrefix + character.Id);
            PlayerPrefs.DeleteKey(SkillLevelPrefix + character.Id);
            CharacterAwakeningService.Default.Reset(character);
            PlayerPrefs.Save();
        }

        public static void ResetAll(CharacterDatabase database)
        {
            if (database == null) return;
            for (int i = 0; i < database.Characters.Count; i++) Reset(database.Characters[i]);
        }

        public static bool TryLevelUp(CharacterData character, out string message)
        {
            if (!IsOwned(character))
            {
                message = "미보유 캐릭터는 성장시킬 수 없습니다";
                return false;
            }
            int level = GetLevel(character);
            if (level >= character.MaxLevel)
            {
                message = "최대 레벨입니다";
                return false;
            }
            int cost = GetLevelUpCost(character);
            var writes = new List<MetaIntWrite>(2);
            if (!PlayerWallet.TryStageCreditsSpend(cost, writes))
            {
                message = "크레딧이 부족합니다";
                return false;
            }
            writes.Add(new MetaIntWrite(LevelPrefix + character.Id, level + 1));
            if (!MetaPlayerPrefsTransaction.Commit(writes))
            {
                message = "Save failed. Please try again.";
                return false;
            }
            MissionService.RecordEnhancement();
            message = character.DisplayName + "  LV. " + (level + 1);
            return true;
        }

        public static bool TrySkillUp(CharacterData character, out string message)
        {
            if (!IsOwned(character))
            {
                message = "미보유 캐릭터는 성장시킬 수 없습니다";
                return false;
            }
            int level = GetSkillLevel(character);
            if (level >= character.SkillMaxLevel)
            {
                message = "스킬이 최대 레벨입니다";
                return false;
            }
            int cost = GetSkillUpCost(character);
            var writes = new List<MetaIntWrite>(2);
            if (!PlayerWallet.TryStageSkillMaterialsSpend(cost, writes))
            {
                message = PlayerWallet.SkillMaterialDisplayName + "가 부족합니다";
                return false;
            }
            writes.Add(new MetaIntWrite(SkillLevelPrefix + character.Id, level + 1));
            if (!MetaPlayerPrefsTransaction.Commit(writes))
            {
                message = "Save failed. Please try again.";
                return false;
            }
            MissionService.RecordEnhancement();
            message = character.SkillName + "  LV. " + (level + 1);
            return true;
        }
    }
}
