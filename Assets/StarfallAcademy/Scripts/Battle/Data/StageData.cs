using UnityEngine;

namespace StarfallAcademy.Lobby
{
    [System.Serializable]
    public sealed class StageEnemyEntry
    {
        [SerializeField] string enemyId;
        [SerializeField] string displayName = "적";
        [SerializeField] EnemyArchetype archetype = EnemyArchetype.Auto;
        [SerializeField, Min(1)] int maxHp = 650;
        [SerializeField, Min(1)] int attack = 80;
        [SerializeField, Min(0)] int defense = 40;
        [SerializeField, Min(1)] int speed = 95;
        [SerializeField, Min(1)] int maxBreak = 60;
        [SerializeField] BattleElement[] weaknesses;
        [SerializeField, Range(0f, 1f)] float delayResistance;
        [SerializeField, Range(0f, 1f)] float effectResistance;
        [SerializeField] bool dangerWarning;

        public string Id => string.IsNullOrWhiteSpace(enemyId) ? DisplayName : enemyId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "적" : displayName;
        public EnemyArchetype Archetype => archetype == EnemyArchetype.Auto
            ? EnemyArchetype.Drone : archetype;
        public int MaxHp => Mathf.Max(1, maxHp);
        public int Attack => Mathf.Max(1, attack);
        public int Defense => Mathf.Max(0, defense);
        public int Speed => Mathf.Max(1, speed);
        public int MaxBreak => Mathf.Max(1, maxBreak);
        public BattleElement[] Weaknesses => ResolveWeaknesses(weaknesses, Archetype, Id);
        public float DelayResistance => Mathf.Clamp01(delayResistance);
        public float EffectResistance => Mathf.Clamp01(effectResistance);
        public bool DangerWarning => dangerWarning
            || Archetype == EnemyArchetype.ElitePredator
            || Archetype == EnemyArchetype.BossObserver;

        internal void Sanitize()
        {
            archetype = (EnemyArchetype)Mathf.Clamp((int)archetype,
                (int)EnemyArchetype.Auto, (int)EnemyArchetype.BossObserver);
            maxHp = Mathf.Max(1, maxHp);
            attack = Mathf.Max(1, attack);
            defense = Mathf.Max(0, defense);
            speed = Mathf.Max(1, speed);
            maxBreak = Mathf.Max(1, maxBreak);
            delayResistance = Mathf.Clamp01(delayResistance);
            effectResistance = Mathf.Clamp01(effectResistance);
        }

        internal static StageEnemyEntry CreateFallback(string id, string name,
            EnemyArchetype fallbackArchetype, int hp, int attackPower, int defensePower,
            int speedValue, int breakValue, BattleElement[] fallbackWeaknesses,
            float delayResist, float effectResist, bool warning)
        {
            return new StageEnemyEntry
            {
                enemyId = id,
                displayName = name,
                archetype = fallbackArchetype,
                maxHp = hp,
                attack = attackPower,
                defense = defensePower,
                speed = speedValue,
                maxBreak = breakValue,
                weaknesses = fallbackWeaknesses,
                delayResistance = delayResist,
                effectResistance = effectResist,
                dangerWarning = warning
            };
        }

        internal static BattleElement[] ResolveWeaknesses(BattleElement[] source,
            EnemyArchetype fallbackArchetype, string stableId)
        {
            if (source != null && source.Length > 0)
            {
                var sanitized = new BattleElement[Mathf.Min(3, source.Length)];
                int count = 0;
                for (int i = 0; i < source.Length && count < sanitized.Length; i++)
                {
                    BattleElement value = source[i];
                    if (value < BattleElement.Fire || value > BattleElement.Dark) continue;
                    bool duplicate = false;
                    for (int j = 0; j < count; j++)
                        if (sanitized[j] == value) duplicate = true;
                    if (!duplicate) sanitized[count++] = value;
                }
                if (count > 0)
                {
                    var result = new BattleElement[count];
                    System.Array.Copy(sanitized, result, count);
                    return result;
                }
            }

            switch (fallbackArchetype)
            {
                case EnemyArchetype.Defender:
                    return new[] { BattleElement.Fire, BattleElement.Ice };
                case EnemyArchetype.ElitePredator:
                    return new[] { BattleElement.Fire, BattleElement.Lightning, BattleElement.Wind };
                case EnemyArchetype.BossObserver:
                    return new[] { BattleElement.Fire, BattleElement.Lightning, BattleElement.Light };
                case EnemyArchetype.Drone:
                    return new[] { BattleElement.Lightning, BattleElement.Light };
                default:
                    int offset = (int)(StableHash(stableId) % 6);
                    return new[]
                    {
                        (BattleElement)offset,
                        (BattleElement)((offset + 3) % 6)
                    };
            }
        }

        static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return hash;
            }
        }
    }

    [CreateAssetMenu(fileName = "Stage", menuName = "Starfall/Battle Stage")]
    public sealed class StageData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] string stageId = "stage_01";
        [SerializeField] string chapter = "CHAPTER 1";
        [SerializeField] string displayName = "첫 번째 작전";
        [SerializeField, TextArea(2, 4)] string description = "도시 외곽에 나타난 적을 정리하세요.";

        [Header("Enemies")]
        [SerializeField] string enemyName = "그림자 잔재";
        [SerializeField, Range(1, 3)] int enemyCount = 3;
        [SerializeField, Min(1)] int enemyLevel = 1;
        [SerializeField, Min(100)] int enemyMaxHp = 1200;
        [SerializeField, Min(1)] int enemyAttack = 120;
        [SerializeField, Min(1)] int enemySpeed = 50;
        [SerializeField] bool bossStage;

        [Header("Battle MVP Enemies")]
        [SerializeField] EnemyArchetype enemyArchetype = EnemyArchetype.Auto;
        [SerializeField] BattleElement[] enemyWeaknesses;
        [SerializeField, Min(0), Tooltip("0이면 적 유형 기본값을 사용합니다.")]
        int enemyDefense;
        [SerializeField, Min(0), Tooltip("0이면 적 유형 기본값을 사용합니다.")]
        int enemyMaxBreak;
        [SerializeField, Range(0f, 1f)] float enemyDelayResistance;
        [SerializeField, Range(0f, 1f)] float enemyEffectResistance;
        [SerializeField, Range(0, 5)] int initialSkillPoints = 3;
        [SerializeField, Tooltip("비어 있으면 위의 기존 단일 적 데이터를 반복 사용합니다.")]
        StageEnemyEntry[] enemyLineup;
        [SerializeField] bool showsDangerWarning;

        [Header("Boss Phase 2")]
        [SerializeField] bool bossPhaseTwoEnabled = true;
        [SerializeField, Range(.1f, .9f)] float bossPhaseTwoThreshold = .5f;
        [SerializeField, Min(1f)] float bossPhaseTwoAttackMultiplier = 1.15f;
        [SerializeField, Min(0)] int bossPhaseTwoSpeedBonus = 10;
        [SerializeField, Range(0, 2)] int bossPhaseTwoSummonCount = 2;

        [Header("Requirements & Reward")]
        [SerializeField, Min(0)] int recommendedPower = 4000;
        [SerializeField, Min(0)] int rewardCredits = 5000;
        [SerializeField, Min(0)] int rewardSkillMaterials = 10;

        public string Id => string.IsNullOrWhiteSpace(stageId) ? name : stageId;
        public string Chapter => chapter;
        public string DisplayName => displayName;
        public string Description => description;
        public string EnemyName => enemyName;
        public int EnemyCount => ConfiguredEnemyCount > 0
            ? ConfiguredEnemyCount : Mathf.Clamp(enemyCount, 1, 3);
        public int EnemyLevel => enemyLevel;
        public int EnemyMaxHp => enemyMaxHp;
        public int EnemyAttack => enemyAttack;
        public int EnemySpeed => enemySpeed;
        public bool BossStage => bossStage;
        public int RecommendedPower => recommendedPower;
        public int RewardCredits => rewardCredits;
        public int RewardSkillMaterials => rewardSkillMaterials;
        public EnemyArchetype EnemyArchetype => ResolveEnemyArchetype();
        public BattleElement[] EnemyWeaknesses => StageEnemyEntry.ResolveWeaknesses(
            enemyWeaknesses, EnemyArchetype, Id);
        public int EnemyDefense => enemyDefense > 0
            ? enemyDefense : DefaultDefense(EnemyArchetype);
        public int EnemyMaxBreak => enemyMaxBreak > 0
            ? enemyMaxBreak : DefaultMaxBreak(EnemyArchetype);
        public float EnemyDelayResistance => enemyDelayResistance > 0f
            ? Mathf.Clamp01(enemyDelayResistance) : DefaultDelayResistance(EnemyArchetype);
        public float EnemyEffectResistance => enemyEffectResistance > 0f
            ? Mathf.Clamp01(enemyEffectResistance) : DefaultEffectResistance(EnemyArchetype);
        public int InitialSkillPoints => Mathf.Clamp(initialSkillPoints, 0, 5);
        public StageEnemyEntry[] EnemyLineup => BuildEnemyLineup();
        public bool ShowsDangerWarning => showsDangerWarning
            || EnemyArchetype == EnemyArchetype.ElitePredator
            || EnemyArchetype == EnemyArchetype.BossObserver;
        public bool BossPhaseTwoEnabled => bossStage && bossPhaseTwoEnabled;
        public float BossPhaseTwoThreshold => Mathf.Clamp(bossPhaseTwoThreshold, .1f, .9f);
        public float BossPhaseTwoAttackMultiplier => Mathf.Max(1f, bossPhaseTwoAttackMultiplier);
        public int BossPhaseTwoSpeedBonus => Mathf.Max(0, bossPhaseTwoSpeedBonus);
        public int BossPhaseTwoSummonCount => Mathf.Clamp(bossPhaseTwoSummonCount, 0, 2);

        void OnValidate()
        {
            enemyCount = Mathf.Clamp(enemyCount, 1, 3);
            enemyLevel = Mathf.Max(1, enemyLevel);
            enemyMaxHp = Mathf.Max(100, enemyMaxHp);
            enemyAttack = Mathf.Max(1, enemyAttack);
            enemySpeed = Mathf.Max(1, enemySpeed);
            enemyArchetype = (EnemyArchetype)Mathf.Clamp((int)enemyArchetype,
                (int)EnemyArchetype.Auto, (int)EnemyArchetype.BossObserver);
            enemyDefense = Mathf.Max(0, enemyDefense);
            enemyMaxBreak = Mathf.Max(0, enemyMaxBreak);
            enemyDelayResistance = Mathf.Clamp01(enemyDelayResistance);
            enemyEffectResistance = Mathf.Clamp01(enemyEffectResistance);
            initialSkillPoints = Mathf.Clamp(initialSkillPoints, 0, 5);
            bossPhaseTwoThreshold = Mathf.Clamp(bossPhaseTwoThreshold, .1f, .9f);
            bossPhaseTwoAttackMultiplier = Mathf.Max(1f, bossPhaseTwoAttackMultiplier);
            bossPhaseTwoSpeedBonus = Mathf.Max(0, bossPhaseTwoSpeedBonus);
            bossPhaseTwoSummonCount = Mathf.Clamp(bossPhaseTwoSummonCount, 0, 2);
            if (enemyLineup == null) return;
            foreach (StageEnemyEntry entry in enemyLineup)
                if (entry != null) entry.Sanitize();
        }

        int ConfiguredEnemyCount
        {
            get
            {
                if (enemyLineup == null || enemyLineup.Length == 0) return 0;
                int count = 0;
                for (int i = 0; i < enemyLineup.Length && count < 3; i++)
                    if (enemyLineup[i] != null) count++;
                return count;
            }
        }

        StageEnemyEntry[] BuildEnemyLineup()
        {
            int configuredCount = ConfiguredEnemyCount;
            if (configuredCount > 0)
            {
                var configured = new StageEnemyEntry[configuredCount];
                int output = 0;
                for (int i = 0; i < enemyLineup.Length && output < configured.Length; i++)
                    if (enemyLineup[i] != null) configured[output++] = enemyLineup[i];
                return configured;
            }

            int count = Mathf.Clamp(enemyCount, 1, 3);
            var fallback = new StageEnemyEntry[count];
            for (int i = 0; i < count; i++)
            {
                float scale = 1f + i * .06f;
                string suffix = count > 1 ? " " + (i + 1) : string.Empty;
                fallback[i] = StageEnemyEntry.CreateFallback(Id + "_enemy_" + i,
                    EnemyName + suffix, EnemyArchetype,
                    Mathf.RoundToInt(enemyMaxHp * scale),
                    Mathf.RoundToInt(enemyAttack * scale), EnemyDefense,
                    Mathf.Max(1, enemySpeed - i * 2), EnemyMaxBreak,
                    EnemyWeaknesses, EnemyDelayResistance, EnemyEffectResistance,
                    ShowsDangerWarning);
            }
            return fallback;
        }

        EnemyArchetype ResolveEnemyArchetype()
        {
            if (enemyArchetype != EnemyArchetype.Auto) return enemyArchetype;
            if (bossStage) return EnemyArchetype.BossObserver;
            if (enemyCount == 1 && (enemyMaxHp >= 3500 || recommendedPower >= 8000))
                return EnemyArchetype.ElitePredator;
            return StableHash(Id) % 3 == 0
                ? EnemyArchetype.Defender : EnemyArchetype.Drone;
        }

        static int DefaultDefense(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Defender: return 70;
                case EnemyArchetype.ElitePredator: return 100;
                case EnemyArchetype.BossObserver: return 130;
                default: return 40;
            }
        }

        static int DefaultMaxBreak(EnemyArchetype archetype)
        {
            switch (archetype)
            {
                case EnemyArchetype.Defender: return 90;
                case EnemyArchetype.ElitePredator: return 240;
                case EnemyArchetype.BossObserver: return 360;
                default: return 60;
            }
        }

        static float DefaultDelayResistance(EnemyArchetype archetype)
        {
            if (archetype == EnemyArchetype.BossObserver) return .5f;
            return archetype == EnemyArchetype.ElitePredator ? .3f : 0f;
        }

        static float DefaultEffectResistance(EnemyArchetype archetype)
        {
            return archetype == EnemyArchetype.BossObserver ? .7f : 0f;
        }

        static uint StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value ?? string.Empty)
                    hash = (hash ^ character) * 16777619;
                return hash;
            }
        }
    }
}
