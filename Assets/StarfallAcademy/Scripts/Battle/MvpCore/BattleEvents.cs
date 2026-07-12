using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public interface IBattleEvent { }

    public sealed class BattleEventBus
    {
        readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();
        public event Action<IBattleEvent> AnyEvent;

        public IDisposable Subscribe<T>(Action<T> handler) where T : IBattleEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                handlers[type] = list;
            }
            list.Add(handler);
            return new Subscription(() => Unsubscribe(handler));
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IBattleEvent
        {
            if (handler == null || !handlers.TryGetValue(typeof(T), out List<Delegate> list)) return;
            list.Remove(handler);
            if (list.Count == 0) handlers.Remove(typeof(T));
        }

        public void Publish<T>(T battleEvent) where T : IBattleEvent
        {
            if (battleEvent == null) return;
            if (handlers.TryGetValue(typeof(T), out List<Delegate> list))
            {
                Delegate[] snapshot = list.ToArray();
                foreach (Delegate handler in snapshot) ((Action<T>)handler).Invoke(battleEvent);
            }
            AnyEvent?.Invoke(battleEvent);
        }

        public void Clear()
        {
            handlers.Clear();
            AnyEvent = null;
        }

        sealed class Subscription : IDisposable
        {
            Action unsubscribe;
            public Subscription(Action unsubscribe) { this.unsubscribe = unsubscribe; }
            public void Dispose() { unsubscribe?.Invoke(); unsubscribe = null; }
        }
    }

    public sealed class BattleStartedEvent : IBattleEvent
    {
        public IReadOnlyList<CombatUnit> Units { get; }
        public BattleStartedEvent(IReadOnlyList<CombatUnit> units) { Units = units; }
    }

    public sealed class TurnStartedEvent : IBattleEvent
    {
        public CombatUnit Unit { get; }
        public TurnStartedEvent(CombatUnit unit) { Unit = unit; }
    }

    public sealed class ActionResolvedEvent : IBattleEvent
    {
        public ActionResolution Resolution { get; }
        public ActionResolvedEvent(ActionResolution resolution) { Resolution = resolution; }
    }

    public sealed class DamageDealtEvent : IBattleEvent
    {
        public DamageResult Result { get; }
        public DamageDealtEvent(DamageResult result) { Result = result; }
    }

    public sealed class HealingDoneEvent : IBattleEvent
    {
        public HealingResult Result { get; }
        public HealingDoneEvent(HealingResult result) { Result = result; }
    }

    public sealed class BreakTriggeredEvent : IBattleEvent
    {
        public CombatUnit Attacker { get; }
        public CombatUnit Target { get; }
        public BattleElement Element { get; }
        public BreakResult Result { get; }
        public BreakTriggeredEvent(CombatUnit attacker, CombatUnit target, BattleElement element, BreakResult result)
        { Attacker = attacker; Target = target; Element = element; Result = result; }
    }

    public sealed class StatusAppliedEvent : IBattleEvent
    {
        public CombatUnit Target { get; }
        public StatusApplyResult Result { get; }
        public StatusAppliedEvent(CombatUnit target, StatusApplyResult result) { Target = target; Result = result; }
    }

    public sealed class ResourcesChangedEvent : IBattleEvent
    {
        public int SkillPoints { get; }
        public CombatUnit Unit { get; }
        public float Energy { get; }
        public ResourcesChangedEvent(int skillPoints, CombatUnit unit = null)
        { SkillPoints = skillPoints; Unit = unit; Energy = unit?.Energy ?? 0f; }
    }

    public sealed class UnitDefeatedEvent : IBattleEvent
    {
        public CombatUnit Unit { get; }
        public UnitDefeatedEvent(CombatUnit unit) { Unit = unit; }
    }

    public sealed class BattleEndedEvent : IBattleEvent
    {
        public BattleOutcome Outcome { get; }
        public BattleEndedEvent(BattleOutcome outcome) { Outcome = outcome; }
    }
}
