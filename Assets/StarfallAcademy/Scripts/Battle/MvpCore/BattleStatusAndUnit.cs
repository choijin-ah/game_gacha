using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class StatusEffectInstance
    {
        readonly List<StatModifier> customModifiers;

        public string EffectId { get; }
        public StatusEffectType Type { get; }
        public CombatUnit Source { get; }
        public CombatUnit Target { get; internal set; }
        public float Magnitude { get; private set; }
        public float FlatValue { get; private set; }
        public float ApplyChance { get; }
        public int RemainingOwnerActions { get; private set; }
        public int Stacks { get; private set; }
        public int MaxStacks { get; }
        public StatusStackBehavior StackBehavior { get; }
        public bool Dispelable { get; }
        public float RuntimeValue { get; internal set; }
        public bool IsExpired => RemainingOwnerActions == 0;
        public float TotalMagnitude => Magnitude * Math.Max(1, Stacks);
        public float TotalFlatValue => FlatValue * Math.Max(1, Stacks);

        public StatusEffectInstance(StatusEffectType type, float magnitude, int remainingOwnerActions,
            CombatUnit source = null, StatusStackBehavior stackBehavior = StatusStackBehavior.RefreshDuration,
            int maxStacks = 1, string effectId = null, float flatValue = 0f,
            float applyChance = 1f, bool dispelable = true, IEnumerable<StatModifier> modifiers = null)
        {
            Type = type;
            Magnitude = magnitude;
            FlatValue = flatValue;
            ApplyChance = BattleMath.Clamp01(applyChance);
            RemainingOwnerActions = remainingOwnerActions < 0 ? -1 : Math.Max(1, remainingOwnerActions);
            Source = source;
            StackBehavior = stackBehavior;
            MaxStacks = Math.Max(1, maxStacks);
            Stacks = 1;
            EffectId = string.IsNullOrWhiteSpace(effectId) ? type.ToString() : effectId;
            Dispelable = dispelable;
            customModifiers = modifiers == null ? new List<StatModifier>() : new List<StatModifier>(modifiers);
        }

        public IReadOnlyList<StatModifier> GetStatModifiers()
        {
            var result = new List<StatModifier>(customModifiers);
            float value = TotalMagnitude;
            switch (Type)
            {
                case StatusEffectType.AttackUp:
                    result.Add(new StatModifier(BattleStatType.Attack, 0f, value, sourceId: EffectId));
                    break;
                case StatusEffectType.AttackDown:
                    result.Add(new StatModifier(BattleStatType.Attack, 0f, -value, sourceId: EffectId));
                    break;
                case StatusEffectType.DefenseDown:
                    result.Add(new StatModifier(BattleStatType.Defense, 0f, -value, sourceId: EffectId));
                    break;
                case StatusEffectType.SpeedDown:
                    result.Add(new StatModifier(BattleStatType.Speed, 0f, -value, sourceId: EffectId));
                    break;
                case StatusEffectType.DamageUp:
                    result.Add(new StatModifier(BattleStatType.DamageIncrease, value, 0f, sourceId: EffectId));
                    break;
            }
            return result;
        }

        public StatusEffectInstance CloneFor(CombatUnit target)
        {
            var clone = new StatusEffectInstance(Type, Magnitude, RemainingOwnerActions, Source,
                StackBehavior, MaxStacks, EffectId, FlatValue, ApplyChance, Dispelable, customModifiers)
            {
                Target = target,
                Stacks = Stacks,
                RuntimeValue = RuntimeValue
            };
            return clone;
        }

        public bool IsSameFamily(StatusEffectInstance other)
        {
            if (other == null || !string.Equals(EffectId, other.EffectId, StringComparison.Ordinal)) return false;
            return StackBehavior != StatusStackBehavior.IndependentBySource || ReferenceEquals(Source, other.Source);
        }

        internal bool MergeFrom(StatusEffectInstance incoming)
        {
            if (incoming == null) return false;
            switch (StackBehavior)
            {
                case StatusStackBehavior.NonStacking:
                    return false;
                case StatusStackBehavior.KeepHigher:
                {
                    bool stronger = Math.Abs(incoming.TotalMagnitude) > Math.Abs(TotalMagnitude)
                        || Math.Abs(incoming.TotalFlatValue) > Math.Abs(TotalFlatValue);
                    if (stronger)
                    {
                        Magnitude = incoming.Magnitude;
                        FlatValue = incoming.FlatValue;
                        Stacks = incoming.Stacks;
                    }
                    RemainingOwnerActions = MergeDuration(RemainingOwnerActions, incoming.RemainingOwnerActions);
                    return stronger;
                }
                case StatusStackBehavior.Stack:
                    Stacks = Math.Min(MaxStacks, Stacks + Math.Max(1, incoming.Stacks));
                    Magnitude = Math.Max(Magnitude, incoming.Magnitude);
                    FlatValue = Math.Max(FlatValue, incoming.FlatValue);
                    RemainingOwnerActions = MergeDuration(RemainingOwnerActions, incoming.RemainingOwnerActions);
                    return true;
                case StatusStackBehavior.IndependentBySource:
                case StatusStackBehavior.RefreshDuration:
                    Magnitude = Math.Max(Magnitude, incoming.Magnitude);
                    FlatValue = Math.Max(FlatValue, incoming.FlatValue);
                    RemainingOwnerActions = MergeDuration(RemainingOwnerActions, incoming.RemainingOwnerActions);
                    return true;
                default:
                    return false;
            }
        }

        internal void ConsumeOwnerAction()
        {
            if (RemainingOwnerActions > 0) RemainingOwnerActions--;
        }

        static int MergeDuration(int current, int incoming)
        {
            if (current < 0 || incoming < 0) return -1;
            return Math.Max(current, incoming);
        }
    }

    public sealed class StatusApplyResult
    {
        public bool Applied { get; internal set; }
        public bool Merged { get; internal set; }
        public string FailureReason { get; internal set; }
        public StatusEffectInstance Effect { get; internal set; }
    }

    public sealed class DamageApplication
    {
        public int RequestedDamage { get; internal set; }
        public int ShieldAbsorbed { get; internal set; }
        public int HpDamage { get; internal set; }
        public bool Defeated { get; internal set; }
    }

    public sealed class StatusTickResult
    {
        public StatusEffectInstance Effect { get; internal set; }
        public DamageApplication Damage { get; internal set; }
    }

    public sealed class CombatUnit
    {
        readonly List<StatusEffectInstance> statuses = new List<StatusEffectInstance>();
        readonly HashSet<BattleElement> weaknesses = new HashSet<BattleElement>();

        public string Id { get; }
        public string DisplayName { get; }
        public BattleTeam Team { get; }
        public int Slot { get; }
        public CharacterData CharacterData { get; }
        public BattleElement Element { get; set; }
        public EnemyArchetype Archetype { get; set; }
        public BattleBaseStats BaseStats { get; }
        public RuntimeStats Stats { get; }
        public float CurrentHp { get; private set; }
        public float MaxHp => Stats.MaxHp;
        public float Shield { get; private set; }
        public float Energy { get; private set; }
        public float MaxEnergy { get; private set; }
        public double ActionValue { get; private set; }
        public float BreakMax { get; private set; }
        public float BreakCurrent { get; private set; }
        public bool IsBroken { get; private set; }
        public bool BreakRecoveryPending { get; private set; }
        public bool IsBoss { get; set; }
        public float PhaseTwoThreshold { get; set; } = .5f;
        public float DelayResistance { get; set; }
        public bool Warning { get; private set; }
        public string WarningText { get; private set; }
        public int Phase { get; private set; } = 1;
        public int RegularActionsCompleted { get; private set; }
        public bool IsAlive => CurrentHp > 0f;
        public float HpRatio => MaxHp <= 0f ? 0f : CurrentHp / MaxHp;
        public IReadOnlyList<StatusEffectInstance> Statuses => statuses;
        public IReadOnlyCollection<BattleElement> Weaknesses => weaknesses;

        int breakRecoveryAction;

        public CombatUnit(string id, string displayName, BattleTeam team, int slot,
            BattleBaseStats baseStats, CharacterData characterData = null,
            BattleElement element = BattleElement.Auto, float breakMax = 0f)
        {
            if (baseStats == null) throw new ArgumentNullException(nameof(baseStats));
            Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
            Team = team;
            Slot = Math.Max(0, slot);
            CharacterData = characterData;
            Element = element;
            BaseStats = baseStats.Clone();
            Stats = new RuntimeStats(BaseStats);
            CurrentHp = Stats.MaxHp;
            MaxEnergy = Math.Max(1f, BaseStats.MaxEnergy);
            BreakMax = BattleMath.NonNegative(breakMax);
            BreakCurrent = BreakMax;
            ActionValue = GetBaseActionInterval();
        }

        public void SetWeaknesses(IEnumerable<BattleElement> elements)
        {
            weaknesses.Clear();
            if (elements == null) return;
            foreach (BattleElement element in elements)
                if (element != BattleElement.Auto) weaknesses.Add(element);
        }

        public bool HasWeakness(BattleElement element) => element != BattleElement.Auto && weaknesses.Contains(element);

        public double GetBaseActionInterval() => 10000d / Math.Max(1f, Stats.Speed);

        public void SetActionValue(double value) => ActionValue = Math.Max(0d, value);

        public DamageApplication TakeDamage(float amount)
        {
            int requested = BattleMath.RoundDamage(amount);
            int absorbed = Math.Min(requested, BattleMath.RoundDamage(Shield));
            if (absorbed > 0)
            {
                ConsumeStatusShields(absorbed);
                Shield = Math.Max(0f, Shield - absorbed);
            }
            int hpDamage = Math.Min(BattleMath.RoundDamage(CurrentHp), requested - absorbed);
            CurrentHp = Math.Max(0f, CurrentHp - hpDamage);
            if (IsBoss && Phase == 1 && HpRatio <= BattleMath.Clamp(PhaseTwoThreshold, .1f, .9f))
                Phase = 2;
            return new DamageApplication
            {
                RequestedDamage = requested,
                ShieldAbsorbed = absorbed,
                HpDamage = hpDamage,
                Defeated = !IsAlive
            };
        }

        public int Heal(float amount)
        {
            if (!IsAlive) return 0;
            float before = CurrentHp;
            CurrentHp = Math.Min(MaxHp, CurrentHp + BattleMath.NonNegative(amount));
            return BattleMath.RoundDamage(CurrentHp - before);
        }

        public float AddShield(float amount)
        {
            float applied = BattleMath.NonNegative(amount);
            Shield += applied;
            return applied;
        }

        public float GainEnergy(float amount)
        {
            float before = Energy;
            Energy = Math.Min(MaxEnergy, Energy + BattleMath.NonNegative(amount));
            return Energy - before;
        }

        public bool TrySpendEnergy(float amount)
        {
            amount = BattleMath.NonNegative(amount);
            if (Energy + .0001f < amount) return false;
            Energy = Math.Max(0f, Energy - amount);
            return true;
        }

        public void SetEnergy(float amount) => Energy = BattleMath.Clamp(amount, 0f, MaxEnergy);
        public bool CanUseUltimate(float energyCost = -1f) => Energy + .0001f >= (energyCost < 0f ? MaxEnergy : energyCost);

        public StatusApplyResult ApplyStatus(StatusEffectInstance incoming)
        {
            if (incoming == null) return new StatusApplyResult { FailureReason = "Status is null." };
            StatusEffectInstance applied = incoming.CloneFor(this);
            StatusEffectInstance existing = statuses.Find(effect => effect.IsSameFamily(applied));
            if (existing == null || applied.StackBehavior == StatusStackBehavior.IndependentBySource && !existing.IsSameFamily(applied))
            {
                statuses.Add(applied);
                if (applied.Type == StatusEffectType.Shield)
                {
                    applied.RuntimeValue = Math.Max(0f, applied.TotalFlatValue > 0f ? applied.TotalFlatValue : applied.TotalMagnitude);
                    AddShield(applied.RuntimeValue);
                }
                RecalculateStats();
                return new StatusApplyResult { Applied = true, Effect = applied };
            }

            float oldShieldCapacity = existing.Type == StatusEffectType.Shield
                ? Math.Max(0f, existing.TotalFlatValue > 0f ? existing.TotalFlatValue : existing.TotalMagnitude) : 0f;
            bool merged = existing.MergeFrom(applied);
            if (merged && existing.Type == StatusEffectType.Shield)
            {
                float newCapacity = Math.Max(0f, existing.TotalFlatValue > 0f ? existing.TotalFlatValue : existing.TotalMagnitude);
                float added = Math.Max(0f, newCapacity - oldShieldCapacity);
                existing.RuntimeValue += added;
                AddShield(added);
            }
            RecalculateStats();
            return new StatusApplyResult
            {
                Applied = merged,
                Merged = merged,
                Effect = existing,
                FailureReason = merged ? string.Empty : "Stacking rule rejected the status."
            };
        }

        public bool RemoveStatus(StatusEffectInstance effect)
        {
            if (effect == null || !statuses.Remove(effect)) return false;
            RemoveRemainingStatusShield(effect);
            RecalculateStats();
            return true;
        }

        public int RemoveStatuses(Predicate<StatusEffectInstance> predicate)
        {
            if (predicate == null) return 0;
            int removed = 0;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                if (!predicate(statuses[i])) continue;
                RemoveRemainingStatusShield(statuses[i]);
                statuses.RemoveAt(i);
                removed++;
            }
            if (removed > 0) RecalculateStats();
            return removed;
        }

        public IReadOnlyList<StatusTickResult> ProcessStatusTicksAtActionStart()
        {
            var results = new List<StatusTickResult>();
            if (!IsAlive) return results;
            foreach (StatusEffectInstance effect in statuses.ToArray())
            {
                if (effect.Type != StatusEffectType.Burn && effect.Type != StatusEffectType.Shock
                    && effect.Type != StatusEffectType.Bleed) continue;
                float tick = effect.TotalFlatValue > 0f ? effect.TotalFlatValue : effect.TotalMagnitude;
                DamageApplication damage = TakeDamage(tick);
                results.Add(new StatusTickResult { Effect = effect, Damage = damage });
                if (!IsAlive) break;
            }
            return results;
        }

        public void CompleteOwnerRegularAction()
        {
            RegularActionsCompleted++;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                statuses[i].ConsumeOwnerAction();
                if (!statuses[i].IsExpired) continue;
                RemoveRemainingStatusShield(statuses[i]);
                statuses.RemoveAt(i);
            }
            RecalculateStats();
            TryRecoverBreakAfterRegularAction();
        }

        public void ConfigureBreak(float maxBreak)
        {
            BreakMax = BattleMath.NonNegative(maxBreak);
            BreakCurrent = BreakMax;
            IsBroken = false;
            BreakRecoveryPending = false;
        }

        internal float ReduceBreak(float amount)
        {
            if (BreakMax <= 0f || IsBroken) return 0f;
            float before = BreakCurrent;
            BreakCurrent = Math.Max(0f, BreakCurrent - BattleMath.NonNegative(amount));
            return before - BreakCurrent;
        }

        internal void EnterBroken()
        {
            if (IsBroken) return;
            IsBroken = true;
            BreakCurrent = 0f;
            BreakRecoveryPending = true;
            breakRecoveryAction = RegularActionsCompleted + 1;
            SetWarning(false);
        }

        public bool TryRecoverBreakAfterRegularAction()
        {
            if (!IsBroken || !BreakRecoveryPending || Team != BattleTeam.Enemy
                || RegularActionsCompleted < breakRecoveryAction) return false;
            IsBroken = false;
            BreakRecoveryPending = false;
            BreakCurrent = BreakMax;
            return true;
        }

        public void SetWarning(bool enabled, string text = null)
        {
            Warning = enabled;
            WarningText = enabled ? (text ?? "강력한 공격 준비") : string.Empty;
        }

        public void SetPhase(int phase) => Phase = Math.Max(1, phase);

        public void RecalculateStats()
        {
            Stats.Recalculate(BaseStats, statuses);
            CurrentHp = Math.Min(CurrentHp, MaxHp);
        }

        void ConsumeStatusShields(float amount)
        {
            float remaining = amount;
            foreach (StatusEffectInstance effect in statuses)
            {
                if (effect.Type != StatusEffectType.Shield || effect.RuntimeValue <= 0f) continue;
                float consumed = Math.Min(effect.RuntimeValue, remaining);
                effect.RuntimeValue -= consumed;
                remaining -= consumed;
                if (remaining <= 0f) break;
            }
        }

        void RemoveRemainingStatusShield(StatusEffectInstance effect)
        {
            if (effect.Type != StatusEffectType.Shield || effect.RuntimeValue <= 0f) return;
            Shield = Math.Max(0f, Shield - effect.RuntimeValue);
            effect.RuntimeValue = 0f;
        }
    }
}
