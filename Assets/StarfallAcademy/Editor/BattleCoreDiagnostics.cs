using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby
{
    public static class BattleCoreDiagnostics
    {
        const int DeterministicSeed = 240711;

        [MenuItem("Starfall/Diagnostics/Battle Core Smoke Test")]
        public static void RunCoreSmokeTest()
        {
            VerifyResourcesAndUltimateQueue();
            VerifyTurnOrderAndPreview();
            VerifyShieldBreakAndStatuses();
            VerifyBattleRegressionCases();
            VerifyStageTenPhaseTransition();
            VerifyEnemyWarningAndBossPhase();
            VerifyAutoDecisionCore();
            UnityEngine.Debug.Log("[Starfall Battle] Core smoke test passed.");
        }

        static void VerifyResourcesAndUltimateQueue()
        {
            CombatUnit playerA = Unit("player-a", BattleTeam.Player, 0, 110f);
            CombatUnit playerB = Unit("player-b", BattleTeam.Player, 1, 105f);
            CombatUnit enemy = Unit("enemy", BattleTeam.Enemy, 0, 80f);
            var core = new BattleCombatCore(new[] { playerA, playerB, enemy }, deterministicSeed: DeterministicSeed);

            Require(core.Resources.SkillPoints == 3, "전투 시작 SP가 3이 아닙니다.");
            Require(core.Resources.MaximumSkillPoints == 5, "최대 SP가 5가 아닙니다.");

            ActionResolution basic = core.Execute(new ActionRequest(playerA, BattleActionKind.Basic,
                BattleTargetType.SingleEnemy, enemy)
            {
                SkillName = "SP 기본 공격 검증",
                SkillPointCost = -1,
                ConsumesRegularTurn = false
            });
            Require(basic.Success && core.Resources.SkillPoints == 4, "기본 공격의 SP 1 회복이 실패했습니다.");

            ActionResolution skill = core.Execute(new ActionRequest(playerA, BattleActionKind.Skill,
                BattleTargetType.SingleEnemy, enemy)
            {
                SkillName = "SP 스킬 검증",
                SkillPointCost = 1,
                ConsumesRegularTurn = false
            });
            Require(skill.Success && core.Resources.SkillPoints == 3, "스킬의 SP 1 소비가 실패했습니다.");
            core.Resources.GainSkillPoints(99);
            Require(core.Resources.SkillPoints == 5, "SP가 최대치 5를 초과하거나 도달하지 못했습니다.");

            playerA.SetEnergy(playerA.MaxEnergy);
            playerB.SetEnergy(playerB.MaxEnergy);
            double playerAActionValue = playerA.ActionValue;
            double playerBActionValue = playerB.ActionValue;
            var firstUltimate = new ActionRequest(playerA, BattleActionKind.Ultimate,
                BattleTargetType.SingleEnemy, enemy)
            {
                SkillName = "첫 번째 필살기",
                EnergyCost = playerA.MaxEnergy,
                DamageMultiplier = .1f,
                ConsumesRegularTurn = false
            };
            var secondUltimate = new ActionRequest(playerB, BattleActionKind.Ultimate,
                BattleTargetType.SingleEnemy, enemy)
            {
                SkillName = "두 번째 필살기",
                EnergyCost = playerB.MaxEnergy,
                DamageMultiplier = .1f,
                ConsumesRegularTurn = false
            };

            Require(core.QueueUltimate(firstUltimate, out string firstFailure),
                "첫 번째 필살기 예약 실패: " + firstFailure);
            Require(core.QueueUltimate(secondUltimate, out string secondFailure),
                "두 번째 필살기 예약 실패: " + secondFailure);
            Require(Near(playerA.Energy, 0f) && Near(playerB.Energy, 0f),
                "필살기 예약 시 에너지가 즉시 0으로 선점되지 않았습니다.");
            Require(core.Ultimates.Count == 2, "필살기 두 개가 큐에 유지되지 않았습니다.");

            Require(core.TryExecuteNextUltimate(out ActionResolution firstResolution)
                && ReferenceEquals(firstResolution.Request, firstUltimate), "필살기 큐 FIFO 순서가 깨졌습니다.");
            Require(core.TryExecuteNextUltimate(out ActionResolution secondResolution)
                && ReferenceEquals(secondResolution.Request, secondUltimate), "두 번째 필살기 FIFO 순서가 깨졌습니다.");
            Require(Near(playerA.ActionValue, playerAActionValue) && Near(playerB.ActionValue, playerBActionValue),
                "필살기가 정규 행동 수치를 변경했습니다.");
        }

        static void VerifyTurnOrderAndPreview()
        {
            CombatUnit fast = Unit("fast", BattleTeam.Player, 0, 125f);
            CombatUnit slow = Unit("slow", BattleTeam.Enemy, 0, 80f);
            var manager = new TurnManager(new[] { slow, fast });
            double fastBefore = fast.ActionValue;
            double slowBefore = slow.ActionValue;
            IReadOnlyList<CombatUnit> preview = manager.PeekNextActions(8);

            Require(preview.Count == 8, "행동 순서 미리보기가 8칸을 반환하지 않았습니다.");
            Require(ReferenceEquals(preview[0], fast), "속도가 빠른 유닛이 미리보기 첫 순서가 아닙니다.");
            Require(Near(fast.ActionValue, fastBefore) && Near(slow.ActionValue, slowBefore),
                "행동 순서 미리보기가 실제 행동 수치를 변경했습니다.");
            Require(ReferenceEquals(manager.NextActor(), fast), "속도 기반 첫 행동자 계산이 잘못되었습니다.");
            Require(manager.CompleteRegularTurn(fast), "첫 정규 행동 완료 처리가 실패했습니다.");
            Require(ReferenceEquals(manager.NextActor(), slow), "행동 완료 후 다음 행동자 계산이 잘못되었습니다.");
        }

        static void VerifyShieldBreakAndStatuses()
        {
            CombatUnit shielded = Unit("shielded", BattleTeam.Player, 0, 100f);
            float hpBefore = shielded.CurrentHp;
            shielded.AddShield(50f);
            DamageApplication shieldDamage = shielded.TakeDamage(80f);
            Require(shieldDamage.ShieldAbsorbed == 50 && shieldDamage.HpDamage == 30,
                "피해가 보호막부터 흡수되지 않았습니다.");
            Require(Near(shielded.Shield, 0f) && Near(shielded.CurrentHp, hpBefore - 30f),
                "보호막 우선 피해 적용 후 HP가 올바르지 않습니다.");

            CombatUnit breaker = Unit("breaker", BattleTeam.Player, 0, 120f);
            CombatUnit breakTarget = Unit("break-target", BattleTeam.Enemy, 0, 100f, breakMax: 60f);
            breakTarget.SetWeaknesses(new[] { BattleElement.Fire });
            var turns = new TurnManager(new[] { breaker, breakTarget });
            var breaks = new BreakSystem(turns, new DamageCalculator(new Random(DeterministicSeed)));
            float gaugeBefore = breakTarget.BreakCurrent;
            BreakResult nonWeak = breaks.ApplyBreakDamage(breaker, breakTarget, BattleElement.Ice, 60f);
            Require(!nonWeak.WeaknessMatched && Near(breakTarget.BreakCurrent, gaugeBefore),
                "비약점 공격이 브레이크 게이지를 감소시켰습니다.");

            double actionBeforeBreak = breakTarget.ActionValue;
            BreakResult weakness = breaks.ApplyBreakDamage(breaker, breakTarget, BattleElement.Fire, 60f);
            Require(weakness.WeaknessMatched && weakness.BreakTriggered && breakTarget.IsBroken,
                "약점 공격이 브레이크를 발생시키지 않았습니다.");
            Require(Near(breakTarget.ActionValue, actionBeforeBreak * 1.3d),
                "브레이크의 기본 행동 지연 30%가 적용되지 않았습니다.");

            CombatUnit statusTarget = Unit("status-target", BattleTeam.Player, 0, 100f);
            StatusApplyResult applied = statusTarget.ApplyStatus(new StatusEffectInstance(
                StatusEffectType.AttackUp, .2f, 2, breaker, StatusStackBehavior.KeepHigher));
            Require(applied.Applied && statusTarget.Statuses.Count == 1, "상태 효과 부여가 실패했습니다.");
            statusTarget.CompleteOwnerRegularAction();
            Require(statusTarget.Statuses.Count == 1
                && statusTarget.Statuses[0].RemainingOwnerActions == 1,
                "상태 지속 시간이 보유자의 첫 정규 행동에서 올바르게 감소하지 않았습니다.");
            statusTarget.CompleteOwnerRegularAction();
            Require(statusTarget.Statuses.Count == 0, "상태 효과가 두 번째 정규 행동 후 종료되지 않았습니다.");
        }

        static void VerifyEnemyWarningAndBossPhase()
        {
            CombatUnit player = Unit("boss-target", BattleTeam.Player, 0, 90f);
            CombatUnit boss = Unit("boss", BattleTeam.Enemy, 0, 100f);
            boss.IsBoss = true;
            boss.Archetype = EnemyArchetype.BossObserver;
            boss.CompleteOwnerRegularAction();
            var core = new BattleCombatCore(new[] { player, boss }, deterministicSeed: DeterministicSeed);

            ActionRequest warning = core.DecideEnemyAction(boss);
            Require(warning != null && warning.WarningStateAfterResolution == true,
                "보스 AI가 예고 행동을 선택하지 않았습니다.");
            warning.ConsumesRegularTurn = false;
            ActionResolution warningResolution = core.Execute(warning);
            Require(warningResolution.Success && boss.Warning, "보스 예고 상태가 활성화되지 않았습니다.");

            ActionRequest warnedAttack = core.DecideEnemyAction(boss);
            Require(warnedAttack != null && warnedAttack.WarningStateAfterResolution == false
                && warnedAttack.TargetType == BattleTargetType.AllEnemies,
                "예고 후 보스 AI가 강력한 광역 공격을 선택하지 않았습니다.");
            warnedAttack.ConsumesRegularTurn = false;
            Require(core.Execute(warnedAttack).Success && !boss.Warning,
                "강력한 공격 해결 후 보스 경고가 해제되지 않았습니다.");

            boss.TakeDamage(boss.MaxHp * .51f);
            Require(boss.Phase == 2, "보스 HP 50% 이하에서 2페이즈로 전환되지 않았습니다.");
        }

        static void VerifyBattleRegressionCases()
        {
            VerifyShieldRefresh();
            VerifySelfStatusLifetime();
            VerifyBreakDamageDefeat();
            VerifyAggroTargeting();
            VerifyUltimateReservationCleanup();
            VerifyDisabledBossPhase();
        }

        static void VerifyShieldRefresh()
        {
            CombatUnit source = Unit("shield-source", BattleTeam.Player, 0, 100f);
            CombatUnit target = Unit("shield-refresh-target", BattleTeam.Player, 1, 100f);
            var shield = new StatusEffectInstance(StatusEffectType.Shield, 0f, 2, source,
                StatusStackBehavior.RefreshDuration, effectId: "regression.shield", flatValue: 120f);
            StatusApplyResult initial = target.ApplyStatus(shield);
            target.TakeDamage(120f);
            Require(initial.Applied && Near(target.Shield, 0f)
                && Near(initial.Effect.RuntimeValue, 0f),
                "Shield setup did not reach the depleted state.");

            StatusApplyResult refreshed = target.ApplyStatus(shield);
            Require(refreshed.Applied && refreshed.Merged && Near(target.Shield, 120f)
                && Near(refreshed.Effect.RuntimeValue, 120f),
                "Refreshing a depleted shield did not restore its capacity.");
        }

        static void VerifySelfStatusLifetime()
        {
            CombatUnit actor = Unit("self-status-actor", BattleTeam.Player, 0, 120f);
            CombatUnit enemy = Unit("self-status-enemy", BattleTeam.Enemy, 0, 80f);
            var core = new BattleCombatCore(new[] { actor, enemy }, deterministicSeed: DeterministicSeed);
            Require(ReferenceEquals(core.NextActor(), actor),
                "Self-status regression actor was not first in the turn order.");
            var request = new ActionRequest(actor, BattleActionKind.Skill,
                BattleTargetType.Self, actor)
            {
                SkillName = "Self status lifetime regression",
                ConsumesRegularTurn = true
            };
            request.AddStatus(new StatusEffectInstance(StatusEffectType.AttackUp, .2f, 2,
                actor, StatusStackBehavior.RefreshDuration, effectId: "regression.self-status"));
            ActionResolution resolution = core.Execute(request);
            Require(resolution.Success && actor.Statuses.Count == 1
                && actor.Statuses[0].RemainingOwnerActions == 2,
                "A self-applied status lost duration on the action that applied it.");
            actor.CompleteOwnerRegularAction();
            Require(actor.Statuses[0].RemainingOwnerActions == 1,
                "A self-applied status did not tick on the following owner action.");
        }

        static void VerifyBreakDamageDefeat()
        {
            CombatUnit attacker = Unit("break-kill-attacker", BattleTeam.Player, 0, 120f);
            CombatUnit target = Unit("break-kill-target", BattleTeam.Enemy, 0, 80f, breakMax: 10f);
            target.SetWeaknesses(new[] { BattleElement.Fire });
            target.TakeDamage(target.MaxHp - 1f);
            var core = new BattleCombatCore(new[] { attacker, target }, deterministicSeed: DeterministicSeed);
            int breakEvents = 0;
            core.Events.Subscribe<BreakTriggeredEvent>(_ => breakEvents++);
            var request = new ActionRequest(attacker, BattleActionKind.Skill,
                BattleTargetType.SingleEnemy, target)
            {
                SkillName = "Break defeat regression",
                Element = BattleElement.Fire,
                BreakDamage = 10f,
                ConsumesRegularTurn = false
            };
            ActionResolution resolution = core.Execute(request);
            Require(resolution.Success && !target.IsAlive && !target.IsBroken
                && breakEvents == 0 && resolution.BreakResults.Count == 1
                && resolution.BreakResults[0].BreakTriggered
                && resolution.DamageResults.Count == 1
                && resolution.DefeatedUnits.Count == 1
                && ReferenceEquals(resolution.DefeatedUnits[0], target)
                && Near(attacker.Energy, 10f),
                "Break bonus damage did not finalize defeat through the normal damage path.");
        }

        static void VerifyAggroTargeting()
        {
            CombatUnit enemy = Unit("aggro-enemy", BattleTeam.Enemy, 0, 100f);
            CombatUnit ignored = Unit("aggro-zero", BattleTeam.Player, 0, 100f);
            CombatUnit selected = Unit("aggro-weighted", BattleTeam.Player, 1, 100f);
            ignored.AggroWeight = 0f;
            selected.AggroWeight = 2f;
            var targeting = new TargetingSystem(new Random(DeterministicSeed));
            for (int i = 0; i < 16; i++)
            {
                IReadOnlyList<CombatUnit> result = targeting.Resolve(BattleTargetType.RandomEnemy,
                    enemy, null, new[] { enemy, ignored, selected });
                Require(result.Count == 1 && ReferenceEquals(result[0], selected),
                    "Enemy random targeting ignored character aggro weights.");
            }
        }

        static void VerifyUltimateReservationCleanup()
        {
            CombatUnit actor = Unit("queued-ultimate-actor", BattleTeam.Player, 0, 100f);
            CombatUnit enemy = Unit("queued-ultimate-enemy", BattleTeam.Enemy, 0, 90f);
            var core = new BattleCombatCore(new[] { actor, enemy }, deterministicSeed: DeterministicSeed);
            actor.SetEnergy(actor.MaxEnergy);
            var request = new ActionRequest(actor, BattleActionKind.Ultimate,
                BattleTargetType.SingleEnemy, enemy)
            {
                EnergyCost = actor.MaxEnergy,
                DamageMultiplier = 1f,
                ConsumesRegularTurn = false
            };
            Require(core.QueueUltimate(request, out string failure),
                "Ultimate reservation setup failed: " + failure);
            actor.TakeDamage(actor.MaxHp);
            Require(core.TryExecuteNextUltimate(out ActionResolution resolution)
                && !resolution.Success && !request.ResourcesReserved
                && Near(actor.Energy, actor.MaxEnergy),
                "A dead queued ultimate actor kept or lost its reserved energy.");
        }

        static void VerifyDisabledBossPhase()
        {
            CombatUnit boss = Unit("disabled-phase-boss", BattleTeam.Enemy, 0, 100f);
            boss.IsBoss = true;
            boss.PhaseTwoEnabled = false;
            boss.TakeDamage(boss.MaxHp * .75f);
            Require(boss.Phase == 1,
                "A boss entered phase two even though the stage phase flag was disabled.");
        }

        static void VerifyStageTenPhaseTransition()
        {
            const string path = "Assets/StarfallAcademy/Data/Stages/Stage_10.asset";
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            Require(stage != null, "Stage 10 regression asset is missing: " + path);
            var model = new TurnBattleModel(new FormationState(), stage, DeterministicSeed);
            int enemiesBefore = model.Enemies.Count;
            CombatUnit boss = null;
            foreach (CombatUnit enemy in model.Enemies)
                if (enemy.IsBoss) { boss = enemy; break; }
            Require(boss != null && stage.BossPhaseTwoEnabled,
                "Stage 10 is no longer configured as a phase-two boss stage.");
            boss.TakeDamage(boss.MaxHp * (1f - stage.BossPhaseTwoThreshold + .01f));
            model.NextActor();
            Require(model.Enemies.Count > enemiesBefore,
                "Stage 10 phase transition did not add its configured summons.");
        }

        static void VerifyAutoDecisionCore()
        {
            CombatUnit actor = Unit("auto-actor", BattleTeam.Player, 0, 105f);
            CombatUnit ordinary = Unit("ordinary", BattleTeam.Enemy, 0, 90f, breakMax: 60f);
            CombatUnit weak = Unit("weak", BattleTeam.Enemy, 1, 90f, breakMax: 60f);
            weak.SetWeaknesses(new[] { BattleElement.Fire });
            var breakSkill = new BattleActionConfig("격파 검증", null, BattleActionKind.Skill,
                BattleTargetType.SingleEnemy, 1f, 0f, 0, 1, 0, 0, 60, BattleElement.Fire);
            CombatUnit selectedEnemy = AutoDecisionService.SelectOffensiveTarget(
                new[] { ordinary, weak }, actor, breakSkill, AutoBattlePreset.BreakFirst);
            Require(ReferenceEquals(selectedEnemy, weak),
                "격파 우선 자동 전투가 약점 격파 가능한 적을 선택하지 않았습니다.");

            CombatUnit healthy = Unit("healthy", BattleTeam.Player, 1, 100f);
            CombatUnit injured = Unit("injured", BattleTeam.Player, 2, 100f);
            injured.TakeDamage(injured.MaxHp * .7f);
            var healSkill = new BattleActionConfig("회복 검증", null, BattleActionKind.Skill,
                BattleTargetType.SingleAlly, 0f, .2f, 0, 1, 0, 0, 0, BattleElement.Ice);
            CombatUnit selectedAlly = AutoDecisionService.SelectAllyTarget(
                new[] { healthy, injured }, actor, healSkill);
            Require(ReferenceEquals(selectedAlly, injured),
                "생존 자동 전투가 체력 비율이 가장 낮은 아군을 선택하지 않았습니다.");

            AutoBattlePreset cycled = AutoBattlePreset.Balanced;
            for (int i = 0; i < 5; i++) cycled = AutoBattleSettings.NextPreset(cycled);
            Require(cycled == AutoBattlePreset.Balanced,
                "자동 전투 프리셋 5종 순환이 시작 프리셋으로 돌아오지 않습니다.");
        }

        static CombatUnit Unit(string id, BattleTeam team, int slot, float speed, float breakMax = 0f)
        {
            return new CombatUnit(id, id, team, slot,
                new BattleBaseStats(1000f, 100f, 50f, speed, .05f, 1.5f, 100f),
                element: BattleElement.Fire, breakMax: breakMax);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Starfall Battle Smoke Test] " + message);
        }

        static bool Near(double left, double right, double epsilon = .0001d)
        {
            return Math.Abs(left - right) <= epsilon;
        }
    }
}
