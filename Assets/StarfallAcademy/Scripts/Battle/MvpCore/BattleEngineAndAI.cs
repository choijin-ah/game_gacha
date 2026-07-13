using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class EnemyAIController
    {
        readonly Random random;

        public EnemyAIController(Random random = null)
        {
            this.random = random ?? new Random();
        }

        public ActionRequest Decide(CombatUnit enemy, IReadOnlyList<CombatUnit> units)
        {
            if (enemy == null || !enemy.IsAlive || enemy.Team != BattleTeam.Enemy) return null;
            EnemyArchetype archetype = enemy.Archetype == EnemyArchetype.Auto
                ? (enemy.IsBoss ? EnemyArchetype.BossObserver : EnemyArchetype.Drone) : enemy.Archetype;
            return archetype switch
            {
                EnemyArchetype.Defender => DefenderAction(enemy, units),
                EnemyArchetype.ElitePredator => EliteAction(enemy),
                EnemyArchetype.BossObserver => BossAction(enemy),
                _ => BasicAction(enemy)
            };
        }

        ActionRequest BasicAction(CombatUnit enemy)
        {
            return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.RandomEnemy)
            {
                SkillName = "기본 공격",
                DamageMultiplier = 1f,
                Element = enemy.Element,
                ConsumesRegularTurn = true
            };
        }

        ActionRequest DefenderAction(CombatUnit enemy, IReadOnlyList<CombatUnit> units)
        {
            CombatUnit lowestShield = null;
            if (units != null)
            {
                foreach (CombatUnit unit in units)
                {
                    if (unit == null || !unit.IsAlive || unit.Team != BattleTeam.Enemy) continue;
                    if (lowestShield == null || unit.Shield < lowestShield.Shield
                        || Math.Abs(unit.Shield - lowestShield.Shield) < .001f && unit.Slot < lowestShield.Slot)
                        lowestShield = unit;
                }
            }
            if (lowestShield != null && lowestShield.Shield <= .01f)
            {
                return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.SingleAlly, lowestShield)
                {
                    SkillName = "방호 명령",
                    ShieldAmount = lowestShield.MaxHp * .15f,
                    ConsumesRegularTurn = true
                };
            }
            ActionRequest attack = BasicAction(enemy);
            attack.DamageMultiplier = .9f;
            return attack;
        }

        ActionRequest EliteAction(CombatUnit enemy)
        {
            if (enemy.Warning)
            {
                return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.AllEnemies)
                {
                    SkillName = "공명 폭발",
                    DamageMultiplier = 1.7f,
                    Element = enemy.Element,
                    WarningStateAfterResolution = false,
                    ConsumesRegularTurn = true
                };
            }
            if (enemy.RegularActionsCompleted % 4 == 1)
            {
                return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.Self, enemy)
                {
                    SkillName = "공명 충전",
                    WarningStateAfterResolution = true,
                    WarningText = "광역 공격 준비",
                    ConsumesRegularTurn = true
                };
            }
            return BasicAction(enemy);
        }

        ActionRequest BossAction(CombatUnit enemy)
        {
            if (enemy.Warning)
            {
                return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.AllEnemies)
                {
                    SkillName = enemy.Phase >= 2 ? "종말 좌표 붕괴" : "좌표 소거",
                    DamageMultiplier = enemy.Phase >= 2 ? 2f : 1.7f,
                    Element = enemy.Element,
                    WarningStateAfterResolution = false,
                    ConsumesRegularTurn = true
                };
            }
            int cadence = enemy.Phase >= 2 ? 2 : 3;
            if (enemy.RegularActionsCompleted > 0 && enemy.RegularActionsCompleted % cadence == 1)
            {
                return new ActionRequest(enemy, BattleActionKind.Enemy, BattleTargetType.Self, enemy)
                {
                    SkillName = "종말 좌표 지정",
                    WarningStateAfterResolution = true,
                    WarningText = "강력한 광역 공격 준비",
                    ConsumesRegularTurn = true
                };
            }
            ActionRequest request = BasicAction(enemy);
            request.SkillName = "관측 광선";
            request.DamageMultiplier = enemy.Phase >= 2 ? 1.35f : 1.2f;
            return request;
        }
    }

    public sealed class BattleCombatCore
    {
        readonly List<CombatUnit> units;
        readonly BattleActionExecutor executor;
        readonly EnemyAIController enemyAI;
        BattleOutcome lastPublishedOutcome = BattleOutcome.Ongoing;

        public IReadOnlyList<CombatUnit> Units => units;
        public TurnManager Turns { get; }
        public ResourceSystem Resources { get; }
        public TargetingSystem Targeting { get; }
        public DamageCalculator Damage { get; }
        public HealingCalculator Healing { get; }
        public BreakSystem Breaks { get; }
        public BattleEventBus Events { get; }
        public UltimateQueue Ultimates { get; }
        public CombatUnit CurrentActor => Turns.CurrentActor;
        public BattleOutcome Outcome => CalculateOutcome();
        public int RegularTurnsCompleted { get; private set; }

        public BattleCombatCore(IEnumerable<CombatUnit> combatants, int initialSkillPoints = 3,
            int maximumSkillPoints = 5, int? deterministicSeed = null)
        {
            units = combatants == null ? new List<CombatUnit>() : new List<CombatUnit>(combatants);
            units.RemoveAll(unit => unit == null);
            var random = deterministicSeed.HasValue ? new Random(deterministicSeed.Value) : new Random();
            Events = new BattleEventBus();
            Resources = new ResourceSystem(initialSkillPoints, maximumSkillPoints);
            Turns = new TurnManager(units);
            Targeting = new TargetingSystem(random);
            Damage = new DamageCalculator(random);
            Healing = new HealingCalculator();
            Breaks = new BreakSystem(Turns, Damage, Events);
            Ultimates = new UltimateQueue(10);
            enemyAI = new EnemyAIController(random);
            executor = new BattleActionExecutor(units, Resources, Targeting, Damage, Healing, Breaks, Events, random);
            Resources.SkillPointsChanged += (_, __) => Events.Publish(new ResourcesChangedEvent(Resources.SkillPoints));
        }

        public void Start()
        {
            Events.Publish(new BattleStartedEvent(units));
            PublishOutcomeIfChanged();
        }

        public bool AddUnit(CombatUnit unit)
        {
            if (unit == null || units.Contains(unit)) return false;
            units.Add(unit);
            Turns.AddUnit(unit);
            return true;
        }

        public CombatUnit NextActor()
        {
            while (Outcome == BattleOutcome.Ongoing)
            {
                CombatUnit actor = Turns.NextActor();
                if (actor == null) return null;
                IReadOnlyList<StatusTickResult> ticks = actor.ProcessStatusTicksAtActionStart();
                if (actor.IsAlive)
                {
                    Events.Publish(new TurnStartedEvent(actor));
                    return actor;
                }
                Events.Publish(new UnitDefeatedEvent(actor));
                Turns.CompleteRegularTurn(actor);
                PublishOutcomeIfChanged();
                if (ticks.Count == 0) return null;
            }
            return null;
        }

        public IReadOnlyList<CombatUnit> PeekNextActions(int count = 8) => Turns.PeekNextActions(count);

        public ActionResolution Execute(ActionRequest request)
        {
            if (request == null) return Failed(null, "Action request is null.");
            bool interrupt = request.IsUltimate || request.IsFollowUp || !request.ConsumesRegularTurn;
            if (!interrupt && !ReferenceEquals(request.Actor, Turns.CurrentActor))
                return Failed(request, "It is not this unit's turn.");
            ActionResolution resolution = executor.Execute(request);
            if (resolution.Success && request.ConsumesRegularTurn)
            {
                Turns.CompleteRegularTurn(request.Actor);
                RegularTurnsCompleted++;
            }
            PublishOutcomeIfChanged();
            return resolution;
        }

        public ActionRequest DecideEnemyAction(CombatUnit enemy = null)
        {
            enemy ??= CurrentActor;
            return enemyAI.Decide(enemy, units);
        }

        public bool QueueUltimate(ActionRequest request, out string failureReason)
        {
            bool queued = Ultimates.Enqueue(request, out failureReason);
            if (queued) Events.Publish(new ResourcesChangedEvent(Resources.SkillPoints, request.Actor));
            return queued;
        }

        public bool TryExecuteNextUltimate(out ActionResolution resolution)
        {
            resolution = null;
            if (!Ultimates.TryDequeue(out ActionRequest request)) return false;
            resolution = Execute(request);
            if (!resolution.Success && Ultimates.ReleaseReservation(request))
                Events.Publish(new ResourcesChangedEvent(Resources.SkillPoints, request.Actor));
            return true;
        }

        public BattleOutcome CalculateOutcome()
        {
            bool playerAlive = units.Exists(unit => unit.Team == BattleTeam.Player && unit.IsAlive);
            bool enemyAlive = units.Exists(unit => unit.Team == BattleTeam.Enemy && unit.IsAlive);
            if (!enemyAlive && playerAlive) return BattleOutcome.Victory;
            if (!playerAlive) return BattleOutcome.Defeat;
            return BattleOutcome.Ongoing;
        }

        void PublishOutcomeIfChanged()
        {
            BattleOutcome outcome = CalculateOutcome();
            if (outcome == BattleOutcome.Ongoing || outcome == lastPublishedOutcome) return;
            lastPublishedOutcome = outcome;
            Ultimates.Clear();
            Events.Publish(new BattleEndedEvent(outcome));
        }

        static ActionResolution Failed(ActionRequest request, string reason)
        {
            return new ActionResolution { Request = request, FailureReason = reason, Success = false };
        }
    }
}
