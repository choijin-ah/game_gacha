using System;

namespace StarfallAcademy.Lobby
{
    public sealed class WeeklyBossRunContext : IBattleModeRunContext
    {
        readonly WeeklyBossService service;
        readonly WeeklyBossDefinition boss;
        readonly WeeklyBossDifficulty difficulty;
        readonly string weekId;

        internal WeeklyBossRunContext(WeeklyBossService service, WeeklyBossDefinition boss,
            WeeklyBossDifficulty difficulty, string weekId, string runId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.boss = boss ?? throw new ArgumentNullException(nameof(boss));
            this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
            this.weekId = weekId;
            RunId = runId;
            Rules = new BattleRuleSet(BattleMode.WeeklyBoss, difficulty.TurnLimit,
                allowTimeoutResult: true, allowNonKillResult: true, attemptCost: 1,
                scoreEnabled: true, enemyHpMultiplier: difficulty.HpMultiplier,
                enemyAttackMultiplier: difficulty.AttackMultiplier,
                enemySpeedMultiplier: difficulty.SpeedMultiplier);
        }

        public BattleMode Mode => BattleMode.WeeklyBoss;
        public StageData Stage => boss.BaseStage;
        public BattleRuleSet Rules { get; }
        public string ReturnScene => SceneNames.WeeklyBoss;
        public string RunId { get; }
        public bool RewardEligible => true;
        public WeeklyBossDefinition Boss => boss;
        public WeeklyBossDifficulty Difficulty => difficulty;

        public BattleModeCompletion Complete(BattleResult result)
        {
            int score = WeeklyBossScoreCalculator.Calculate(result, difficulty);
            if (!service.RecordScore(boss, difficulty, weekId, score))
            {
                service.RefundAttempt(boss, difficulty, weekId);
                return new BattleModeCompletion
                {
                    Succeeded = false,
                    Title = "결 과 저 장 실 패",
                    Body = "주간 보스 기록을 저장하지 못해 도전 횟수를 반환했습니다.",
                    NextLabel = "재도전",
                    CanRetry = true
                };
            }
            WeeklyBossSnapshot snapshot = service.GetSnapshot(boss, difficulty, weekId);
            int claimed = service.ClaimEligibleRewards(boss, difficulty, weekId,
                snapshot.BestScore);
            return new BattleModeCompletion
            {
                Succeeded = true,
                Title = result != null && result.EnemiesDefeated ? "보 스 격 파" : "작 전 종 료",
                Body = boss.DisplayName + "  ·  " + difficulty.DisplayName
                    + "\nSCORE  " + score.ToString("N0")
                    + "\nDAMAGE  " + (result?.DamageDealtToEnemies ?? 0).ToString("N0")
                    + "  ·  ACTION " + (result?.RegularTurns ?? 0)
                    + "\nBEST  " + snapshot.BestScore.ToString("N0")
                    + (claimed > 0 ? "\n\n점수 보상 " + claimed + "개를 획득했습니다." : string.Empty)
                    + "\n남은 도전 " + snapshot.AttemptsRemaining + "회",
                NextLabel = "다시 도전",
                CanRetry = snapshot.AttemptsRemaining > 0
            };
        }

        public bool TryCreateRetry(out IBattleModeRunContext retryContext,
            out string failureReason)
        {
            bool started = service.TryBeginRun(boss, difficulty,
                out WeeklyBossRunContext retry, out failureReason);
            retryContext = retry;
            return started;
        }
    }
}
