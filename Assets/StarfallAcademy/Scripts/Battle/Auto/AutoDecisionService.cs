using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public sealed class AutoActionDecision
    {
        public ActionRequest Request { get; }
        public string Reason { get; }
        public bool HasAction => Request != null;

        public AutoActionDecision(ActionRequest request, string reason)
        {
            Request = request;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class AutoUltimateDecision
    {
        public ActionRequest Request { get; }
        public string Reason { get; }
        public string FailureReason { get; }
        public bool Queued { get; }

        public AutoUltimateDecision(ActionRequest request, bool queued, string reason,
            string failureReason = "")
        {
            Request = request;
            Queued = queued;
            Reason = reason ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }
    }

    /// <summary>
    /// Chooses actions without resolving combat. Every returned regular action is created through
    /// TurnBattleModel.CreatePlayerAction, and every accepted ultimate is queued through
    /// TurnBattleModel.QueueUltimate, exactly like manual input.
    /// </summary>
    public sealed class AutoDecisionService
    {
        readonly TurnBattleModel battle;

        public AutoDecisionService(TurnBattleModel battle)
        {
            this.battle = battle ?? throw new ArgumentNullException(nameof(battle));
        }

        public AutoActionDecision DecideRegularAction(CombatUnit actor, AutoBattlePreset preset)
        {
            if (actor?.CharacterData == null || !actor.IsAlive || actor.Team != BattleTeam.Player)
                return new AutoActionDecision(null, "행동 가능한 아군이 아닙니다");
            if (!ReferenceEquals(battle.CurrentActor, actor))
                return new AutoActionDecision(null, "현재 행동 순서가 아닙니다");

            BattleActionConfig basicConfig = actor.CharacterData.BasicAction;
            BattleActionConfig skillConfig = actor.CharacterData.SkillAction;
            CombatUnit basicTarget = SelectTarget(battle.Players, battle.Enemies,
                actor, basicConfig, preset);
            CombatUnit skillTarget = SelectTarget(battle.Players, battle.Enemies,
                actor, skillConfig, preset);
            bool basicValid = HasValidTarget(actor, basicConfig.TargetType, basicTarget);
            bool skillValid = HasValidTarget(actor, skillConfig.TargetType, skillTarget);

            if (skillValid && skillConfig.SkillPointCost > battle.SkillPoints)
                return basicValid
                    ? CreateDecision(actor, basicConfig, basicTarget, "SP가 부족해 일반 공격으로 전환")
                    : new AutoActionDecision(null, "SP가 부족하고 유효한 기본 대상도 없습니다");

            if (skillValid && IsRecoveryConfig(actor, skillConfig))
            {
                float threshold = RegularRecoveryThreshold(preset);
                if (PartyNeedsRecovery(skillConfig.TargetType, skillTarget, actor, threshold))
                    return CreateDecision(actor, skillConfig, skillTarget,
                        "아군 체력 위험 감지 · " + TargetName(skillTarget) + " 회복");
                return basicValid
                    ? CreateDecision(actor, basicConfig, basicTarget, "회복 중복을 피하고 SP를 회복")
                    : new AutoActionDecision(null, "회복이 필요하지 않고 유효한 공격 대상이 없습니다");
            }

            if (skillValid && IsDefensiveConfig(actor, skillConfig))
            {
                if (ShouldUseDefensiveSkill(actor, skillConfig, skillTarget, preset,
                    out string defenseReason))
                    return CreateDecision(actor, skillConfig, skillTarget, defenseReason);
                return basicValid
                    ? CreateDecision(actor, basicConfig, basicTarget, "보호 효과 중복을 피하고 SP를 회복")
                    : new AutoActionDecision(null, "보호 효과가 불필요하고 유효한 공격 대상이 없습니다");
            }

            if (skillValid && IsOffensiveConfig(skillConfig)
                && ShouldUseOffensiveSkill(actor, basicConfig, skillConfig, skillTarget,
                    preset, out string offenseReason))
                return CreateDecision(actor, skillConfig, skillTarget, offenseReason);

            if (basicValid)
                return CreateDecision(actor, basicConfig, basicTarget,
                    BasicAttackReason(actor, skillConfig, preset));
            if (skillValid)
                return CreateDecision(actor, skillConfig, skillTarget,
                    "기본 대상이 없어 유효한 스킬을 선택");
            return new AutoActionDecision(null, "유효한 행동 대상이 없습니다");
        }

        public AutoUltimateDecision TryQueueUltimate(CombatUnit actor, AutoBattlePreset preset)
        {
            if (actor?.CharacterData == null || !actor.IsAlive || actor.Team != BattleTeam.Player)
                return new AutoUltimateDecision(null, false, string.Empty, "행동 가능한 아군이 아닙니다");
            if (IsUltimateQueued(actor))
                return new AutoUltimateDecision(null, false, string.Empty, "이미 필살기가 대기 중입니다");

            BattleActionConfig config = actor.CharacterData.UltimateAction;
            float energyCost = config.EnergyCost > 0 ? config.EnergyCost : actor.MaxEnergy;
            if (!actor.CanUseUltimate(energyCost))
                return new AutoUltimateDecision(null, false, string.Empty, "필살기 에너지가 부족합니다");

            CombatUnit target = SelectTarget(battle.Players, battle.Enemies, actor, config, preset);
            if (!HasValidTarget(actor, config.TargetType, target))
                return new AutoUltimateDecision(null, false, string.Empty, "유효한 필살기 대상이 없습니다");
            if (IsDuplicateDefensiveUltimate(actor, config))
                return new AutoUltimateDecision(null, false, string.Empty, "회복·보호 필살기 중복을 보류합니다");
            if (!ShouldQueueUltimate(actor, config, target, preset, out string reason))
                return new AutoUltimateDecision(null, false, string.Empty, "현재 프리셋의 사용 조건을 기다립니다");

            ActionRequest request = battle.CreatePlayerAction(actor, config, target);
            if (!HasValidTargets(request))
                return new AutoUltimateDecision(request, false, string.Empty, "유효한 필살기 요청을 만들 수 없습니다");

            if (!battle.QueueUltimate(request, out string failureReason))
                return new AutoUltimateDecision(request, false, string.Empty, failureReason);
            return new AutoUltimateDecision(request, true, reason);
        }

        AutoActionDecision CreateDecision(CombatUnit actor, BattleActionConfig config,
            CombatUnit target, string reason)
        {
            ActionRequest request = battle.CreatePlayerAction(actor, config, target);
            return HasValidTargets(request)
                ? new AutoActionDecision(request, reason)
                : new AutoActionDecision(null, "유효한 행동 요청을 만들 수 없습니다");
        }

        bool ShouldUseOffensiveSkill(CombatUnit actor, BattleActionConfig basic,
            BattleActionConfig skill, CombatUnit target, AutoBattlePreset preset, out string reason)
        {
            reason = string.Empty;
            int enemyCount = AliveCount(battle.Enemies);
            bool weakness = IsWeaknessHit(actor, skill, target);
            bool breaksNow = CanBreak(actor, skill, target);
            bool multiTarget = enemyCount >= 3 && HitsMultipleEnemies(skill.TargetType);
            bool finishing = target != null && target.HpRatio <= .24f;
            bool boss = target != null && target.IsBoss;
            bool warning = target != null && target.Warning;
            int cadence = actor.RegularActionsCompleted + actor.Slot;

            switch (preset)
            {
                case AutoBattlePreset.OffenseFirst:
                    reason = finishing ? "처치권 적에게 고화력 스킬 집중"
                        : multiTarget ? "적 3체 이상 · 광역 스킬 우선"
                        : boss ? "보스에게 공격 스킬 집중" : "공격 우선 프리셋 · 스킬 사용";
                    return true;
                case AutoBattlePreset.BreakFirst:
                    if (breaksNow)
                    {
                        reason = "약점 적의 격파 게이지를 이번 행동에 소진";
                        return true;
                    }
                    if (weakness && skill.BreakDamage > basic.BreakDamage)
                    {
                        reason = "약점 적에게 격파 수치가 높은 스킬 선택";
                        return true;
                    }
                    if (multiTarget)
                    {
                        reason = "적 3체 이상 · 다중 격파 시도";
                        return true;
                    }
                    return false;
                case AutoBattlePreset.SurvivalFirst:
                    if (MinimumHpRatio(battle.Players) < .56f) return false;
                    if (breaksNow || warning)
                    {
                        reason = breaksNow ? "위협 적을 격파해 피해를 예방" : "예고 공격 적을 우선 제압";
                        return true;
                    }
                    if (finishing && battle.SkillPoints - skill.SkillPointCost >= 1)
                    {
                        reason = "처치 가능한 적을 제거해 생존 부담 감소";
                        return true;
                    }
                    return false;
                case AutoBattlePreset.PreserveUltimate:
                    if (breaksNow || multiTarget || boss && cadence % 2 == 0)
                    {
                        reason = breaksNow ? "필살기를 보존하고 스킬로 격파"
                            : multiTarget ? "필살기를 보존하고 광역 스킬 사용"
                            : "필살기를 보존하며 보스에게 전투 스킬 사용";
                        return true;
                    }
                    return false;
                default:
                    if (breaksNow || multiTarget || cadence % 3 == 1)
                    {
                        reason = breaksNow ? "약점 격파 가능"
                            : multiTarget ? "적 3체 이상 · 광역 효율 확보"
                            : "SP와 행동 주기를 고려한 균형 스킬";
                        return true;
                    }
                    return false;
            }
        }

        bool ShouldUseDefensiveSkill(CombatUnit actor, BattleActionConfig config,
            CombatUnit target, AutoBattlePreset preset, out string reason)
        {
            reason = string.Empty;
            bool warning = AnyEnemyWarning(battle.Enemies);
            bool boss = AnyAliveBoss(battle.Enemies);
            int enemyCount = AliveCount(battle.Enemies);
            float minimumHp = MinimumHpRatio(battle.Players);
            bool usefulEffect = HasMissingDefensiveEffect(actor, config, target);
            if (!usefulEffect) return false;

            bool shouldUse = preset switch
            {
                AutoBattlePreset.OffenseFirst => actor.CharacterData != null
                    && actor.CharacterData.Role == CharacterRole.Support,
                AutoBattlePreset.BreakFirst => warning || enemyCount >= 3,
                AutoBattlePreset.SurvivalFirst => minimumHp <= .86f || warning || boss,
                AutoBattlePreset.PreserveUltimate => minimumHp <= .7f || warning,
                _ => minimumHp <= .76f || warning || boss && actor.RegularActionsCompleted % 2 == 0
            };
            if (!shouldUse) return false;
            reason = warning ? "적 강공격 예고 · 보호 효과 전개"
                : minimumHp <= .7f ? "아군 체력 저하 · 생존 효과 우선"
                : enemyCount >= 3 ? "적 3체 이상 · 파티 보호"
                : "중복되지 않는 강화·보호 효과 적용";
            return true;
        }

        bool ShouldQueueUltimate(CombatUnit actor, BattleActionConfig config, CombatUnit target,
            AutoBattlePreset preset, out string reason)
        {
            reason = string.Empty;
            if (IsRecoveryConfig(actor, config))
            {
                float threshold = UltimateRecoveryThreshold(preset);
                int critical = CountAlliesBelow(battle.Players, threshold);
                bool needsRecovery = PartyNeedsRecovery(config.TargetType, target, actor, threshold)
                    || critical >= (preset == AutoBattlePreset.PreserveUltimate ? 2 : 1);
                if (!needsRecovery) return false;
                reason = critical >= 2 ? "아군 2명 이상 위험 · 회복 필살기"
                    : "아군 치명상 감지 · 회복 필살기";
                return true;
            }

            if (IsDefensiveConfig(actor, config))
            {
                if (!HasMissingDefensiveEffect(actor, config, target)) return false;
                bool warning = AnyEnemyWarning(battle.Enemies);
                bool boss = AnyAliveBoss(battle.Enemies);
                int enemyCount = AliveCount(battle.Enemies);
                float minimumHp = MinimumHpRatio(battle.Players);
                bool use = preset switch
                {
                    AutoBattlePreset.OffenseFirst => boss || enemyCount >= 3,
                    AutoBattlePreset.BreakFirst => warning || boss,
                    AutoBattlePreset.SurvivalFirst => minimumHp <= .82f || warning || boss,
                    AutoBattlePreset.PreserveUltimate => minimumHp <= .42f || warning && boss,
                    _ => minimumHp <= .68f || warning || boss && enemyCount >= 2
                };
                if (!use) return false;
                reason = warning ? "강공격 예고 대응 · 보호 필살기"
                    : minimumHp <= .55f ? "파티 생존 위험 · 보호 필살기"
                    : enemyCount >= 3 ? "적 3체 이상 · 파티 강화 필살기"
                    : "보스전 파티 강화 필살기";
                return true;
            }

            int aliveEnemies = AliveCount(battle.Enemies);
            bool bossTarget = target != null && target.IsBoss;
            bool bossWindow = bossTarget && (target.IsBroken || target.Phase >= 2
                || target.HpRatio <= .5f || target.Warning);
            bool multiTarget = aliveEnemies >= 3 && HitsMultipleEnemies(config.TargetType);
            bool breakWindow = CanBreak(actor, config, target) || target != null && target.IsBroken;
            bool finishing = target != null && target.HpRatio <= .2f;

            bool shouldQueue = preset switch
            {
                AutoBattlePreset.OffenseFirst => true,
                AutoBattlePreset.BreakFirst => breakWindow || bossTarget || multiTarget,
                AutoBattlePreset.SurvivalFirst => target != null && target.Warning
                    || MinimumHpRatio(battle.Players) <= .5f || multiTarget || bossWindow,
                AutoBattlePreset.PreserveUltimate => bossWindow || multiTarget
                    || bossTarget && finishing,
                _ => bossTarget || multiTarget || breakWindow || finishing
            };
            if (!shouldQueue) return false;
            reason = multiTarget ? "적 3체 이상 · 광역 필살기 효율"
                : bossWindow ? "보스 핵심 구간 · 필살기 사용"
                : breakWindow ? "격파 또는 격파 피해 구간 · 필살기 사용"
                : finishing ? "처치권 적 마무리 · 필살기 사용"
                : preset == AutoBattlePreset.OffenseFirst
                    ? "공격 우선 프리셋 · 준비 즉시 필살기" : "전투 우세 확보 · 필살기 사용";
            return true;
        }

        bool IsDuplicateDefensiveUltimate(CombatUnit actor, BattleActionConfig config)
        {
            if (!IsDefensiveConfig(actor, config)) return false;
            bool recovery = IsRecoveryConfig(actor, config);
            StatusEffectType? expectedStatus = ExpectedDefensiveStatus(actor, config);
            foreach (ActionRequest pending in battle.Core.Ultimates.Pending)
            {
                if (pending == null || !IsDefensiveAction(pending)) continue;
                if (recovery && IsRecoveryAction(pending)) return true;
                if (!expectedStatus.HasValue) continue;
                foreach (StatusEffectInstance pendingEffect in pending.StatusEffects)
                    if (pendingEffect.Type == expectedStatus.Value) return true;
            }
            return false;
        }

        bool IsUltimateQueued(CombatUnit actor)
        {
            foreach (ActionRequest request in battle.Core.Ultimates.Pending)
                if (request != null && ReferenceEquals(request.Actor, actor)) return true;
            return false;
        }

        bool HasValidTarget(CombatUnit actor, BattleTargetType targetType, CombatUnit target)
        {
            if (actor == null || !actor.IsAlive) return false;
            switch (targetType)
            {
                case BattleTargetType.SingleEnemy:
                case BattleTargetType.AdjacentEnemies:
                    return target != null && target.IsAlive && target.Team != actor.Team;
                case BattleTargetType.AllEnemies:
                case BattleTargetType.RandomEnemy:
                    return AliveCount(battle.Enemies) > 0;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.AdjacentAllies:
                    return target != null && target.IsAlive && target.Team == actor.Team;
                case BattleTargetType.AllAllies:
                case BattleTargetType.LowestHpAlly:
                    return AliveCount(battle.Players) > 0;
                case BattleTargetType.Self:
                    return actor.IsAlive;
                default:
                    return false;
            }
        }

        bool HasValidTargets(ActionRequest request)
        {
            if (request?.Actor == null || !request.Actor.IsAlive) return false;
            switch (request.TargetType)
            {
                case BattleTargetType.SingleEnemy:
                case BattleTargetType.AdjacentEnemies:
                    return request.PrimaryTarget != null && request.PrimaryTarget.IsAlive
                        && request.PrimaryTarget.Team != request.Actor.Team;
                case BattleTargetType.AllEnemies:
                case BattleTargetType.RandomEnemy:
                    return AliveCount(battle.Enemies) > 0;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.AdjacentAllies:
                    return request.PrimaryTarget != null && request.PrimaryTarget.IsAlive
                        && request.PrimaryTarget.Team == request.Actor.Team;
                case BattleTargetType.AllAllies:
                case BattleTargetType.LowestHpAlly:
                    return AliveCount(battle.Players) > 0;
                case BattleTargetType.Self:
                    return request.Actor.IsAlive;
                default:
                    return false;
            }
        }

        bool PartyNeedsRecovery(BattleTargetType targetType, CombatUnit primaryTarget,
            CombatUnit actor, float threshold)
        {
            List<CombatUnit> targets = ResolveAlliedTargets(targetType, primaryTarget,
                actor, battle.Players);
            if (targets.Count == 0) return false;
            float missingTotal = 0f;
            int moderatelyInjured = 0;
            foreach (CombatUnit target in targets)
            {
                if (target.HpRatio <= threshold) return true;
                missingTotal += 1f - target.HpRatio;
                if (target.HpRatio < .82f) moderatelyInjured++;
            }
            return targets.Count > 1 && (moderatelyInjured >= 2 || missingTotal >= .65f);
        }

        bool HasMissingDefensiveEffect(CombatUnit actor, BattleActionConfig config,
            CombatUnit primaryTarget)
        {
            List<CombatUnit> targets = ResolveAlliedTargets(config.TargetType, primaryTarget,
                actor, battle.Players);
            if (targets.Count == 0) return false;
            StatusEffectType? expected = ExpectedDefensiveStatus(actor, config);
            if (!expected.HasValue) return true;
            if (expected.Value == StatusEffectType.Shield)
            {
                foreach (CombatUnit target in targets)
                    if (target.Shield / Mathf.Max(1f, target.MaxHp) < .08f) return true;
                return false;
            }
            string expectedId = ExpectedDefensiveEffectId(actor, config);
            foreach (CombatUnit target in targets)
            {
                bool found = false;
                foreach (StatusEffectInstance current in target.Statuses)
                {
                    if (current.Type != expected.Value) continue;
                    if (!string.IsNullOrWhiteSpace(expectedId)
                        && !string.Equals(current.EffectId, expectedId, StringComparison.Ordinal)) continue;
                    found = true;
                    break;
                }
                if (!found) return true;
            }
            return false;
        }

        static bool IsRecoveryConfig(CombatUnit actor, BattleActionConfig config)
        {
            CharacterRole? role = actor?.CharacterData?.Role;
            if (role == CharacterRole.Tank || role == CharacterRole.Support) return false;
            if (role == CharacterRole.Healer && config.Kind == BattleActionKind.Ultimate) return false;
            return IsAllyTarget(config.TargetType) && config.HealingMultiplier > 0f;
        }

        static bool IsDefensiveConfig(CombatUnit actor, BattleActionConfig config)
        {
            if (IsRecoveryConfig(actor, config)) return true;
            CharacterRole? role = actor?.CharacterData?.Role;
            if (config.Kind == BattleActionKind.Skill || config.Kind == BattleActionKind.Ultimate)
            {
                if (role == CharacterRole.Support || role == CharacterRole.Tank) return true;
                if (role == CharacterRole.Healer && config.Kind == BattleActionKind.Ultimate) return true;
            }
            return IsAllyTarget(config.TargetType) && config.DamageMultiplier <= 0f;
        }

        static bool IsOffensiveConfig(BattleActionConfig config)
        {
            return IsEnemyTarget(config.TargetType)
                && (config.DamageMultiplier > 0f || config.FixedValue > 0
                    || config.BreakDamage > 0);
        }

        static StatusEffectType? ExpectedDefensiveStatus(CombatUnit actor,
            BattleActionConfig config)
        {
            CharacterRole? role = actor?.CharacterData?.Role;
            if (role == CharacterRole.Support)
                return config.Kind == BattleActionKind.Ultimate
                    ? StatusEffectType.DamageUp : StatusEffectType.AttackUp;
            if (role == CharacterRole.Tank
                || role == CharacterRole.Healer && config.Kind == BattleActionKind.Ultimate)
                return StatusEffectType.Shield;
            return null;
        }

        static string ExpectedDefensiveEffectId(CombatUnit actor, BattleActionConfig config)
        {
            if (actor == null) return string.Empty;
            if (actor.CharacterData?.Role == CharacterRole.Support)
                return actor.Id + (config.Kind == BattleActionKind.Ultimate
                    ? ".support_ultimate" : ".support_skill");
            if (ExpectedDefensiveStatus(actor, config) == StatusEffectType.Shield)
                return actor.Id + ".party_shield";
            return string.Empty;
        }

        static bool IsRecoveryAction(ActionRequest request)
        {
            return request != null && IsAllyTarget(request.TargetType)
                && (request.HealingAttackMultiplier > 0f || request.HealingMaxHpMultiplier > 0f
                    || request.FixedHealing > 0f);
        }

        static bool IsDefensiveAction(ActionRequest request)
        {
            if (request == null || !IsAllyTarget(request.TargetType)) return false;
            if (IsRecoveryAction(request) || request.ShieldAmount > 0f) return true;
            foreach (StatusEffectInstance effect in request.StatusEffects)
                if (effect.Type == StatusEffectType.Shield || effect.Type == StatusEffectType.AttackUp
                    || effect.Type == StatusEffectType.DamageUp) return true;
            return request.DamageMultiplier <= 0f && request.FixedDamage <= 0f;
        }

        static bool IsWeaknessHit(CombatUnit actor, BattleActionConfig config,
            CombatUnit target)
        {
            if (actor == null || target == null || target.IsBroken) return false;
            BattleElement element = config.Element == BattleElement.Auto ? actor.Element : config.Element;
            return target.HasWeakness(element);
        }

        static bool CanBreak(CombatUnit actor, BattleActionConfig config, CombatUnit target)
        {
            return IsWeaknessHit(actor, config, target) && target.BreakCurrent > 0f
                && config.BreakDamage + .001f >= target.BreakCurrent;
        }

        static string BasicAttackReason(CombatUnit actor, BattleActionConfig skill,
            AutoBattlePreset preset)
        {
            if (preset == AutoBattlePreset.SurvivalFirst) return "SP를 회복해 생존 행동을 준비";
            if (preset == AutoBattlePreset.BreakFirst) return "더 유리한 약점·격파 기회를 대기";
            if (preset == AutoBattlePreset.PreserveUltimate) return "자원을 모으며 필살기 사용 구간을 대기";
            if (skill.SkillPointCost > 0) return "SP 수급과 공격 주기를 균형 있게 조정";
            return actor.DisplayName + "의 안정적인 기본 행동";
        }

        static float RegularRecoveryThreshold(AutoBattlePreset preset)
        {
            return preset switch
            {
                AutoBattlePreset.OffenseFirst => .42f,
                AutoBattlePreset.BreakFirst => .55f,
                AutoBattlePreset.SurvivalFirst => .82f,
                AutoBattlePreset.PreserveUltimate => .6f,
                _ => .68f
            };
        }

        static float UltimateRecoveryThreshold(AutoBattlePreset preset)
        {
            return preset switch
            {
                AutoBattlePreset.OffenseFirst => .28f,
                AutoBattlePreset.BreakFirst => .4f,
                AutoBattlePreset.SurvivalFirst => .68f,
                AutoBattlePreset.PreserveUltimate => .25f,
                _ => .48f
            };
        }

        public static CombatUnit SelectTarget(IReadOnlyList<CombatUnit> players,
            IReadOnlyList<CombatUnit> enemies, CombatUnit actor, BattleActionConfig config,
            AutoBattlePreset preset)
        {
            if (actor == null || !actor.IsAlive) return null;
            if (config.TargetType == BattleTargetType.Self) return actor;
            if (IsAllyTarget(config.TargetType))
                return SelectAllyTarget(players, actor, config);
            return SelectOffensiveTarget(enemies, actor, config, preset);
        }

        public static CombatUnit SelectOffensiveTarget(IReadOnlyList<CombatUnit> enemies,
            CombatUnit actor, BattleActionConfig config, AutoBattlePreset preset)
        {
            CombatUnit best = null;
            float bestScore = float.MinValue;
            if (enemies == null || actor == null) return null;
            BattleElement element = config.Element == BattleElement.Auto ? actor.Element : config.Element;
            foreach (CombatUnit enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive || enemy.Team == actor.Team) continue;
                bool weakness = enemy.HasWeakness(element) && !enemy.IsBroken;
                bool canBreak = weakness && enemy.BreakCurrent > 0f
                    && config.BreakDamage + .001f >= enemy.BreakCurrent;
                float score = (1f - enemy.HpRatio) * (preset == AutoBattlePreset.OffenseFirst ? 190f : 110f);
                if (weakness) score += preset == AutoBattlePreset.BreakFirst ? 260f : 85f;
                if (canBreak) score += preset == AutoBattlePreset.BreakFirst ? 520f : 330f;
                if (enemy.IsBroken) score += preset == AutoBattlePreset.OffenseFirst ? 220f : 130f;
                if (enemy.Warning) score += preset == AutoBattlePreset.SurvivalFirst ? 240f : 70f;
                if (enemy.IsBoss) score += preset == AutoBattlePreset.PreserveUltimate ? 100f : 45f;
                if (config.TargetType == BattleTargetType.AdjacentEnemies)
                    score += AdjacentAliveCount(enemies, enemy) * 45f;
                if (score > bestScore + .001f || Mathf.Approximately(score, bestScore)
                    && (best == null || enemy.Slot < best.Slot))
                {
                    best = enemy;
                    bestScore = score;
                }
            }
            return best;
        }

        public static CombatUnit SelectAllyTarget(IReadOnlyList<CombatUnit> players,
            CombatUnit actor, BattleActionConfig config)
        {
            CombatUnit best = null;
            float bestScore = float.MinValue;
            if (players == null || actor == null) return null;
            bool supportBuff = actor.CharacterData != null && actor.CharacterData.Role == CharacterRole.Support
                && config.Kind != BattleActionKind.Basic;
            foreach (CombatUnit ally in players)
            {
                if (ally == null || !ally.IsAlive || ally.Team != actor.Team) continue;
                float score = (1f - ally.HpRatio) * 1000f;
                if (supportBuff)
                {
                    if (!HasStatus(ally, StatusEffectType.AttackUp)) score += 500f;
                    score += ally.Stats.Attack * .02f;
                }
                score -= ally.Shield / Mathf.Max(1f, ally.MaxHp) * 80f;
                if (score > bestScore + .001f || Mathf.Approximately(score, bestScore)
                    && (best == null || ally.Slot < best.Slot))
                {
                    best = ally;
                    bestScore = score;
                }
            }
            return best;
        }

        static List<CombatUnit> ResolveAlliedTargets(BattleTargetType targetType,
            CombatUnit primaryTarget, CombatUnit actor, IReadOnlyList<CombatUnit> players)
        {
            var alive = new List<CombatUnit>();
            if (players != null)
                foreach (CombatUnit player in players)
                    if (player != null && player.IsAlive) alive.Add(player);
            alive.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            var result = new List<CombatUnit>();
            switch (targetType)
            {
                case BattleTargetType.AllAllies:
                    result.AddRange(alive);
                    break;
                case BattleTargetType.LowestHpAlly:
                    CombatUnit lowest = null;
                    foreach (CombatUnit ally in alive)
                        if (lowest == null || ally.HpRatio < lowest.HpRatio) lowest = ally;
                    if (lowest != null) result.Add(lowest);
                    break;
                case BattleTargetType.AdjacentAllies:
                    int index = alive.IndexOf(primaryTarget);
                    if (index < 0 && alive.Count > 0) index = 0;
                    if (index >= 0)
                    {
                        result.Add(alive[index]);
                        if (index > 0) result.Add(alive[index - 1]);
                        if (index + 1 < alive.Count) result.Add(alive[index + 1]);
                    }
                    break;
                case BattleTargetType.Self:
                    if (actor != null && actor.IsAlive) result.Add(actor);
                    break;
                default:
                    if (primaryTarget != null && primaryTarget.IsAlive)
                        result.Add(primaryTarget);
                    break;
            }
            return result;
        }

        static int AdjacentAliveCount(IReadOnlyList<CombatUnit> units, CombatUnit primary)
        {
            var alive = new List<CombatUnit>();
            foreach (CombatUnit unit in units)
                if (unit != null && unit.IsAlive) alive.Add(unit);
            alive.Sort((left, right) => left.Slot.CompareTo(right.Slot));
            int index = alive.IndexOf(primary);
            if (index < 0) return 0;
            int count = 1;
            if (index > 0) count++;
            if (index + 1 < alive.Count) count++;
            return count;
        }

        static bool IsEnemyTarget(BattleTargetType type)
        {
            return type == BattleTargetType.SingleEnemy || type == BattleTargetType.AllEnemies
                || type == BattleTargetType.AdjacentEnemies || type == BattleTargetType.RandomEnemy;
        }

        static bool IsAllyTarget(BattleTargetType type)
        {
            return type == BattleTargetType.SingleAlly || type == BattleTargetType.AllAllies
                || type == BattleTargetType.AdjacentAllies || type == BattleTargetType.LowestHpAlly
                || type == BattleTargetType.Self;
        }

        static bool HitsMultipleEnemies(BattleTargetType type)
        {
            return type == BattleTargetType.AllEnemies || type == BattleTargetType.AdjacentEnemies;
        }

        static bool HasStatus(CombatUnit unit, StatusEffectType type)
        {
            foreach (StatusEffectInstance effect in unit.Statuses)
                if (effect.Type == type) return true;
            return false;
        }

        static int AliveCount(IReadOnlyList<CombatUnit> units)
        {
            int count = 0;
            if (units != null)
                foreach (CombatUnit unit in units) if (unit != null && unit.IsAlive) count++;
            return count;
        }

        static float MinimumHpRatio(IReadOnlyList<CombatUnit> units)
        {
            float minimum = 1f;
            bool found = false;
            if (units != null)
                foreach (CombatUnit unit in units)
                    if (unit != null && unit.IsAlive)
                    { minimum = Mathf.Min(minimum, unit.HpRatio); found = true; }
            return found ? minimum : 0f;
        }

        static int CountAlliesBelow(IReadOnlyList<CombatUnit> players, float threshold)
        {
            int count = 0;
            if (players != null)
                foreach (CombatUnit player in players)
                    if (player != null && player.IsAlive && player.HpRatio <= threshold) count++;
            return count;
        }

        static bool AnyEnemyWarning(IReadOnlyList<CombatUnit> enemies)
        {
            if (enemies != null)
                foreach (CombatUnit enemy in enemies)
                    if (enemy != null && enemy.IsAlive && enemy.Warning) return true;
            return false;
        }

        static bool AnyAliveBoss(IReadOnlyList<CombatUnit> enemies)
        {
            if (enemies != null)
                foreach (CombatUnit enemy in enemies)
                    if (enemy != null && enemy.IsAlive && enemy.IsBoss) return true;
            return false;
        }

        static string TargetName(CombatUnit target)
        {
            return target != null ? target.DisplayName : "아군";
        }
    }
}
