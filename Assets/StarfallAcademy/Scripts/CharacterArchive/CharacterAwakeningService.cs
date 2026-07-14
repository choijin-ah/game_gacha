using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class CharacterAwakeningService
    {
        const string StageKeyPrefix = "StarfallAcademy.Character.AwakeningStage.";
        static readonly object SyncRoot = new object();

        readonly IMetaStorage storage;
        readonly ItemInventoryService inventory;

        public CharacterAwakeningService(IMetaStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            inventory = new ItemInventoryService(storage);
        }

        public static CharacterAwakeningService Default { get; } =
            new CharacterAwakeningService(PlayerPrefsMetaStorage.Shared);

        public int GetStage(CharacterData character)
        {
            if (character == null) return 0;
            return Mathf.Clamp(storage.GetInt(StageKey(character), 0), 0,
                character.AwakeningStages.Count);
        }

        public int GetFragments(CharacterData character) => character == null ? 0
            : inventory.GetAmount(ItemInventoryService.CharacterFragmentId(character.Id));

        public bool CanAwaken(CharacterData character, out string reason)
        {
            if (character == null)
            {
                reason = "캐릭터를 선택해 주세요.";
                return false;
            }
            if (!CharacterProgressionService.IsOwned(character))
            {
                reason = "보유한 캐릭터만 각성할 수 있습니다.";
                return false;
            }
            int stage = GetStage(character);
            if (stage >= character.AwakeningStages.Count)
            {
                reason = "최대 각성 단계입니다.";
                return false;
            }
            int cost = character.AwakeningStages[stage].RequiredFragments;
            int fragments = GetFragments(character);
            if (fragments < cost)
            {
                reason = "캐릭터 조각이 부족합니다. (" + fragments + "/" + cost + ")";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool TryAwaken(CharacterData character, out string message)
        {
            if (!CanAwaken(character, out message)) return false;
            lock (SyncRoot)
            {
                int previousStage = GetStage(character);
                int cost = character.AwakeningStages[previousStage].RequiredFragments;
                string fragmentId = ItemInventoryService.CharacterFragmentId(character.Id);
                int previousFragments = inventory.GetAmount(fragmentId);
                bool hadFragments = inventory.HasStoredValue(fragmentId);
                bool hadStage = storage.HasKey(StageKey(character));
                try
                {
                    if (!inventory.TryStageSpend(fragmentId, cost, out int remaining))
                    {
                        message = "캐릭터 조각이 부족합니다.";
                        return false;
                    }
                    storage.SetInt(StageKey(character), previousStage + 1);
                    storage.Save();
                    message = character.DisplayName + " 각성 " + (previousStage + 1)
                        + "단계 완료 · 남은 조각 " + remaining;
                    return true;
                }
                catch (Exception exception)
                {
                    inventory.Restore(fragmentId, hadFragments, previousFragments);
                    if (hadStage) storage.SetInt(StageKey(character), previousStage);
                    else storage.DeleteKey(StageKey(character));
                    try { storage.Save(); } catch (Exception) { }
                    message = "각성 저장에 실패했습니다: " + exception.Message;
                    return false;
                }
            }
        }

        public int GetCombatPowerBonus(CharacterData character)
        {
            if (character == null) return 0;
            double total = 0d;
            int stage = GetStage(character);
            for (int i = 0; i < stage; i++)
            {
                var modifiers = character.AwakeningStages[i].StatModifiers;
                for (int j = 0; j < modifiers.Count; j++)
                {
                    AwakeningStatModifier modifier = modifiers[j];
                    if (modifier == null) continue;
                    if (modifier.Stat == AwakeningStatType.CombatPowerFlat)
                        total += Math.Max(0d, modifier.Value);
                    else if (modifier.Stat == AwakeningStatType.MaxHpPercent
                        || modifier.Stat == AwakeningStatType.AttackPercent
                        || modifier.Stat == AwakeningStatType.DefensePercent)
                        total += Math.Max(0d, modifier.Value) * 10d;
                }
            }
            return total >= int.MaxValue ? int.MaxValue : Math.Max(0, (int)Math.Round(total));
        }

        public void ApplyBattleStats(CharacterData character, BattleBaseStats stats)
        {
            if (character == null || stats == null) return;
            int stage = GetStage(character);
            for (int i = 0; i < stage; i++)
            {
                var modifiers = character.AwakeningStages[i].StatModifiers;
                for (int j = 0; j < modifiers.Count; j++) ApplyModifier(stats, modifiers[j]);
            }
            stats.Sanitize();
        }

        public BattleActionConfig ResolveAction(CharacterData character, BattleActionConfig action)
        {
            if (character == null) return action;
            float damage = action.DamageMultiplier;
            float healing = action.HealingMultiplier;
            int breakDamage = action.BreakDamage;
            int energyCost = action.EnergyCost;
            int stage = GetStage(character);
            for (int i = 0; i < stage; i++)
            {
                var changes = character.AwakeningStages[i].SkillEffectChanges;
                for (int j = 0; j < changes.Count; j++)
                {
                    AwakeningSkillEffectChange change = changes[j];
                    if (change == null || change.Action != action.Kind) continue;
                    damage += change.DamageMultiplierBonus;
                    healing += change.HealingMultiplierBonus;
                    breakDamage += change.BreakDamageBonus;
                    energyCost += change.EnergyCostDelta;
                }
            }
            return new BattleActionConfig(action.Name, action.Icon, action.Kind, action.TargetType,
                damage, healing, action.FixedValue, action.SkillPointCost, energyCost,
                action.EnergyGain, breakDamage, action.Element);
        }

        public void Reset(CharacterData character)
        {
            if (character == null) return;
            storage.DeleteKey(StageKey(character));
            storage.DeleteKey(ItemInventoryService.StorageKeyFor(
                ItemInventoryService.CharacterFragmentId(character.Id)));
            storage.Save();
        }

        static void ApplyModifier(BattleBaseStats stats, AwakeningStatModifier modifier)
        {
            if (modifier == null) return;
            float value = modifier.Value;
            switch (modifier.Stat)
            {
                case AwakeningStatType.MaxHpPercent: stats.MaxHp *= 1f + value / 100f; break;
                case AwakeningStatType.AttackPercent: stats.Attack *= 1f + value / 100f; break;
                case AwakeningStatType.DefensePercent: stats.Defense *= 1f + value / 100f; break;
                case AwakeningStatType.SpeedFlat: stats.Speed += value; break;
                case AwakeningStatType.CritChanceFlat: stats.CritChance += value / 100f; break;
                case AwakeningStatType.CritDamageFlat: stats.CritDamage += value / 100f; break;
                case AwakeningStatType.DamageIncrease: stats.DamageIncrease += value / 100f; break;
            }
        }

        static string StageKey(CharacterData character) => StageKeyPrefix + character.Id;
    }
}
