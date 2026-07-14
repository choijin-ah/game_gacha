using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public enum BattleMode
    {
        StandardStage,
        WeeklyBoss,
        ChallengeTower
    }

    public enum BattleEndReason
    {
        Ongoing,
        EnemiesDefeated,
        PlayersDefeated,
        TurnLimit
    }

    /// <summary>Content-owned rules which do not belong on reusable StageData assets.</summary>
    [Serializable]
    public sealed class BattleRuleSet
    {
        [SerializeField] BattleMode mode;
        [SerializeField, Min(0)] int turnLimit;
        [SerializeField] bool allowTimeoutResult;
        [SerializeField] bool allowNonKillResult;
        [SerializeField, Min(0)] int staminaCost;
        [SerializeField, Min(0)] int attemptCost;
        [SerializeField] bool scoreEnabled;
        [SerializeField, Min(.01f)] float enemyHpMultiplier = 1f;
        [SerializeField, Min(.01f)] float enemyAttackMultiplier = 1f;
        [SerializeField, Min(.01f)] float enemySpeedMultiplier = 1f;

        [NonSerialized] IBattleRuntimeModifier runtimeModifier;

        public BattleRuleSet(BattleMode mode = BattleMode.StandardStage, int turnLimit = 0,
            bool allowTimeoutResult = false, bool allowNonKillResult = false,
            int staminaCost = 0, int attemptCost = 0, bool scoreEnabled = false,
            float enemyHpMultiplier = 1f, float enemyAttackMultiplier = 1f,
            float enemySpeedMultiplier = 1f, IBattleRuntimeModifier runtimeModifier = null)
        {
            this.mode = mode;
            this.turnLimit = Mathf.Max(0, turnLimit);
            this.allowTimeoutResult = allowTimeoutResult;
            this.allowNonKillResult = allowNonKillResult;
            this.staminaCost = Mathf.Max(0, staminaCost);
            this.attemptCost = Mathf.Max(0, attemptCost);
            this.scoreEnabled = scoreEnabled;
            this.enemyHpMultiplier = Mathf.Max(.01f, enemyHpMultiplier);
            this.enemyAttackMultiplier = Mathf.Max(.01f, enemyAttackMultiplier);
            this.enemySpeedMultiplier = Mathf.Max(.01f, enemySpeedMultiplier);
            this.runtimeModifier = runtimeModifier;
        }

        public BattleMode Mode => mode;
        public int TurnLimit => Mathf.Max(0, turnLimit);
        public bool HasTurnLimit => TurnLimit > 0;
        public bool AllowTimeoutResult => allowTimeoutResult;
        public bool AllowNonKillResult => allowNonKillResult;
        public int StaminaCost => Mathf.Max(0, staminaCost);
        public int AttemptCost => Mathf.Max(0, attemptCost);
        public bool ScoreEnabled => scoreEnabled;
        public float EnemyHpMultiplier => Mathf.Max(.01f, enemyHpMultiplier);
        public float EnemyAttackMultiplier => Mathf.Max(.01f, enemyAttackMultiplier);
        public float EnemySpeedMultiplier => Mathf.Max(.01f, enemySpeedMultiplier);
        public IBattleRuntimeModifier RuntimeModifier => runtimeModifier;

        public static BattleRuleSet Standard(StageData stage) => new BattleRuleSet(
            BattleMode.StandardStage, staminaCost: stage != null ? stage.StaminaCost : 0);

        public BattleRuleSet WithRuntimeModifier(IBattleRuntimeModifier modifier)
        {
            runtimeModifier = modifier;
            return this;
        }
    }

    public interface IBattleRuntimeModifier
    {
        void ModifyStats(BattleTeam team, BattleBaseStats stats);
        void ModifyAction(ActionRequest request);
    }

    public sealed class BattleResult
    {
        public BattleResult()
        {
        }

        public BattleResult(BattleMode mode, BattleEndReason endReason,
            BattleOutcome coreOutcome, bool successful, int regularTurns,
            int defeatedAllies, long damageDealtToEnemies)
        {
            Mode = mode;
            EndReason = endReason;
            CoreOutcome = coreOutcome;
            IsSuccessful = successful;
            RegularTurns = Mathf.Max(0, regularTurns);
            DefeatedAllies = Mathf.Max(0, defeatedAllies);
            DamageDealtToEnemies = Math.Max(0L, damageDealtToEnemies);
        }

        public BattleMode Mode { get; internal set; }
        public BattleEndReason EndReason { get; internal set; }
        public BattleOutcome CoreOutcome { get; internal set; }
        public bool IsSuccessful { get; internal set; }
        public bool EnemiesDefeated => CoreOutcome == BattleOutcome.Victory;
        public bool TimedOut => EndReason == BattleEndReason.TurnLimit;
        public int RegularTurns { get; internal set; }
        public int DefeatedAllies { get; internal set; }
        public long DamageDealtToEnemies { get; internal set; }
    }

    public sealed class BattleModeCompletion
    {
        public bool Succeeded { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string NextLabel { get; set; }
        public bool CanRetry { get; set; }
    }

    /// <summary>Feature-specific lifecycle kept outside standard stage progression.</summary>
    public interface IBattleModeRunContext
    {
        BattleMode Mode { get; }
        StageData Stage { get; }
        BattleRuleSet Rules { get; }
        string ReturnScene { get; }
        string RunId { get; }
        bool RewardEligible { get; }
        BattleModeCompletion Complete(BattleResult result);
        bool TryCreateRetry(out IBattleModeRunContext retryContext, out string failureReason);
    }
}
