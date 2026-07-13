using System;
using System.Collections.Generic;
using System.Globalization;

namespace StarfallAcademy.Lobby
{
    public enum DailyMissionType
    {
        Login,
        StaminaSpent,
        Enhancement,
        BattleCompleted
    }

    public sealed class DailyMissionDefinition
    {
        public DailyMissionDefinition(string id, DailyMissionType type, int target, RewardBundle reward)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("임무 ID가 비어 있습니다.", nameof(id));
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target));
            if (!reward.IsValid) throw new ArgumentException("임무 보상이 유효하지 않습니다.", nameof(reward));
            Id = id.Trim();
            Type = type;
            Target = target;
            Reward = reward;
        }

        public string Id { get; }
        public DailyMissionType Type { get; }
        public int Target { get; }
        public RewardBundle Reward { get; }
    }

    public readonly struct DailyMissionProgress
    {
        public DailyMissionProgress(DailyMissionDefinition definition, int current, bool claimed)
        {
            Definition = definition;
            Current = current;
            Claimed = claimed;
        }

        public DailyMissionDefinition Definition { get; }
        public int Current { get; }
        public bool IsComplete => Definition != null && Current >= Definition.Target;
        public bool Claimed { get; }
        public bool CanClaim => IsComplete && !Claimed;
    }

    public enum MissionClaimStatus
    {
        Claimed,
        NotCompleted,
        AlreadyClaimed,
        UnknownMission,
        RewardRejected
    }

    public readonly struct MissionClaimResult
    {
        public MissionClaimResult(MissionClaimStatus status, DailyMissionProgress progress,
            RewardGrantResult rewardResult)
        {
            Status = status;
            Progress = progress;
            RewardResult = rewardResult;
        }

        public MissionClaimStatus Status { get; }
        public DailyMissionProgress Progress { get; }
        public RewardGrantResult RewardResult { get; }
        public bool Succeeded => Status == MissionClaimStatus.Claimed;
    }

    // UTC 날짜별 로그인·행동력·강화·전투 카운터와 보상 수령을 관리합니다.
    public sealed class MissionService
    {
        const string DayKey = "StarfallAcademy.Meta.Mission.UtcDay";
        const string ProgressPrefix = "StarfallAcademy.Meta.Mission.Progress.";
        const string ClaimedPrefix = "StarfallAcademy.Meta.Mission.Claimed.";

        readonly object syncRoot = new object();
        readonly IMetaStorage storage;
        readonly IUtcClock clock;
        readonly RewardService rewardService;
        readonly List<DailyMissionDefinition> definitions;
        readonly Dictionary<DailyMissionType, DailyMissionDefinition> definitionsByType;

        public static MissionService Default { get; } = new MissionService(
            PlayerPrefsMetaStorage.Shared, SystemUtcClock.Shared, RewardService.Default);

        public MissionService(IMetaStorage storage, IUtcClock clock, RewardService rewardService,
            IEnumerable<DailyMissionDefinition> definitions = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
            this.definitions = new List<DailyMissionDefinition>(definitions ?? CreateDefaultDefinitions());
            definitionsByType = new Dictionary<DailyMissionType, DailyMissionDefinition>();

            for (int i = 0; i < this.definitions.Count; i++)
            {
                DailyMissionDefinition definition = this.definitions[i]
                    ?? throw new ArgumentException("임무 정의에 null이 포함되어 있습니다.", nameof(definitions));
                if (definitionsByType.ContainsKey(definition.Type))
                    throw new ArgumentException("같은 종류의 일일 임무가 중복되었습니다.", nameof(definitions));
                definitionsByType.Add(definition.Type, definition);
            }
        }

        public IReadOnlyList<DailyMissionDefinition> Definitions => definitions;

        public DailyMissionProgress AddProgress(DailyMissionType type, int amount = 1)
        {
            lock (syncRoot)
            {
                EnsureCurrentDay();
                if (!definitionsByType.TryGetValue(type, out DailyMissionDefinition definition))
                    return default;
                if (amount <= 0) return ReadProgress(definition);

                int current = storage.GetInt(ProgressKey(definition), 0);
                long next = (long)Math.Max(0, current) + amount;
                current = (int)Math.Min(definition.Target, Math.Min(int.MaxValue, next));
                storage.SetInt(ProgressKey(definition), current);
                storage.Save();
                return ReadProgress(definition);
            }
        }

        public DailyMissionProgress GetProgress(DailyMissionType type)
        {
            lock (syncRoot)
            {
                EnsureCurrentDay();
                return definitionsByType.TryGetValue(type, out DailyMissionDefinition definition)
                    ? ReadProgress(definition)
                    : default;
            }
        }

        public bool HasClaimableReward()
        {
            lock (syncRoot)
            {
                EnsureCurrentDay();
                for (int i = 0; i < definitions.Count; i++)
                {
                    if (ReadProgress(definitions[i]).CanClaim) return true;
                }
                return false;
            }
        }

        public MissionClaimResult ClaimReward(DailyMissionType type)
        {
            lock (syncRoot)
            {
                EnsureCurrentDay();
                if (!definitionsByType.TryGetValue(type, out DailyMissionDefinition definition))
                    return new MissionClaimResult(MissionClaimStatus.UnknownMission, default, default);

                DailyMissionProgress progress = ReadProgress(definition);
                if (progress.Claimed)
                    return new MissionClaimResult(MissionClaimStatus.AlreadyClaimed, progress, default);
                if (!progress.IsComplete)
                    return new MissionClaimResult(MissionClaimStatus.NotCompleted, progress, default);

                string day = CurrentDayId();
                string transactionId = "daily-mission:" + day + ":" + definition.Id;
                RewardGrantResult rewardResult = rewardService.GrantReward(
                    transactionId, definition.Reward);

                // 지급 후 저장이 끊긴 경우에도 transactionId를 확인해 수령 상태를 복구합니다.
                if (!rewardResult.Succeeded && !rewardResult.AlreadyProcessed)
                    return new MissionClaimResult(MissionClaimStatus.RewardRejected, progress, rewardResult);

                storage.SetInt(ClaimedKey(definition), 1);
                storage.Save();
                return new MissionClaimResult(MissionClaimStatus.Claimed,
                    ReadProgress(definition), rewardResult);
            }
        }

        public static void RecordLogin()
        {
            DailyMissionProgress progress = Default.GetProgress(DailyMissionType.Login);
            if (progress.Definition != null && !progress.IsComplete)
                Default.AddProgress(DailyMissionType.Login);
        }

        public static void RecordEnhancement(int amount = 1) =>
            Default.AddProgress(DailyMissionType.Enhancement, amount);

        public static void RecordBattleCompleted() =>
            Default.AddProgress(DailyMissionType.BattleCompleted);

        public static void RecordStaminaSpent(int amount) =>
            Default.AddProgress(DailyMissionType.StaminaSpent, amount);

        public static DailyMissionProgress GetDailyProgress(DailyMissionType type) =>
            Default.GetProgress(type);

        public static MissionClaimResult ClaimDailyReward(DailyMissionType type) =>
            Default.ClaimReward(type);

        public static bool HasClaimableDailyReward() => Default.HasClaimableReward();

        public static IReadOnlyList<DailyMissionDefinition> CreateDefaultDefinitions()
        {
            // 수치는 MVP 기본값이며 생성자 주입으로 밸런스 테이블을 교체할 수 있습니다.
            return new[]
            {
                new DailyMissionDefinition("login", DailyMissionType.Login, 1,
                    new RewardBundle(5000, 0, 20)),
                new DailyMissionDefinition("stamina-spent", DailyMissionType.StaminaSpent, 30,
                    new RewardBundle(5000, 10, 30)),
                new DailyMissionDefinition("enhancement", DailyMissionType.Enhancement, 1,
                    new RewardBundle(5000, 10, 30)),
                new DailyMissionDefinition("battle-completed", DailyMissionType.BattleCompleted, 3,
                    new RewardBundle(10000, 0, 50))
            };
        }

        DailyMissionProgress ReadProgress(DailyMissionDefinition definition)
        {
            int current = Math.Max(0, storage.GetInt(ProgressKey(definition), 0));
            bool claimed = storage.GetInt(ClaimedKey(definition), 0) == 1;
            return new DailyMissionProgress(definition, current, claimed);
        }

        void EnsureCurrentDay()
        {
            string currentDay = CurrentDayId();
            if (string.Equals(storage.GetString(DayKey, string.Empty), currentDay,
                StringComparison.Ordinal)) return;

            storage.SetString(DayKey, currentDay);
            for (int i = 0; i < definitions.Count; i++)
            {
                storage.SetInt(ProgressKey(definitions[i]), 0);
                storage.SetInt(ClaimedKey(definitions[i]), 0);
            }
            storage.Save();
        }

        string CurrentDayId()
        {
            DateTime now = clock.UtcNow;
            if (now.Kind == DateTimeKind.Local) now = now.ToUniversalTime();
            else if (now.Kind == DateTimeKind.Unspecified)
                now = DateTime.SpecifyKind(now, DateTimeKind.Utc);
            return now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        static string ProgressKey(DailyMissionDefinition definition) => ProgressPrefix + definition.Id;
        static string ClaimedKey(DailyMissionDefinition definition) => ClaimedPrefix + definition.Id;
    }
}
