using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class BreakResult
    {
        public CombatUnit Attacker { get; internal set; }
        public CombatUnit Target { get; internal set; }
        public BattleElement Element { get; internal set; }
        public bool WeaknessMatched { get; internal set; }
        public float BreakReduced { get; internal set; }
        public bool BreakTriggered { get; internal set; }
        public DamageResult BreakDamage { get; internal set; }
        public StatusEffectInstance AppliedEffect { get; internal set; }
    }

    public sealed class BreakSystem
    {
        readonly TurnManager turnManager;
        readonly DamageCalculator damageCalculator;
        readonly BattleEventBus eventBus;

        public BreakSystem(TurnManager turnManager = null, DamageCalculator damageCalculator = null,
            BattleEventBus eventBus = null)
        {
            this.turnManager = turnManager;
            this.damageCalculator = damageCalculator ?? new DamageCalculator();
            this.eventBus = eventBus;
        }

        public BreakResult ApplyBreakDamage(CombatUnit attacker, CombatUnit target,
            BattleElement element, float breakDamage, float breakDamageMultiplier = .5f)
        {
            var result = new BreakResult { Attacker = attacker, Target = target, Element = element };
            if (attacker == null || target == null || !target.IsAlive || target.IsBroken) return result;
            result.WeaknessMatched = target.HasWeakness(element);
            if (!result.WeaknessMatched) return result;
            result.BreakReduced = target.ReduceBreak(breakDamage);
            if (target.BreakCurrent > 0f || result.BreakReduced <= 0f) return result;

            result.BreakTriggered = true;
            result.BreakDamage = damageCalculator.CalculateAndApply(new DamageRequest
            {
                Attacker = attacker,
                Target = target,
                Element = element,
                SkillMultiplier = Math.Max(0f, breakDamageMultiplier),
                CanCrit = false
            });
            if (target.IsAlive)
            {
                target.EnterBroken();
                ApplyDelay(target, .3f);
                result.AppliedEffect = ApplyElementEffect(attacker, target, element);
                eventBus?.Publish(new BreakTriggeredEvent(attacker, target, element, result));
            }
            return result;
        }

        public bool RecoverIfDue(CombatUnit unit) => unit != null && unit.TryRecoverBreakAfterRegularAction();

        StatusEffectInstance ApplyElementEffect(CombatUnit attacker, CombatUnit target, BattleElement element)
        {
            StatusEffectInstance effect = null;
            switch (element)
            {
                case BattleElement.Fire:
                    effect = new StatusEffectInstance(StatusEffectType.Burn, 0f, 2, attacker,
                        StatusStackBehavior.RefreshDuration, flatValue: attacker.Stats.Attack * .3f);
                    break;
                case BattleElement.Ice:
                    ApplyDelay(target, .2f);
                    break;
                case BattleElement.Lightning:
                    effect = new StatusEffectInstance(StatusEffectType.Shock, 0f, 2, attacker,
                        StatusStackBehavior.RefreshDuration, flatValue: attacker.Stats.Attack * .25f);
                    break;
                case BattleElement.Wind:
                    effect = new StatusEffectInstance(StatusEffectType.Bleed, 0f, 2, attacker,
                        StatusStackBehavior.RefreshDuration, flatValue: attacker.Stats.Attack * .25f);
                    break;
                case BattleElement.Light:
                    effect = new StatusEffectInstance(StatusEffectType.AttackDown, .15f, 2, attacker,
                        StatusStackBehavior.KeepHigher);
                    break;
                case BattleElement.Dark:
                    effect = new StatusEffectInstance(StatusEffectType.DefenseDown, .15f, 2, attacker,
                        StatusStackBehavior.KeepHigher);
                    break;
            }
            if (effect != null) target.ApplyStatus(effect);
            return effect;
        }

        void ApplyDelay(CombatUnit target, float rate)
        {
            if (turnManager != null)
            {
                turnManager.Delay(target, rate);
                return;
            }
            float effective = rate * (1f - BattleMath.Clamp01(target.DelayResistance));
            target.SetActionValue(target.ActionValue * (1d + effective));
        }
    }

    public sealed class TargetingSystem
    {
        readonly Random random;

        public TargetingSystem(Random random = null)
        {
            this.random = random ?? new Random();
        }

        public IReadOnlyList<CombatUnit> Resolve(BattleTargetType targetType, CombatUnit actor,
            CombatUnit primaryTarget, IEnumerable<CombatUnit> combatants)
        {
            var all = Alive(combatants);
            if (actor == null || !actor.IsAlive) return Array.Empty<CombatUnit>();
            var enemies = Team(all, actor.Team == BattleTeam.Player ? BattleTeam.Enemy : BattleTeam.Player);
            var allies = Team(all, actor.Team);

            switch (targetType)
            {
                case BattleTargetType.SingleEnemy:
                    return Single(primaryTarget, enemies);
                case BattleTargetType.SingleAlly:
                    return Single(primaryTarget, allies);
                case BattleTargetType.AllEnemies:
                    return enemies;
                case BattleTargetType.AllAllies:
                    return allies;
                case BattleTargetType.AdjacentEnemies:
                    return Adjacent(primaryTarget, enemies);
                case BattleTargetType.AdjacentAllies:
                    return Adjacent(primaryTarget, allies);
                case BattleTargetType.LowestHpAlly:
                    allies.Sort(CompareLowestHp);
                    return allies.Count == 0 ? Array.Empty<CombatUnit>() : new[] { allies[0] };
                case BattleTargetType.RandomEnemy:
                    return RandomEnemy(actor, enemies);
                case BattleTargetType.Self:
                    return new[] { actor };
                default:
                    return Array.Empty<CombatUnit>();
            }
        }

        static List<CombatUnit> Alive(IEnumerable<CombatUnit> units)
        {
            var result = new List<CombatUnit>();
            if (units == null) return result;
            foreach (CombatUnit unit in units) if (unit != null && unit.IsAlive) result.Add(unit);
            return result;
        }

        static List<CombatUnit> Team(List<CombatUnit> units, BattleTeam team)
        {
            var result = units.FindAll(unit => unit.Team == team);
            result.Sort((left, right) => left.Slot != right.Slot
                ? left.Slot.CompareTo(right.Slot) : string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            return result;
        }

        static IReadOnlyList<CombatUnit> Single(CombatUnit primary, List<CombatUnit> candidates)
        {
            if (primary != null && candidates.Contains(primary)) return new[] { primary };
            return candidates.Count == 0 ? Array.Empty<CombatUnit>() : new[] { candidates[0] };
        }

        static IReadOnlyList<CombatUnit> Adjacent(CombatUnit primary, List<CombatUnit> candidates)
        {
            if (candidates.Count == 0) return Array.Empty<CombatUnit>();
            int index = primary == null ? 0 : candidates.IndexOf(primary);
            if (index < 0) index = 0;
            var result = new List<CombatUnit>(3) { candidates[index] };
            if (index > 0) result.Add(candidates[index - 1]);
            if (index + 1 < candidates.Count) result.Add(candidates[index + 1]);
            result.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            return result;
        }

        IReadOnlyList<CombatUnit> RandomEnemy(CombatUnit actor, List<CombatUnit> candidates)
        {
            if (candidates.Count == 0) return Array.Empty<CombatUnit>();
            if (actor.Team != BattleTeam.Enemy)
                return new[] { candidates[random.Next(candidates.Count)] };

            double totalWeight = 0d;
            foreach (CombatUnit candidate in candidates)
                totalWeight += Math.Max(0f, candidate.AggroWeight);
            if (totalWeight <= 0d)
                return new[] { candidates[random.Next(candidates.Count)] };

            double roll = random.NextDouble() * totalWeight;
            CombatUnit lastWeighted = null;
            foreach (CombatUnit candidate in candidates)
            {
                double weight = Math.Max(0f, candidate.AggroWeight);
                if (weight <= 0d) continue;
                lastWeighted = candidate;
                roll -= weight;
                if (roll < 0d) return new[] { candidate };
            }
            return new[] { lastWeighted ?? candidates[candidates.Count - 1] };
        }

        static int CompareLowestHp(CombatUnit left, CombatUnit right)
        {
            int ratio = left.HpRatio.CompareTo(right.HpRatio);
            if (ratio != 0) return ratio;
            int slot = left.Slot.CompareTo(right.Slot);
            return slot != 0 ? slot : string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }
    }
}
