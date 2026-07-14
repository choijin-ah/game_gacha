using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [Serializable]
    public sealed class WeeklyBossDifficulty
    {
        [SerializeField] string difficultyId = "normal";
        [SerializeField] string displayName = "일반";
        [SerializeField, Min(0)] int recommendedPower = 20000;
        [SerializeField, Min(.01f)] float hpMultiplier = 1f;
        [SerializeField, Min(.01f)] float attackMultiplier = 1f;
        [SerializeField, Min(.01f)] float speedMultiplier = 1f;
        [SerializeField, Min(1)] int turnLimit = 30;
        [SerializeField, Min(1)] int weeklyAttempts = 3;

        public string Id => string.IsNullOrWhiteSpace(difficultyId) ? "difficulty" : difficultyId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public int RecommendedPower => Mathf.Max(0, recommendedPower);
        public float HpMultiplier => Mathf.Max(.01f, hpMultiplier);
        public float AttackMultiplier => Mathf.Max(.01f, attackMultiplier);
        public float SpeedMultiplier => Mathf.Max(.01f, speedMultiplier);
        public int TurnLimit => Mathf.Max(1, turnLimit);
        public int WeeklyAttempts => Mathf.Max(1, weeklyAttempts);
    }

    [Serializable]
    public sealed class WeeklyBossScoreRewardTier
    {
        [SerializeField, Min(0)] int requiredScore;
        [SerializeField] RewardPackage reward = new RewardPackage();

        public int RequiredScore => Mathf.Max(0, requiredScore);
        public RewardPackage Reward => reward;
    }

    [CreateAssetMenu(fileName = "WeeklyBoss", menuName = "Starfall/Weekly Boss Definition")]
    public sealed class WeeklyBossDefinition : ScriptableObject
    {
        [SerializeField] string bossId = "WB_001";
        [SerializeField] string displayName = "심연의 관측자";
        [SerializeField, TextArea(2, 5)] string description;
        [SerializeField] Sprite portrait;
        [SerializeField] StageData baseStage;
        [SerializeField] AudioClip menuBgm;
        [SerializeField] ScheduleRange availability = new ScheduleRange();
        [SerializeField] List<WeeklyBossDifficulty> difficultyEntries = new List<WeeklyBossDifficulty>();
        [SerializeField] List<WeeklyBossScoreRewardTier> scoreRewardTiers = new List<WeeklyBossScoreRewardTier>();

        public string Id => string.IsNullOrWhiteSpace(bossId) ? name : bossId.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public Sprite Portrait => portrait;
        public StageData BaseStage => baseStage;
        public AudioClip MenuBgm => menuBgm;
        public ScheduleRange Availability => availability;
        public IReadOnlyList<WeeklyBossDifficulty> Difficulties => difficultyEntries;
        public IReadOnlyList<WeeklyBossScoreRewardTier> RewardTiers => scoreRewardTiers;
        public bool IsAvailable(DateTime utcNow) => availability == null || availability.Contains(utcNow);

        void OnValidate()
        {
            if (availability == null) availability = new ScheduleRange();
            if (difficultyEntries == null) difficultyEntries = new List<WeeklyBossDifficulty>();
            if (scoreRewardTiers == null) scoreRewardTiers = new List<WeeklyBossScoreRewardTier>();
        }
    }
}
