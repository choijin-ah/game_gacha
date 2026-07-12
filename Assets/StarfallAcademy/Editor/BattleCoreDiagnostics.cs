using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby
{
    public static class BattleCoreDiagnostics
    {
        const int DeterministicSeed = 240711;

        [MenuItem("Starfall/Battle/Run Core Smoke Test")]
        public static void RunCoreSmokeTest()
        {
            VerifyResourcesAndUltimateQueue();
            VerifyTurnOrderAndPreview();
            VerifyShieldBreakAndStatuses();
            VerifyEnemyWarningAndBossPhase();
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
