using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class ResourceSystem
    {
        public const int DefaultInitialSkillPoints = 3;
        public const int DefaultMaximumSkillPoints = 5;

        public int SkillPoints { get; private set; }
        public int MaximumSkillPoints { get; }
        public event Action<int, int> SkillPointsChanged;

        public ResourceSystem(int initialSkillPoints = DefaultInitialSkillPoints,
            int maximumSkillPoints = DefaultMaximumSkillPoints)
        {
            MaximumSkillPoints = Math.Max(1, maximumSkillPoints);
            SkillPoints = Clamp(initialSkillPoints);
        }

        public int GainSkillPoints(int amount)
        {
            int before = SkillPoints;
            SkillPoints = Clamp(SkillPoints + Math.Max(0, amount));
            Notify(before);
            return SkillPoints - before;
        }

        public bool CanSpendSkillPoints(int amount) => amount <= 0 || SkillPoints >= amount;

        public bool TrySpendSkillPoints(int amount)
        {
            amount = Math.Max(0, amount);
            if (SkillPoints < amount) return false;
            int before = SkillPoints;
            SkillPoints -= amount;
            Notify(before);
            return true;
        }

        public void Reset(int skillPoints = DefaultInitialSkillPoints)
        {
            int before = SkillPoints;
            SkillPoints = Clamp(skillPoints);
            Notify(before);
        }

        int Clamp(int value) => Math.Max(0, Math.Min(MaximumSkillPoints, value));

        void Notify(int before)
        {
            if (before != SkillPoints) SkillPointsChanged?.Invoke(before, SkillPoints);
        }
    }

    public sealed class TurnPreviewEntry
    {
        public CombatUnit Unit { get; internal set; }
        public int Order { get; internal set; }
        public double RelativeActionValue { get; internal set; }
        public bool IsCurrent { get; internal set; }
    }

    public sealed class TurnManager
    {
        readonly List<CombatUnit> units = new List<CombatUnit>();

        public IReadOnlyList<CombatUnit> Units => units;
        public CombatUnit CurrentActor { get; private set; }

        public TurnManager(IEnumerable<CombatUnit> combatants)
        {
            Reset(combatants);
        }

        public void Reset(IEnumerable<CombatUnit> combatants, bool resetActionValues = true)
        {
            units.Clear();
            CurrentActor = null;
            if (combatants == null) return;
            foreach (CombatUnit unit in combatants)
            {
                if (unit == null || units.Contains(unit)) continue;
                units.Add(unit);
                if (resetActionValues) unit.SetActionValue(unit.GetBaseActionInterval());
            }
        }

        public bool AddUnit(CombatUnit unit, bool initializeActionValue = true)
        {
            if (unit == null || units.Contains(unit)) return false;
            units.Add(unit);
            if (initializeActionValue) unit.SetActionValue(unit.GetBaseActionInterval());
            return true;
        }

        public CombatUnit NextActor()
        {
            if (CurrentActor != null && CurrentActor.IsAlive) return CurrentActor;
            CurrentActor = null;
            List<CombatUnit> alive = GetAliveUnits();
            if (alive.Count == 0) return null;
            double minimum = double.MaxValue;
            foreach (CombatUnit unit in alive) minimum = Math.Min(minimum, unit.ActionValue);
            if (minimum > 0d && minimum < double.MaxValue)
            {
                foreach (CombatUnit unit in alive)
                    unit.SetActionValue(Math.Max(0d, unit.ActionValue - minimum));
            }
            alive.Sort(CompareUnits);
            CurrentActor = alive[0];
            return CurrentActor;
        }

        public CombatUnit AdvanceToNextUnit() => NextActor();

        public bool CompleteRegularTurn(CombatUnit unit)
        {
            if (unit == null || !ReferenceEquals(unit, CurrentActor)) return false;
            if (unit.IsAlive) unit.SetActionValue(unit.ActionValue + unit.GetBaseActionInterval());
            unit.CompleteOwnerRegularAction();
            CurrentActor = null;
            return true;
        }

        public void Advance(CombatUnit unit, float rate)
        {
            if (unit == null || !unit.IsAlive) return;
            rate = BattleMath.Clamp01(rate);
            unit.SetActionValue(unit.ActionValue * (1d - rate));
        }

        public void Delay(CombatUnit unit, float rate)
        {
            if (unit == null || !unit.IsAlive) return;
            rate = Math.Max(0f, rate);
            float resistance = BattleMath.Clamp01(unit.DelayResistance);
            double effectiveRate = rate * (1d - resistance);
            unit.SetActionValue(unit.ActionValue * (1d + effectiveRate));
        }

        public IReadOnlyList<CombatUnit> PeekNextActions(int count = 8)
        {
            var entries = PeekTimeline(count);
            var result = new List<CombatUnit>(entries.Count);
            foreach (TurnPreviewEntry entry in entries) result.Add(entry.Unit);
            return result;
        }

        public IReadOnlyList<TurnPreviewEntry> PeekTimeline(int count = 8)
        {
            count = Math.Max(0, count);
            var result = new List<TurnPreviewEntry>(count);
            var states = new List<PreviewState>();
            foreach (CombatUnit unit in units)
                if (unit != null && unit.IsAlive) states.Add(new PreviewState(unit, unit.ActionValue));
            if (states.Count == 0) return result;

            double elapsed = 0d;
            for (int order = 0; order < count; order++)
            {
                PreviewState chosen;
                if (order == 0 && CurrentActor != null && CurrentActor.IsAlive)
                    chosen = states.Find(state => ReferenceEquals(state.Unit, CurrentActor));
                else
                {
                    states.Sort(ComparePreview);
                    chosen = states[0];
                    double step = Math.Max(0d, chosen.ActionValue);
                    elapsed += step;
                    foreach (PreviewState state in states) state.ActionValue = Math.Max(0d, state.ActionValue - step);
                }
                if (chosen == null) break;
                result.Add(new TurnPreviewEntry
                {
                    Unit = chosen.Unit,
                    Order = order,
                    RelativeActionValue = elapsed,
                    IsCurrent = order == 0 && ReferenceEquals(chosen.Unit, CurrentActor)
                });
                chosen.ActionValue += chosen.Unit.GetBaseActionInterval();
            }
            return result;
        }

        List<CombatUnit> GetAliveUnits()
        {
            var result = new List<CombatUnit>();
            foreach (CombatUnit unit in units) if (unit != null && unit.IsAlive) result.Add(unit);
            return result;
        }

        static int CompareUnits(CombatUnit left, CombatUnit right)
        {
            int action = CompareDouble(left.ActionValue, right.ActionValue);
            return action != 0 ? action : CompareTie(left, right);
        }

        static int CompareTie(CombatUnit left, CombatUnit right)
        {
            int speed = right.Stats.Speed.CompareTo(left.Stats.Speed);
            if (speed != 0) return speed;
            int slot = left.Slot.CompareTo(right.Slot);
            if (slot != 0) return slot;
            if (left.Team != right.Team) return left.Team == BattleTeam.Enemy ? -1 : 1;
            return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }

        static int ComparePreview(PreviewState left, PreviewState right)
        {
            int action = CompareDouble(left.ActionValue, right.ActionValue);
            return action != 0 ? action : CompareTie(left.Unit, right.Unit);
        }

        static int CompareDouble(double left, double right)
        {
            double difference = left - right;
            if (Math.Abs(difference) <= BattleMath.ActionValueEpsilon) return 0;
            return difference < 0d ? -1 : 1;
        }

        sealed class PreviewState
        {
            public CombatUnit Unit { get; }
            public double ActionValue { get; set; }
            public PreviewState(CombatUnit unit, double actionValue) { Unit = unit; ActionValue = actionValue; }
        }
    }
}
