using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [CreateAssetMenu(fileName = "Stage", menuName = "Starfall Academy/Battle Stage")]
    public sealed class StageData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string stageId = "stage_01";
        [SerializeField] string chapter = "CHAPTER 1";
        [SerializeField] string displayName = "첫 번째 작전";
        [SerializeField, TextArea(2, 4)] string description = "도시 외곽에 나타난 적을 정리하세요.";

        [Header("Enemies")]
        [SerializeField] string enemyName = "그림자 잔재";
        [SerializeField, Range(1, 4)] int enemyCount = 3;
        [SerializeField, Min(1)] int enemyLevel = 1;
        [SerializeField, Min(100)] int enemyMaxHp = 1200;
        [SerializeField, Min(1)] int enemyAttack = 120;
        [SerializeField, Min(1)] int enemySpeed = 50;
        [SerializeField] bool bossStage;

        [Header("Requirements & Reward")]
        [SerializeField, Min(0)] int recommendedPower = 4000;
        [SerializeField, Min(0)] int rewardCredits = 5000;
        [SerializeField, Min(0)] int rewardSkillMaterials = 10;

        public string Id => string.IsNullOrWhiteSpace(stageId) ? name : stageId;
        public string Chapter => chapter;
        public string DisplayName => displayName;
        public string Description => description;
        public string EnemyName => enemyName;
        public int EnemyCount => enemyCount;
        public int EnemyLevel => enemyLevel;
        public int EnemyMaxHp => enemyMaxHp;
        public int EnemyAttack => enemyAttack;
        public int EnemySpeed => enemySpeed;
        public bool BossStage => bossStage;
        public int RecommendedPower => recommendedPower;
        public int RewardCredits => rewardCredits;
        public int RewardSkillMaterials => rewardSkillMaterials;

        void OnValidate()
        {
            enemyCount = Mathf.Clamp(enemyCount, 1, 4);
            enemyLevel = Mathf.Max(1, enemyLevel);
            enemyMaxHp = Mathf.Max(100, enemyMaxHp);
            enemyAttack = Mathf.Max(1, enemyAttack);
            enemySpeed = Mathf.Max(1, enemySpeed);
        }
    }
}
