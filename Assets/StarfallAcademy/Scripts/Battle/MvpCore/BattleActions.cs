using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public sealed class ActionRequest
    {
        readonly List<CombatUnit> additionalTargets = new List<CombatUnit>();
        readonly List<StatusEffectInstance> statusEffects = new List<StatusEffectInstance>();

        public Guid RequestId { get; } = Guid.NewGuid();
        public CombatUnit Actor { get; set; }
        public string SkillId { get; set; }
        public string SkillName { get; set; }
        public BattleActionKind Kind { get; set; }
        public BattleSkillKind SkillKind => Kind.ToSkillKind();
        public BattleTargetType TargetType { get; set; } = BattleTargetType.SingleEnemy;
        public CombatUnit PrimaryTarget { get; set; }
        public IReadOnlyList<CombatUnit> AdditionalTargets => additionalTargets;
        public IReadOnlyList<StatusEffectInstance> StatusEffects => statusEffects;
        public BattleElement Element { get; set; } = BattleElement.Auto;
        public int SkillPointCost { get; set; }
        public float EnergyCost { get; set; }
        public float EnergyGain { get; set; }
        public float DamageMultiplier { get; set; }
        public float? SecondaryDamageMultiplier { get; set; }
        public float FixedDamage { get; set; }
        public float HealingAttackMultiplier { get; set; }
        public float HealingMaxHpMultiplier { get; set; }
        public float FixedHealing { get; set; }
        public float ShieldAmount { get; set; }
        public float BreakDamage { get; set; }
        public float? SecondaryBreakDamage { get; set; }
        public float DefenseIgnore { get; set; }
        public bool CanCrit { get; set; } = true;
        public bool IsUltimate => Kind == BattleActionKind.Ultimate;
        public bool IsFollowUp { get; set; }
        public bool ConsumesRegularTurn { get; set; } = true;
        public bool ResourcesReserved { get; internal set; }
        public bool? WarningStateAfterResolution { get; set; }
        public string WarningText { get; set; }
        public int QueueSequence { get; internal set; }

        public ActionRequest()
        {
        }

        public ActionRequest(CombatUnit actor, BattleActionKind kind, BattleTargetType targetType,
            CombatUnit primaryTarget = null)
        {
            Actor = actor;
            Kind = kind;
            TargetType = targetType;
            PrimaryTarget = primaryTarget;
        }

        public ActionRequest(CombatUnit actor, BattleSkillKind kind, BattleTargetType targetType,
            CombatUnit primaryTarget = null) : this(actor, kind.ToActionKind(), targetType, primaryTarget)
        {
        }

        public void AddTarget(CombatUnit target)
        {
            if (target != null && !additionalTargets.Contains(target)) additionalTargets.Add(target);
        }

        public void AddStatus(StatusEffectInstance effect)
        {
            if (effect != null) statusEffects.Add(effect);
        }

        public static ActionRequest FromConfig(CombatUnit actor, BattleActionConfig config,
            CombatUnit primaryTarget = null)
        {
            bool heals = config.HealingMultiplier > 0f;
            return new ActionRequest(actor, config.Kind, config.TargetType, primaryTarget)
            {
                SkillName = config.Name,
                Element = config.Element,
                SkillPointCost = config.SkillPointCost,
                EnergyCost = config.EnergyCost,
                EnergyGain = config.EnergyGain,
                DamageMultiplier = config.DamageMultiplier,
                HealingAttackMultiplier = config.HealingMultiplier,
                FixedDamage = heals ? 0f : config.FixedValue,
                FixedHealing = heals ? config.FixedValue : 0f,
                BreakDamage = config.BreakDamage,
                ConsumesRegularTurn = config.Kind != BattleActionKind.Ultimate
            };
        }
    }

    public sealed class ActionResolution
    {
        readonly List<CombatUnit> targets = new List<CombatUnit>();
        readonly List<DamageResult> damageResults = new List<DamageResult>();
        readonly List<HealingResult> healingResults = new List<HealingResult>();
        readonly List<BreakResult> breakResults = new List<BreakResult>();
        readonly List<StatusApplyResult> statusResults = new List<StatusApplyResult>();
        readonly List<CombatUnit> defeatedUnits = new List<CombatUnit>();

        public ActionRequest Request { get; internal set; }
        public bool Success { get; internal set; }
        public string FailureReason { get; internal set; }
        public IReadOnlyList<CombatUnit> Targets => targets;
        public IReadOnlyList<DamageResult> DamageResults => damageResults;
        public IReadOnlyList<HealingResult> HealingResults => healingResults;
        public IReadOnlyList<BreakResult> BreakResults => breakResults;
        public IReadOnlyList<StatusApplyResult> StatusResults => statusResults;
        public IReadOnlyList<CombatUnit> DefeatedUnits => defeatedUnits;
        public int SkillPointsBefore { get; internal set; }
        public int SkillPointsAfter { get; internal set; }
        public float EnergyBefore { get; internal set; }
        public float EnergyAfter { get; internal set; }

        internal List<CombatUnit> MutableTargets => targets;
        internal List<DamageResult> MutableDamageResults => damageResults;
        internal List<HealingResult> MutableHealingResults => healingResults;
        internal List<BreakResult> MutableBreakResults => breakResults;
        internal List<StatusApplyResult> MutableStatusResults => statusResults;
        internal List<CombatUnit> MutableDefeatedUnits => defeatedUnits;
    }

    public sealed class BattleActionExecutor
    {
        readonly IReadOnlyList<CombatUnit> units;
        readonly ResourceSystem resources;
        readonly TargetingSystem targeting;
        readonly DamageCalculator damageCalculator;
        readonly HealingCalculator healingCalculator;
        readonly BreakSystem breakSystem;
        readonly BattleEventBus eventBus;
        readonly Random random;

        public BattleActionExecutor(IReadOnlyList<CombatUnit> units, ResourceSystem resources,
            TargetingSystem targeting, DamageCalculator damageCalculator,
            HealingCalculator healingCalculator, BreakSystem breakSystem,
            BattleEventBus eventBus = null, Random random = null)
        {
            this.units = units ?? throw new ArgumentNullException(nameof(units));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            this.damageCalculator = damageCalculator ?? throw new ArgumentNullException(nameof(damageCalculator));
            this.healingCalculator = healingCalculator ?? throw new ArgumentNullException(nameof(healingCalculator));
            this.breakSystem = breakSystem ?? throw new ArgumentNullException(nameof(breakSystem));
            this.eventBus = eventBus;
            this.random = random ?? new Random();
        }

        public ActionResolution Execute(ActionRequest request)
        {
            var resolution = new ActionResolution { Request = request };
            if (!Validate(request, resolution)) return resolution;
            CombatUnit actor = request.Actor;
            resolution.SkillPointsBefore = resources.SkillPoints;
            resolution.EnergyBefore = actor.Energy;

            IReadOnlyList<CombatUnit> resolved = targeting.Resolve(request.TargetType, actor,
                request.PrimaryTarget, units);
            foreach (CombatUnit target in resolved)
                if (!resolution.MutableTargets.Contains(target)) resolution.MutableTargets.Add(target);
            foreach (CombatUnit target in request.AdditionalTargets)
                if (target != null && target.IsAlive && !resolution.MutableTargets.Contains(target)) resolution.MutableTargets.Add(target);
            if (resolution.MutableTargets.Count == 0)
                return Fail(resolution, "No valid target.");

            if (!request.ResourcesReserved)
            {
                if (request.SkillPointCost > 0 && !resources.TrySpendSkillPoints(request.SkillPointCost))
                    return Fail(resolution, "Not enough skill points.");
                if (request.EnergyCost > 0f && !actor.TrySpendEnergy(request.EnergyCost))
                {
                    if (request.SkillPointCost > 0) resources.GainSkillPoints(request.SkillPointCost);
                    return Fail(resolution, "Not enough energy.");
                }
            }

            for (int i = 0; i < resolution.MutableTargets.Count; i++)
            {
                CombatUnit target = resolution.MutableTargets[i];
                bool secondary = !ReferenceEquals(target, request.PrimaryTarget) && resolution.MutableTargets.Count > 1;
                float damageMultiplier = secondary && request.SecondaryDamageMultiplier.HasValue
                    ? request.SecondaryDamageMultiplier.Value : request.DamageMultiplier;
                if (damageMultiplier > 0f || request.FixedDamage > 0f)
                {
                    DamageResult damage = damageCalculator.CalculateAndApply(new DamageRequest
                    {
                        Attacker = actor,
                        Target = target,
                        Element = request.Element,
                        SkillMultiplier = damageMultiplier,
                        FixedDamage = request.FixedDamage,
                        DefenseIgnore = request.DefenseIgnore,
                        CanCrit = request.CanCrit
                    });
                    resolution.MutableDamageResults.Add(damage);
                    eventBus?.Publish(new DamageDealtEvent(damage));
                    if (damage.Application != null && damage.Application.HpDamage > 0
                        && target.Team == BattleTeam.Player)
                        target.GainEnergy(5f + random.Next(0, 6));
                    if (!target.IsAlive && !resolution.MutableDefeatedUnits.Contains(target))
                    {
                        resolution.MutableDefeatedUnits.Add(target);
                        if (actor.Team == BattleTeam.Player) actor.GainEnergy(10f);
                        eventBus?.Publish(new UnitDefeatedEvent(target));
                    }
                }

                if (target.IsAlive && (request.HealingAttackMultiplier > 0f
                    || request.HealingMaxHpMultiplier > 0f || request.FixedHealing > 0f))
                {
                    HealingResult healing = healingCalculator.CalculateAndApply(new HealingRequest
                    {
                        Source = actor,
                        Target = target,
                        AttackMultiplier = request.HealingAttackMultiplier,
                        MaxHpMultiplier = request.HealingMaxHpMultiplier,
                        FixedHealing = request.FixedHealing
                    });
                    resolution.MutableHealingResults.Add(healing);
                    eventBus?.Publish(new HealingDoneEvent(healing));
                }

                if (target.IsAlive && request.ShieldAmount > 0f) target.AddShield(request.ShieldAmount);

                if (target.IsAlive && request.BreakDamage > 0f)
                {
                    float breakValue = secondary && request.SecondaryBreakDamage.HasValue
                        ? request.SecondaryBreakDamage.Value : request.BreakDamage;
                    BreakResult breakResult = breakSystem.ApplyBreakDamage(actor, target,
                        request.Element == BattleElement.Auto ? actor.Element : request.Element, breakValue);
                    resolution.MutableBreakResults.Add(breakResult);
                }

                if (target.IsAlive)
                {
                    foreach (StatusEffectInstance template in request.StatusEffects)
                    {
                        float chance = BattleMath.Clamp01(template.ApplyChance * (1f + actor.Stats.EffectHit)
                            * (1f - target.Stats.EffectResistance));
                        if (random.NextDouble() > chance) continue;
                        StatusApplyResult status = target.ApplyStatus(template);
                        resolution.MutableStatusResults.Add(status);
                        if (status.Applied) eventBus?.Publish(new StatusAppliedEvent(target, status));
                    }
                }
            }

            if (request.SkillPointCost < 0) resources.GainSkillPoints(-request.SkillPointCost);
            if (request.EnergyGain > 0f) actor.GainEnergy(request.EnergyGain);
            if (request.WarningStateAfterResolution.HasValue)
                actor.SetWarning(request.WarningStateAfterResolution.Value, request.WarningText);
            request.ResourcesReserved = false;
            resolution.Success = true;
            resolution.SkillPointsAfter = resources.SkillPoints;
            resolution.EnergyAfter = actor.Energy;
            eventBus?.Publish(new ResourcesChangedEvent(resources.SkillPoints, actor));
            eventBus?.Publish(new ActionResolvedEvent(resolution));
            return resolution;
        }

        bool Validate(ActionRequest request, ActionResolution resolution)
        {
            if (request == null) { resolution.FailureReason = "Action request is null."; return false; }
            if (request.Actor == null || !request.Actor.IsAlive) { resolution.FailureReason = "Actor cannot act."; return false; }
            if (!request.ResourcesReserved && request.SkillPointCost > 0
                && !resources.CanSpendSkillPoints(request.SkillPointCost))
            { resolution.FailureReason = "Not enough skill points."; return false; }
            if (!request.ResourcesReserved && request.EnergyCost > request.Actor.Energy + .0001f)
            { resolution.FailureReason = "Not enough energy."; return false; }
            return true;
        }

        static ActionResolution Fail(ActionResolution resolution, string reason)
        {
            resolution.Success = false;
            resolution.FailureReason = reason;
            resolution.SkillPointsAfter = resolution.SkillPointsBefore;
            resolution.EnergyAfter = resolution.Request?.Actor?.Energy ?? 0f;
            return resolution;
        }
    }

    public sealed class UltimateQueue
    {
        readonly Queue<ActionRequest> queue = new Queue<ActionRequest>();
        int sequence;

        public int Count => queue.Count;
        public int MaximumChain { get; }
        public IReadOnlyCollection<ActionRequest> Pending => queue.ToArray();

        public UltimateQueue(int maximumChain = 10)
        {
            MaximumChain = Math.Max(1, maximumChain);
        }

        public bool Enqueue(ActionRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if (request?.Actor == null || !request.Actor.IsAlive)
            { failureReason = "Ultimate actor cannot act."; return false; }
            if (!request.IsUltimate)
            { failureReason = "Only ultimate actions can enter this queue."; return false; }
            if (queue.Count >= MaximumChain)
            { failureReason = "Ultimate chain limit reached."; return false; }
            if (request.ResourcesReserved || ContainsActor(request.Actor))
            { failureReason = "This actor already has a queued ultimate."; return false; }
            float cost = request.EnergyCost > 0f ? request.EnergyCost : request.Actor.MaxEnergy;
            if (!request.Actor.TrySpendEnergy(cost))
            { failureReason = "Not enough energy."; return false; }
            request.ResourcesReserved = true;
            request.EnergyCost = cost;
            request.ConsumesRegularTurn = false;
            request.QueueSequence = ++sequence;
            queue.Enqueue(request);
            return true;
        }

        public bool TryDequeue(out ActionRequest request)
        {
            request = queue.Count > 0 ? queue.Dequeue() : null;
            return request != null;
        }

        public void Clear(bool refundReservedEnergy = true)
        {
            while (queue.Count > 0)
            {
                ActionRequest request = queue.Dequeue();
                if (refundReservedEnergy && request.ResourcesReserved && request.Actor != null)
                    request.Actor.GainEnergy(request.EnergyCost);
                request.ResourcesReserved = false;
            }
        }

        bool ContainsActor(CombatUnit actor)
        {
            foreach (ActionRequest request in queue) if (ReferenceEquals(request.Actor, actor)) return true;
            return false;
        }
    }
}
