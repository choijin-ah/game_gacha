using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum AwakeningStatType
    {
        CombatPowerFlat,
        MaxHpPercent,
        AttackPercent,
        DefensePercent,
        SpeedFlat,
        CritChanceFlat,
        CritDamageFlat,
        DamageIncrease
    }

    [Serializable]
    public sealed class AwakeningStatModifier
    {
        [SerializeField] AwakeningStatType stat;
        [SerializeField] float value;

        public AwakeningStatType Stat => stat;
        public float Value => value;
    }

    [Serializable]
    public sealed class AwakeningSkillEffectChange
    {
        [SerializeField] BattleActionKind action = BattleActionKind.Skill;
        [SerializeField] float damageMultiplierBonus;
        [SerializeField] float healingMultiplierBonus;
        [SerializeField] int breakDamageBonus;
        [SerializeField] int energyCostDelta;
        [SerializeField, TextArea(1, 3)] string description;

        public BattleActionKind Action => action;
        public float DamageMultiplierBonus => damageMultiplierBonus;
        public float HealingMultiplierBonus => healingMultiplierBonus;
        public int BreakDamageBonus => breakDamageBonus;
        public int EnergyCostDelta => energyCostDelta;
        public string Description => description ?? string.Empty;
    }

    [Serializable]
    public sealed class AwakeningStageDefinition
    {
        [SerializeField, Min(1)] int requiredFragments = 10;
        [SerializeField] List<AwakeningStatModifier> statModifiers =
            new List<AwakeningStatModifier>();
        [SerializeField] List<AwakeningSkillEffectChange> skillEffectChanges =
            new List<AwakeningSkillEffectChange>();
        [SerializeField, TextArea(2, 4)] string description;

        public int RequiredFragments => Mathf.Max(1, requiredFragments);
        public IReadOnlyList<AwakeningStatModifier> StatModifiers => statModifiers
            ?? (IReadOnlyList<AwakeningStatModifier>)Array.Empty<AwakeningStatModifier>();
        public IReadOnlyList<AwakeningSkillEffectChange> SkillEffectChanges => skillEffectChanges
            ?? (IReadOnlyList<AwakeningSkillEffectChange>)Array.Empty<AwakeningSkillEffectChange>();
        public string Description => description ?? string.Empty;

        internal void Sanitize()
        {
            requiredFragments = Mathf.Max(1, requiredFragments);
            statModifiers ??= new List<AwakeningStatModifier>();
            skillEffectChanges ??= new List<AwakeningSkillEffectChange>();
        }
    }
}
