using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class BattleBaseStats
    {
        readonly Dictionary<BattleElement, float> elementResistances = new Dictionary<BattleElement, float>();

        public float MaxHp { get; set; } = 1000f;
        public float Attack { get; set; } = 100f;
        public float Defense { get; set; } = 50f;
        public float Speed { get; set; } = 100f;
        public float CritChance { get; set; } = .05f;
        public float CritDamage { get; set; } = 1.5f;
        public float DamageIncrease { get; set; }
        public float DamageTakenIncrease { get; set; }
        public float EffectHit { get; set; }
        public float EffectResistance { get; set; }
        public float HealingIncrease { get; set; }
        public float HealingReceivedIncrease { get; set; }
        public float MaxEnergy { get; set; } = 100f;
        public IReadOnlyDictionary<BattleElement, float> ElementResistances => elementResistances;

        public BattleBaseStats()
        {
        }

        public BattleBaseStats(float maxHp, float attack, float defense, float speed,
            float critChance = .05f, float critDamage = 1.5f, float maxEnergy = 100f)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            CritChance = critChance;
            CritDamage = critDamage;
            MaxEnergy = maxEnergy;
            Sanitize();
        }

        public float GetElementResistance(BattleElement element)
        {
            return elementResistances.TryGetValue(element, out float value) ? value : 0f;
        }

        public void SetElementResistance(BattleElement element, float value)
        {
            if (element == BattleElement.Auto) return;
            elementResistances[element] = BattleMath.Clamp(value, -1f, .95f);
        }

        public void Sanitize()
        {
            MaxHp = Math.Max(1f, MaxHp);
            Attack = BattleMath.NonNegative(Attack);
            Defense = BattleMath.NonNegative(Defense);
            Speed = Math.Max(1f, Speed);
            CritChance = BattleMath.Clamp01(CritChance);
            CritDamage = Math.Max(1f, CritDamage);
            MaxEnergy = Math.Max(1f, MaxEnergy);
        }

        public BattleBaseStats Clone()
        {
            var clone = new BattleBaseStats
            {
                MaxHp = MaxHp,
                Attack = Attack,
                Defense = Defense,
                Speed = Speed,
                CritChance = CritChance,
                CritDamage = CritDamage,
                DamageIncrease = DamageIncrease,
                DamageTakenIncrease = DamageTakenIncrease,
                EffectHit = EffectHit,
                EffectResistance = EffectResistance,
                HealingIncrease = HealingIncrease,
                HealingReceivedIncrease = HealingReceivedIncrease,
                MaxEnergy = MaxEnergy
            };
            foreach (KeyValuePair<BattleElement, float> pair in elementResistances)
                clone.elementResistances[pair.Key] = pair.Value;
            return clone;
        }
    }

    public sealed class StatModifier
    {
        public BattleStatType Stat { get; }
        public float FlatValue { get; }
        public float PercentValue { get; }
        public BattleElement Element { get; }
        public string SourceId { get; }

        public StatModifier(BattleStatType stat, float flatValue = 0f, float percentValue = 0f,
            BattleElement element = BattleElement.Auto, string sourceId = null)
        {
            Stat = stat;
            FlatValue = flatValue;
            PercentValue = percentValue;
            Element = element;
            SourceId = sourceId ?? string.Empty;
        }
    }

    public sealed class RuntimeStats
    {
        readonly Dictionary<BattleElement, float> elementResistances = new Dictionary<BattleElement, float>();

        public float MaxHp { get; private set; }
        public float Attack { get; private set; }
        public float Defense { get; private set; }
        public float Speed { get; private set; }
        public float CritChance { get; private set; }
        public float CritDamage { get; private set; }
        public float DamageIncrease { get; private set; }
        public float DamageTakenIncrease { get; private set; }
        public float EffectHit { get; private set; }
        public float EffectResistance { get; private set; }
        public float HealingIncrease { get; private set; }
        public float HealingReceivedIncrease { get; private set; }
        public IReadOnlyDictionary<BattleElement, float> ElementResistances => elementResistances;

        public RuntimeStats(BattleBaseStats baseStats)
        {
            Recalculate(baseStats, Array.Empty<StatModifier>());
        }

        public void Recalculate(BattleBaseStats baseStats, IEnumerable<StatusEffectInstance> statusEffects)
        {
            var modifiers = new List<StatModifier>();
            if (statusEffects != null)
            {
                foreach (StatusEffectInstance effect in statusEffects)
                {
                    if (effect == null) continue;
                    modifiers.AddRange(effect.GetStatModifiers());
                }
            }
            Recalculate(baseStats, modifiers);
        }

        public void Recalculate(BattleBaseStats baseStats, IEnumerable<StatModifier> modifiers)
        {
            if (baseStats == null) throw new ArgumentNullException(nameof(baseStats));
            baseStats.Sanitize();

            var flat = new Dictionary<BattleStatType, float>();
            var percent = new Dictionary<BattleStatType, float>();
            var resistanceFlat = new Dictionary<BattleElement, float>();
            var resistancePercent = new Dictionary<BattleElement, float>();

            if (modifiers != null)
            {
                foreach (StatModifier modifier in modifiers)
                {
                    if (modifier == null) continue;
                    if (modifier.Stat == BattleStatType.ElementResistance && modifier.Element != BattleElement.Auto)
                    {
                        Add(resistanceFlat, modifier.Element, modifier.FlatValue);
                        Add(resistancePercent, modifier.Element, modifier.PercentValue);
                    }
                    else
                    {
                        Add(flat, modifier.Stat, modifier.FlatValue);
                        Add(percent, modifier.Stat, modifier.PercentValue);
                    }
                }
            }

            MaxHp = Math.Max(1f, Apply(baseStats.MaxHp, BattleStatType.MaxHp, flat, percent));
            Attack = BattleMath.NonNegative(Apply(baseStats.Attack, BattleStatType.Attack, flat, percent));
            Defense = BattleMath.NonNegative(Apply(baseStats.Defense, BattleStatType.Defense, flat, percent));
            Speed = Math.Max(1f, Apply(baseStats.Speed, BattleStatType.Speed, flat, percent));
            CritChance = BattleMath.Clamp01(Apply(baseStats.CritChance, BattleStatType.CritChance, flat, percent));
            CritDamage = Math.Max(1f, Apply(baseStats.CritDamage, BattleStatType.CritDamage, flat, percent));
            DamageIncrease = Math.Max(-.9f, Apply(baseStats.DamageIncrease, BattleStatType.DamageIncrease, flat, percent));
            DamageTakenIncrease = Math.Max(-.9f, Apply(baseStats.DamageTakenIncrease, BattleStatType.DamageTakenIncrease, flat, percent));
            EffectHit = Math.Max(-1f, Apply(baseStats.EffectHit, BattleStatType.EffectHit, flat, percent));
            EffectResistance = BattleMath.Clamp(Apply(baseStats.EffectResistance, BattleStatType.EffectResistance, flat, percent), 0f, .95f);
            HealingIncrease = Math.Max(-.9f, Apply(baseStats.HealingIncrease, BattleStatType.HealingIncrease, flat, percent));
            HealingReceivedIncrease = Math.Max(-.9f, Apply(baseStats.HealingReceivedIncrease, BattleStatType.HealingReceivedIncrease, flat, percent));

            elementResistances.Clear();
            foreach (BattleElement element in Enum.GetValues(typeof(BattleElement)))
            {
                if (element == BattleElement.Auto) continue;
                float value = (baseStats.GetElementResistance(element) + Get(resistanceFlat, element))
                    * (1f + Get(resistancePercent, element));
                elementResistances[element] = BattleMath.Clamp(value, -1f, .95f);
            }
        }

        public float GetElementResistance(BattleElement element)
        {
            return elementResistances.TryGetValue(element, out float value) ? value : 0f;
        }

        static float Apply(float baseValue, BattleStatType stat,
            Dictionary<BattleStatType, float> flat, Dictionary<BattleStatType, float> percent)
        {
            return (baseValue + Get(flat, stat)) * (1f + Get(percent, stat));
        }

        static float Get<TKey>(Dictionary<TKey, float> values, TKey key)
        {
            return values.TryGetValue(key, out float value) ? value : 0f;
        }

        static void Add<TKey>(Dictionary<TKey, float> values, TKey key, float value)
        {
            values[key] = Get(values, key) + value;
        }
    }
}
