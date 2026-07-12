using System;

namespace StarfallAcademy.Lobby
{
    public enum BattleTeam
    {
        Player,
        Enemy
    }

    // BattleActionKind is the data-facing enum. This name is kept for runtime/API
    // compatibility with the combat design document.
    public enum BattleSkillKind
    {
        Basic,
        Skill,
        Ultimate,
        Guard,
        Enemy
    }

    public enum EnemyArchetype
    {
        Auto,
        Drone,
        Defender,
        ElitePredator,
        BossObserver
    }

    public enum StatusEffectType
    {
        AttackUp,
        DamageUp,
        DefenseDown,
        SpeedDown,
        AttackDown,
        Burn,
        Shock,
        Bleed,
        Shield
    }

    public enum StatusStackBehavior
    {
        NonStacking,
        KeepHigher,
        RefreshDuration,
        Stack,
        IndependentBySource
    }

    public enum BattleStatType
    {
        MaxHp,
        Attack,
        Defense,
        Speed,
        CritChance,
        CritDamage,
        DamageIncrease,
        DamageTakenIncrease,
        EffectHit,
        EffectResistance,
        HealingIncrease,
        HealingReceivedIncrease,
        ElementResistance
    }

    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat
    }

    public static class BattleKindConversions
    {
        public static BattleSkillKind ToSkillKind(this BattleActionKind kind)
        {
            return kind switch
            {
                BattleActionKind.Basic => BattleSkillKind.Basic,
                BattleActionKind.Skill => BattleSkillKind.Skill,
                BattleActionKind.Ultimate => BattleSkillKind.Ultimate,
                BattleActionKind.Guard => BattleSkillKind.Guard,
                _ => BattleSkillKind.Enemy
            };
        }

        public static BattleActionKind ToActionKind(this BattleSkillKind kind)
        {
            return kind switch
            {
                BattleSkillKind.Basic => BattleActionKind.Basic,
                BattleSkillKind.Skill => BattleActionKind.Skill,
                BattleSkillKind.Ultimate => BattleActionKind.Ultimate,
                BattleSkillKind.Guard => BattleActionKind.Guard,
                _ => BattleActionKind.Enemy
            };
        }
    }

    internal static class BattleMath
    {
        public const double ActionValueEpsilon = 0.000001d;

        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static float NonNegative(float value) => value < 0f ? 0f : value;

        public static int RoundDamage(float value)
        {
            if (value <= 0f) return 0;
            return Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));
        }
    }
}
