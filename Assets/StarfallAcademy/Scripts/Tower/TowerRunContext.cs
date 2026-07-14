using System;

namespace StarfallAcademy.Lobby
{
    public sealed class TowerRunContext : IBattleModeRunContext
    {
        readonly TowerProgressService service;
        readonly TowerDatabase database;
        readonly TowerFloorData floor;

        internal TowerRunContext(TowerProgressService service, TowerDatabase database,
            TowerFloorData floor, string runId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.floor = floor ?? throw new ArgumentNullException(nameof(floor));
            RunId = runId;
            var modifier = new TowerModifierService(floor.Modifiers);
            Rules = new BattleRuleSet(BattleMode.ChallengeTower, runtimeModifier: modifier);
        }

        public BattleMode Mode => BattleMode.ChallengeTower;
        public StageData Stage => floor.BaseStage;
        public BattleRuleSet Rules { get; }
        public string ReturnScene => SceneNames.ChallengeTower;
        public string RunId { get; }
        public bool RewardEligible => true;
        public TowerFloorData Floor => floor;

        public BattleModeCompletion Complete(BattleResult result)
        {
            TowerCompletionResult completion = service.Complete(database, floor, result);
            if (!completion.Succeeded)
                return new BattleModeCompletion
                {
                    Succeeded = false,
                    Title = "결 과 저 장 실 패",
                    Body = completion.Error,
                    NextLabel = "재도전",
                    CanRetry = true
                };
            bool cleared = result != null && result.EnemiesDefeated;
            return new BattleModeCompletion
            {
                Succeeded = true,
                Title = cleared ? floor.FloorNumber.ToString("00") + "층 돌파" : "도 전 실 패",
                Body = cleared
                    ? "획득 별  " + new string('★', completion.EarnedStars)
                        + new string('☆', 3 - completion.EarnedStars)
                        + "\n최고 기록  " + completion.BestStars + "★"
                        + (completion.FirstClear ? "\nFIRST CLEAR" : string.Empty)
                        + (completion.RewardsClaimed > 0
                            ? "\n보상 " + completion.RewardsClaimed + "개 획득" : string.Empty)
                    : "편성과 층 효과를 확인한 뒤 다시 도전하세요.",
                NextLabel = "다시 도전",
                CanRetry = true
            };
        }

        public bool TryCreateRetry(out IBattleModeRunContext retryContext,
            out string failureReason)
        {
            bool started = service.TryBeginRun(floor, database, out TowerRunContext retry,
                out failureReason);
            retryContext = retry;
            return started;
        }
    }
}
