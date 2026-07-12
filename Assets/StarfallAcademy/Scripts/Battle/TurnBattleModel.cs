using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    /// <summary>
    /// Connects formation/progression/stage data to the reusable MVP combat core.
    /// Runtime battle state lives only in CombatUnit and BattleCombatCore.
    /// </summary>
    public sealed class TurnBattleModel
    {
        readonly List<CombatUnit> players = new List<CombatUnit>(FormationState.MaxMembers);
        readonly List<CombatUnit> enemies = new List<CombatUnit>(3);
        readonly List<CombatUnit> units = new List<CombatUnit>(FormationState.MaxMembers + 3);
        readonly HashSet<CombatUnit> phaseTwoApplied = new HashSet<CombatUnit>();
        readonly Dictionary<CombatUnit, int> heatStacks = new Dictionary<CombatUnit, int>();
        readonly Dictionary<Guid, bool> targetWasBroken = new Dictionary<Guid, bool>();
        readonly StageData stage;
        int emergencyHealsUsed;

        public IReadOnlyList<CombatUnit> Players => players;
        public IReadOnlyList<CombatUnit> Enemies => enemies;
        public IReadOnlyList<CombatUnit> Units => units;
        public BattleCombatCore Core { get; }
        public CombatUnit CurrentActor => Core.CurrentActor;
        public int SP => Core.Resources.SkillPoints;
        public int SkillPoints => SP;
        public BattleOutcome Outcome => Core.Outcome;
        public bool PlayersDefeated => Outcome == BattleOutcome.Defeat;
        public bool EnemiesDefeated => Outcome == BattleOutcome.Victory;

        public TurnBattleModel(FormationState formation, StageData stage, int? deterministicSeed = null)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            BuildPlayers(formation);
            BuildEnemies(stage);
            units.AddRange(players);
            units.AddRange(enemies);
            int seed = deterministicSeed ?? StableSeed(stage.Id);
            Core = new BattleCombatCore(units, stage.InitialSkillPoints,
                ResourceSystem.DefaultMaximumSkillPoints, seed);
            ApplyOpeningPassives();
            Core.Start();
        }

        public CombatUnit NextActor()
        {
            ApplyBossPhaseTwoIfNeeded();
            return Core.NextActor();
        }

        public IReadOnlyList<CombatUnit> Peek(int count = 8)
        {
            return Core.PeekNextActions(Mathf.Clamp(count, 0, 32));
        }

        public IReadOnlyList<CombatUnit> PeekNextActions(int count = 8) => Peek(count);

        public ActionRequest CreatePlayerAction(CombatUnit actor, BattleActionConfig config,
            CombatUnit target = null)
        {
            if (actor == null || actor.Team != BattleTeam.Player || !actor.IsAlive) return null;
            target ??= FindAutomaticTarget(actor, config.TargetType);
            ActionRequest request = ActionRequest.FromConfig(actor, config, target);
            request.SkillId = actor.Id + "." + config.Kind.ToString().ToLowerInvariant();
            request.Element = config.Element == BattleElement.Auto ? actor.Element : config.Element;
            ApplyCharacterActionRules(request, config);
            if (actor.CharacterData != null && actor.CharacterData.Role == CharacterRole.Striker)
            {
                int stacks = heatStacks.TryGetValue(actor, out int value) ? value : 0;
                if (request.DamageMultiplier > 0f) request.DamageMultiplier *= 1f + stacks * .06f;
                targetWasBroken[request.RequestId] = target != null && target.IsBroken;
            }
            return request;
        }

        public ActionRequest CreatePlayerAction(CombatUnit actor, BattleActionKind kind,
            CombatUnit target = null)
        {
            CharacterData character = actor?.CharacterData;
            if (character == null) return null;
            BattleActionConfig config = kind switch
            {
                BattleActionKind.Skill => character.SkillAction,
                BattleActionKind.Ultimate => character.UltimateAction,
                _ => character.BasicAction
            };
            return CreatePlayerAction(actor, config, target);
        }

        public ActionRequest CreateEnemyAction(CombatUnit actor = null)
        {
            ApplyBossPhaseTwoIfNeeded();
            actor ??= CurrentActor;
            if (actor == null || actor.Team != BattleTeam.Enemy || !actor.IsAlive) return null;
            return Core.DecideEnemyAction(actor);
        }

        public ActionResolution Execute(ActionRequest request)
        {
            ActionResolution resolution = Core.Execute(request);
            if (resolution.Success) ApplyPostActionRules(resolution);
            ApplyBossPhaseTwoIfNeeded();
            return resolution;
        }

        public bool QueueUltimate(ActionRequest request, out string failureReason)
        {
            if (request == null || request.Kind != BattleActionKind.Ultimate)
            {
                failureReason = "필살기 요청이 아닙니다.";
                return false;
            }
            return Core.QueueUltimate(request, out failureReason);
        }

        public bool QueueUltimate(CombatUnit actor, CombatUnit target, out string failureReason)
        {
            ActionRequest request = CreatePlayerAction(actor, BattleActionKind.Ultimate, target);
            return QueueUltimate(request, out failureReason);
        }

        public bool TryExecute(out ActionResolution resolution)
        {
            if (!Core.TryExecuteNextUltimate(out resolution)) return false;
            if (resolution != null && resolution.Success) ApplyPostActionRules(resolution);
            ApplyBossPhaseTwoIfNeeded();
            return true;
        }

        public bool TryExecuteNextUltimate(out ActionResolution resolution) => TryExecute(out resolution);

        public CombatUnit FindAutomaticTarget(CombatUnit actor, BattleTargetType targetType)
        {
            if (actor == null || !actor.IsAlive) return null;
            BattleTeam opposingTeam = actor.Team == BattleTeam.Player ? BattleTeam.Enemy : BattleTeam.Player;
            switch (targetType)
            {
                case BattleTargetType.Self:
                    return actor;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.LowestHpAlly:
                case BattleTargetType.AdjacentAllies:
                case BattleTargetType.AllAllies:
                    return LowestHpAlive(actor.Team);
                case BattleTargetType.SingleEnemy:
                case BattleTargetType.AdjacentEnemies:
                case BattleTargetType.AllEnemies:
                case BattleTargetType.RandomEnemy:
                    return BestOffensiveTarget(actor, opposingTeam);
                default:
                    return FirstAlive(opposingTeam);
            }
        }

        public CombatUnit AutoTarget(CombatUnit actor, BattleActionConfig config)
        {
            return FindAutomaticTarget(actor, config.TargetType);
        }

        void BuildPlayers(FormationState formation)
        {
            if (formation == null) return;
            int count = Mathf.Min(FormationState.MaxMembers, formation.Count);
            for (int slot = 0; slot < count; slot++)
            {
                CharacterData character = formation.Members[slot];
                if (character == null) continue;
                int power = CharacterProgressionService.GetCombatPower(character);
                var stats = new BattleBaseStats(
                    character.ResolveMaxHp(power),
                    character.ResolveAttack(power),
                    character.ResolveDefense(power),
                    character.ResolveSpeed(),
                    character.CritChance,
                    character.CritDamage,
                    character.ResolveMaxEnergy());
                var unit = new CombatUnit("player_" + character.Id + "_" + slot,
                    character.DisplayName, BattleTeam.Player, slot, stats, character,
                    character.Element);
                players.Add(unit);
            }
        }

        void BuildEnemies(StageData stageData)
        {
            StageEnemyEntry[] lineup = stageData.EnemyLineup;
            int count = Mathf.Min(3, lineup == null ? 0 : lineup.Length);
            for (int slot = 0; slot < count; slot++)
            {
                StageEnemyEntry entry = lineup[slot];
                if (entry == null) continue;
                var stats = new BattleBaseStats(entry.MaxHp, entry.Attack, entry.Defense,
                    entry.Speed, .05f, 1.5f, 100f)
                {
                    EffectResistance = entry.EffectResistance
                };
                bool isBoss = entry.Archetype == EnemyArchetype.BossObserver
                    || stageData.BossStage && slot == 0;
                var unit = new CombatUnit(stageData.Id + "_" + entry.Id + "_" + slot,
                    entry.DisplayName, BattleTeam.Enemy, slot, stats, null,
                    DefaultEnemyElement(entry.Archetype), entry.MaxBreak)
                {
                    Archetype = isBoss ? EnemyArchetype.BossObserver : entry.Archetype,
                    IsBoss = isBoss,
                    PhaseTwoThreshold = stageData.BossPhaseTwoThreshold,
                    DelayResistance = entry.DelayResistance
                };
                unit.SetWeaknesses(entry.Weaknesses);
                enemies.Add(unit);
            }
        }

        void ApplyCharacterActionRules(ActionRequest request, BattleActionConfig config)
        {
            CharacterData character = request.Actor.CharacterData;
            if (character == null) return;
            int skillLevel = CharacterProgressionService.GetSkillLevel(character);
            if (request.Kind == BattleActionKind.Skill && request.DamageMultiplier > 0f)
                request.DamageMultiplier *= 1f + Mathf.Max(0, skillLevel - 1) * .03f;

            if (character.Role == CharacterRole.Striker)
            {
                if (request.PrimaryTarget != null && request.PrimaryTarget.IsBroken)
                {
                    if (request.Kind == BattleActionKind.Skill) request.DamageMultiplier *= 1.3f;
                    if (request.Kind == BattleActionKind.Ultimate) request.DefenseIgnore = .2f;
                }
                return;
            }

            if (character.Role == CharacterRole.Support)
            {
                if (request.Kind == BattleActionKind.Skill)
                {
                    request.DamageMultiplier = 0f;
                    request.AddStatus(new StatusEffectInstance(StatusEffectType.AttackUp,
                        .25f, 2, request.Actor, StatusStackBehavior.RefreshDuration,
                        effectId: request.Actor.Id + ".support_skill"));
                }
                else if (request.Kind == BattleActionKind.Ultimate)
                {
                    request.AddStatus(new StatusEffectInstance(StatusEffectType.DamageUp,
                        .2f, 2, request.Actor, StatusStackBehavior.RefreshDuration,
                        effectId: request.Actor.Id + ".support_ultimate"));
                }
                return;
            }

            if (character.Role == CharacterRole.Healer)
            {
                if (request.Kind == BattleActionKind.Skill)
                {
                    request.HealingMaxHpMultiplier = config.HealingMultiplier;
                    request.HealingAttackMultiplier = 0f;
                }
                else if (request.Kind == BattleActionKind.Ultimate)
                    ConfigurePartyShield(request, config);
                return;
            }

            if (character.Role == CharacterRole.Tank
                && (request.Kind == BattleActionKind.Skill || request.Kind == BattleActionKind.Ultimate))
                ConfigurePartyShield(request, config);

            if (character.Role == CharacterRole.Special && request.Kind == BattleActionKind.Skill
                && request.TargetType == BattleTargetType.AdjacentEnemies)
            {
                request.SecondaryDamageMultiplier = .7f;
                request.SecondaryBreakDamage = Mathf.Max(1f, request.BreakDamage * .5f);
            }
        }

        void ConfigurePartyShield(ActionRequest request, BattleActionConfig config)
        {
            request.TargetType = BattleTargetType.AllAllies;
            request.DamageMultiplier = 0f;
            request.HealingAttackMultiplier = 0f;
            request.HealingMaxHpMultiplier = 0f;
            request.FixedHealing = 0f;
            request.ShieldAmount = 0f;
            float shield = request.Actor.MaxHp * Mathf.Max(.15f, config.HealingMultiplier)
                + config.FixedValue;
            request.AddStatus(new StatusEffectInstance(StatusEffectType.Shield, 0f, 2,
                request.Actor, StatusStackBehavior.RefreshDuration, effectId:
                request.Actor.Id + ".party_shield", flatValue: shield));
        }

        void ApplyPostActionRules(ActionResolution resolution)
        {
            ActionRequest request = resolution.Request;
            CharacterData character = request?.Actor?.CharacterData;
            ApplyEmergencyHealing(resolution);
            ClearHeatIfNoBrokenTargets();
            if (character == null) return;

            if (character.Role == CharacterRole.Striker
                && targetWasBroken.TryGetValue(request.RequestId, out bool wasBroken))
            {
                targetWasBroken.Remove(request.RequestId);
                if (wasBroken)
                {
                    int current = heatStacks.TryGetValue(request.Actor, out int stacks) ? stacks : 0;
                    heatStacks[request.Actor] = Mathf.Min(3, current + 1);
                }
            }

            if (character.Role == CharacterRole.Support && request.Kind == BattleActionKind.Skill)
            {
                foreach (CombatUnit target in resolution.Targets)
                    if (target.Team == BattleTeam.Player) Core.Turns.Advance(target, .2f);
            }
            else if (character.Role == CharacterRole.Support
                && request.Kind == BattleActionKind.Ultimate)
            {
                foreach (CombatUnit target in resolution.Targets)
                    if (target.Team == BattleTeam.Player) target.GainEnergy(10f);
            }
            else if (character.Role == CharacterRole.Special
                && request.Kind == BattleActionKind.Ultimate)
            {
                foreach (CombatUnit target in resolution.Targets)
                    if (target.Team == BattleTeam.Enemy) Core.Turns.Delay(target, .2f);
            }

            if (character.Role == CharacterRole.Special)
            {
                foreach (BreakResult breakResult in resolution.BreakResults)
                {
                    if (!breakResult.BreakTriggered) continue;
                    Core.Resources.GainSkillPoints(1);
                    break;
                }
            }
        }

        void ApplyOpeningPassives()
        {
            CombatUnit support = null;
            CombatUnit strongest = null;
            foreach (CombatUnit player in players)
            {
                if (player.CharacterData != null && player.CharacterData.Role == CharacterRole.Support)
                    support ??= player;
                if (strongest == null || player.Stats.Attack > strongest.Stats.Attack)
                    strongest = player;
            }
            if (support == null || strongest == null) return;
            Core.Turns.Advance(strongest, .15f);
            strongest.ApplyStatus(new StatusEffectInstance(StatusEffectType.AttackUp, .1f, 1,
                support, StatusStackBehavior.RefreshDuration, effectId: support.Id + ".opening_plan"));
        }

        void ApplyEmergencyHealing(ActionResolution resolution)
        {
            if (emergencyHealsUsed >= 2 || resolution?.Request?.Actor?.Team != BattleTeam.Enemy) return;
            CombatUnit healer = null;
            foreach (CombatUnit player in players)
                if (player.IsAlive && player.CharacterData != null
                    && player.CharacterData.Role == CharacterRole.Healer) { healer = player; break; }
            if (healer == null) return;
            foreach (DamageResult damage in resolution.DamageResults)
            {
                CombatUnit target = damage.Target;
                if (target == null || !target.IsAlive || target.Team != BattleTeam.Player
                    || target.HpRatio > .25f) continue;
                target.Heal(healer.MaxHp * .1f);
                emergencyHealsUsed++;
                if (emergencyHealsUsed >= 2) break;
            }
        }

        void ClearHeatIfNoBrokenTargets()
        {
            foreach (CombatUnit enemy in enemies)
                if (enemy.IsAlive && enemy.IsBroken) return;
            heatStacks.Clear();
        }

        void ApplyBossPhaseTwoIfNeeded()
        {
            if (!stage.BossPhaseTwoEnabled) return;
            foreach (CombatUnit boss in enemies)
            {
                if (!boss.IsBoss || !boss.IsAlive || phaseTwoApplied.Contains(boss)
                    || boss.HpRatio > stage.BossPhaseTwoThreshold) continue;
                float oldSpeed = boss.Stats.Speed;
                boss.BaseStats.Attack *= stage.BossPhaseTwoAttackMultiplier;
                boss.BaseStats.Speed += stage.BossPhaseTwoSpeedBonus;
                boss.SetPhase(2);
                boss.RecalculateStats();
                if (boss.Stats.Speed > 0f)
                    boss.SetActionValue(boss.ActionValue * oldSpeed / boss.Stats.Speed);
                phaseTwoApplied.Add(boss);
                SummonPhaseTwoEnemies(boss);
            }
        }

        void SummonPhaseTwoEnemies(CombatUnit boss)
        {
            int summonCount = Mathf.Min(stage.BossPhaseTwoSummonCount, 3 - enemies.Count);
            for (int i = 0; i < summonCount; i++)
            {
                int slot = enemies.Count;
                var stats = new BattleBaseStats(
                    Mathf.Max(650f, boss.MaxHp * .12f),
                    Mathf.Max(80f, boss.Stats.Attack * .5f),
                    Mathf.Max(40f, boss.Stats.Defense * .35f),
                    Mathf.Max(95f, boss.Stats.Speed - 5f), .05f, 1.5f, 100f);
                var summon = new CombatUnit(stage.Id + "_phase2_drone_" + slot,
                    "관측 드론 " + (i + 1), BattleTeam.Enemy, slot, stats, null,
                    BattleElement.Wind, 60f)
                {
                    Archetype = EnemyArchetype.Drone
                };
                summon.SetWeaknesses(new[] { BattleElement.Lightning, BattleElement.Light });
                enemies.Add(summon);
                units.Add(summon);
                Core.AddUnit(summon);
            }
        }

        CombatUnit BestOffensiveTarget(CombatUnit actor, BattleTeam team)
        {
            CombatUnit result = null;
            foreach (CombatUnit candidate in units)
            {
                if (candidate.Team != team || !candidate.IsAlive) continue;
                if (result == null || CompareOffensiveTargets(actor, candidate, result) < 0)
                    result = candidate;
            }
            return result;
        }

        static int CompareOffensiveTargets(CombatUnit actor, CombatUnit left, CombatUnit right)
        {
            int leftWeak = left.HasWeakness(actor.Element) ? 0 : 1;
            int rightWeak = right.HasWeakness(actor.Element) ? 0 : 1;
            if (leftWeak != rightWeak) return leftWeak.CompareTo(rightWeak);
            int leftBroken = left.IsBroken ? 0 : 1;
            int rightBroken = right.IsBroken ? 0 : 1;
            if (leftBroken != rightBroken) return leftBroken.CompareTo(rightBroken);
            int breakOrder = left.BreakCurrent.CompareTo(right.BreakCurrent);
            if (breakOrder != 0) return breakOrder;
            int hp = left.HpRatio.CompareTo(right.HpRatio);
            return hp != 0 ? hp : left.Slot.CompareTo(right.Slot);
        }

        CombatUnit LowestHpAlive(BattleTeam team)
        {
            CombatUnit result = null;
            foreach (CombatUnit unit in units)
            {
                if (unit.Team != team || !unit.IsAlive) continue;
                if (result == null || unit.HpRatio < result.HpRatio
                    || Mathf.Approximately(unit.HpRatio, result.HpRatio) && unit.Slot < result.Slot)
                    result = unit;
            }
            return result;
        }

        CombatUnit FirstAlive(BattleTeam team)
        {
            foreach (CombatUnit unit in units)
                if (unit.Team == team && unit.IsAlive) return unit;
            return null;
        }

        static BattleElement DefaultEnemyElement(EnemyArchetype archetype)
        {
            return archetype switch
            {
                EnemyArchetype.Defender => BattleElement.Light,
                EnemyArchetype.ElitePredator => BattleElement.Dark,
                EnemyArchetype.BossObserver => BattleElement.Dark,
                _ => BattleElement.Wind
            };
        }

        static int StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }
    }
}
