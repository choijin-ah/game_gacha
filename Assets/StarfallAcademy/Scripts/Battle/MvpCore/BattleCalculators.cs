using System;

namespace StarfallAcademy.Lobby
{
    public sealed class DamageRequest
    {
        public CombatUnit Attacker { get; set; }
        public CombatUnit Target { get; set; }
        public BattleElement Element { get; set; } = BattleElement.Auto;
        public float SkillMultiplier { get; set; } = 1f;
        public float FixedDamage { get; set; }
        public float AdditionalDamageIncrease { get; set; }
        public float AdditionalDamageTaken { get; set; }
        public float DefenseIgnore { get; set; }
        public bool CanCrit { get; set; } = true;
        public bool? ForceCritical { get; set; }
    }

    public sealed class DamageResult
    {
        public CombatUnit Attacker { get; internal set; }
        public CombatUnit Target { get; internal set; }
        public BattleElement Element { get; internal set; }
        public float BaseDamage { get; internal set; }
        public bool IsCritical { get; internal set; }
        public float CriticalMultiplier { get; internal set; }
        public float DamageIncreaseMultiplier { get; internal set; }
        public float DefenseMultiplier { get; internal set; }
        public float ResistanceMultiplier { get; internal set; }
        public float DamageTakenMultiplier { get; internal set; }
        public int FinalDamage { get; internal set; }
        public DamageApplication Application { get; internal set; }
    }

    public sealed class DamageCalculator
    {
        readonly Random random;

        public DamageCalculator(Random random = null)
        {
            this.random = random ?? new Random();
        }

        public DamageResult Calculate(DamageRequest request)
        {
            if (request?.Attacker == null) throw new ArgumentException("Damage attacker is required.", nameof(request));
            if (request.Target == null) throw new ArgumentException("Damage target is required.", nameof(request));
            CombatUnit attacker = request.Attacker;
            CombatUnit target = request.Target;
            BattleElement element = request.Element == BattleElement.Auto ? attacker.Element : request.Element;
            float baseDamage = Math.Max(0f, attacker.Stats.Attack * Math.Max(0f, request.SkillMultiplier) + request.FixedDamage);
            bool critical = request.CanCrit && (request.ForceCritical ?? random.NextDouble() < attacker.Stats.CritChance);
            float criticalMultiplier = critical ? attacker.Stats.CritDamage : 1f;
            float damageIncrease = Math.Max(.1f, 1f + attacker.Stats.DamageIncrease + request.AdditionalDamageIncrease);
            float defense = target.Stats.Defense * (1f - BattleMath.Clamp01(request.DefenseIgnore));
            float defenseMultiplier = 100f / (100f + Math.Max(0f, defense));
            float resistanceMultiplier = 1f - target.Stats.GetElementResistance(element);
            float damageTaken = 1f + target.Stats.DamageTakenIncrease + request.AdditionalDamageTaken + (target.IsBroken ? .2f : 0f);
            damageTaken = Math.Max(.1f, damageTaken);
            int finalDamage = BattleMath.RoundDamage(baseDamage * criticalMultiplier * damageIncrease
                * defenseMultiplier * resistanceMultiplier * damageTaken);
            return new DamageResult
            {
                Attacker = attacker,
                Target = target,
                Element = element,
                BaseDamage = baseDamage,
                IsCritical = critical,
                CriticalMultiplier = criticalMultiplier,
                DamageIncreaseMultiplier = damageIncrease,
                DefenseMultiplier = defenseMultiplier,
                ResistanceMultiplier = resistanceMultiplier,
                DamageTakenMultiplier = damageTaken,
                FinalDamage = finalDamage
            };
        }

        public DamageResult CalculateAndApply(DamageRequest request)
        {
            DamageResult result = Calculate(request);
            result.Application = result.Target.TakeDamage(result.FinalDamage);
            return result;
        }
    }

    public sealed class HealingRequest
    {
        public CombatUnit Source { get; set; }
        public CombatUnit Target { get; set; }
        public float AttackMultiplier { get; set; }
        public float MaxHpMultiplier { get; set; }
        public float FixedHealing { get; set; }
    }

    public sealed class HealingResult
    {
        public CombatUnit Source { get; internal set; }
        public CombatUnit Target { get; internal set; }
        public float BaseHealing { get; internal set; }
        public int CalculatedHealing { get; internal set; }
        public int AppliedHealing { get; internal set; }
    }

    public sealed class HealingCalculator
    {
        public HealingResult Calculate(HealingRequest request)
        {
            if (request?.Source == null) throw new ArgumentException("Healing source is required.", nameof(request));
            if (request.Target == null) throw new ArgumentException("Healing target is required.", nameof(request));
            float baseHealing = Math.Max(0f, request.Source.Stats.Attack * Math.Max(0f, request.AttackMultiplier)
                + request.Source.MaxHp * Math.Max(0f, request.MaxHpMultiplier) + request.FixedHealing);
            float multiplier = Math.Max(.1f, 1f + request.Source.Stats.HealingIncrease)
                * Math.Max(.1f, 1f + request.Target.Stats.HealingReceivedIncrease);
            return new HealingResult
            {
                Source = request.Source,
                Target = request.Target,
                BaseHealing = baseHealing,
                CalculatedHealing = BattleMath.RoundDamage(baseHealing * multiplier)
            };
        }

        public HealingResult CalculateAndApply(HealingRequest request)
        {
            HealingResult result = Calculate(request);
            result.AppliedHealing = result.Target.Heal(result.CalculatedHealing);
            return result;
        }
    }
}
