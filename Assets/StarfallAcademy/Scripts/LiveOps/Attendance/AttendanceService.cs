using System;
using System.Collections.Generic;
using System.Globalization;

namespace StarfallAcademy.Lobby
{
    public enum AttendanceClaimStatus
    {
        Claimed,
        MissingCampaign,
        InvalidSchedule,
        NotActive,
        NoRewardDays,
        AlreadyClaimedToday,
        CampaignCompleted,
        InvalidReward,
        RewardRejected,
        SaveFailed
    }

    public readonly struct AttendanceClaimResult
    {
        public AttendanceClaimResult(AttendanceClaimStatus status,
            AttendanceCampaignData campaign, AttendanceDayDefinition day,
            RewardGrantResult rewardResult, string message)
        {
            Status = status;
            Campaign = campaign;
            Day = day;
            RewardResult = rewardResult;
            Message = message ?? string.Empty;
        }

        public AttendanceClaimStatus Status { get; }
        public AttendanceCampaignData Campaign { get; }
        public AttendanceDayDefinition Day { get; }
        public RewardGrantResult RewardResult { get; }
        public string Message { get; }
        public bool Succeeded => Status == AttendanceClaimStatus.Claimed;
    }

    [Serializable]
    public sealed class AttendanceProgress
    {
        public AttendanceProgress()
        {
        }

        internal AttendanceProgress(string campaignId, string lastAcceptedUtcDay,
            string lastClaimedUtcDay, int currentSequenceIndex, bool completed,
            int claimCount, int resetGeneration, string lastModifiedUtc)
        {
            this.campaignId = campaignId ?? string.Empty;
            this.lastAcceptedUtcDay = lastAcceptedUtcDay ?? string.Empty;
            this.lastClaimedUtcDay = lastClaimedUtcDay ?? string.Empty;
            this.currentSequenceIndex = Math.Max(0, currentSequenceIndex);
            this.completed = completed;
            this.claimCount = Math.Max(0, claimCount);
            this.resetGeneration = Math.Max(0, resetGeneration);
            this.lastModifiedUtc = lastModifiedUtc ?? string.Empty;
        }

        [UnityEngine.SerializeField] string campaignId;
        [UnityEngine.SerializeField] string lastAcceptedUtcDay;
        [UnityEngine.SerializeField] string lastClaimedUtcDay;
        [UnityEngine.SerializeField] int currentSequenceIndex;
        [UnityEngine.SerializeField] bool completed;
        [UnityEngine.SerializeField] int claimCount;
        [UnityEngine.SerializeField] int resetGeneration;
        [UnityEngine.SerializeField] string lastModifiedUtc;

        public string CampaignId => campaignId ?? string.Empty;
        public string LastAcceptedUtcDay => lastAcceptedUtcDay ?? string.Empty;
        public string LastClaimedUtcDay => lastClaimedUtcDay ?? string.Empty;
        public int CurrentSequenceIndex => Math.Max(0, currentSequenceIndex);
        public bool Completed => completed;
        public int ClaimCount => Math.Max(0, claimCount);
        public int ResetGeneration => Math.Max(0, resetGeneration);
        public string LastModifiedUtc => lastModifiedUtc ?? string.Empty;
    }

    [Serializable]
    public sealed class AttendanceSaveSnapshot
    {
        [UnityEngine.SerializeField] int version = 1;
        [UnityEngine.SerializeField] string capturedAtUtc;
        [UnityEngine.SerializeField] List<AttendanceProgress> campaigns =
            new List<AttendanceProgress>();
        [UnityEngine.SerializeField] List<string> claimedCampaignIds =
            new List<string>();

        public int Version => version;
        public string CapturedAtUtc => capturedAtUtc ?? string.Empty;
        public IReadOnlyList<AttendanceProgress> Campaigns => campaigns;
        public IReadOnlyList<string> ClaimedCampaignIds => claimedCampaignIds;

        internal void SetCapturedAt(string value) => capturedAtUtc = value ?? string.Empty;
        internal void Add(AttendanceProgress progress)
        {
            if (progress == null) return;
            campaigns.Add(progress);
            if (progress.Completed && !claimedCampaignIds.Contains(progress.CampaignId))
                claimedCampaignIds.Add(progress.CampaignId);
        }
    }

    /// <summary>
    /// Sequential attendance progression. Every campaign owns an independent UTC high-water
    /// mark, so changing the device clock backwards cannot mint another daily claim.
    /// </summary>
    public sealed class AttendanceService
    {
        const string Prefix = "StarfallAcademy.LiveOps.Attendance.";
        const string LastAcceptedSuffix = ".LastAcceptedUtcDay";
        const string LastClaimedSuffix = ".LastClaimedUtcDay";
        const string SequenceSuffix = ".SequenceIndex";
        const string CompletedSuffix = ".Completed";
        const string ClaimCountSuffix = ".ClaimCount";
        const string GenerationSuffix = ".ResetGeneration";
        const string LastModifiedSuffix = ".LastModifiedUtc";

        readonly object syncRoot = new object();
        readonly IMetaStorage storage;
        readonly IUtcClock clock;
        readonly RewardPackageService rewardService;

        public static AttendanceService Default { get; } = new AttendanceService(
            PlayerPrefsMetaStorage.Shared, ContentUtcClock.Shared, RewardPackageService.Default);

        public AttendanceService(IMetaStorage storage, IUtcClock clock,
            RewardPackageService rewardService)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.rewardService = rewardService ?? throw new ArgumentNullException(nameof(rewardService));
        }

        public AttendanceProgress GetProgress(AttendanceCampaignData campaign)
        {
            lock (syncRoot)
                return campaign == null ? null : ReadProgress(campaign, true);
        }

        public bool CanClaim(AttendanceCampaignData campaign) =>
            TryGetClaimableDay(campaign, out _, out _);

        public bool TryGetClaimableDay(AttendanceCampaignData campaign,
            out AttendanceDayDefinition day, out string reason)
        {
            lock (syncRoot)
            {
                day = null;
                AttendanceClaimStatus status = ResolveClaimable(campaign,
                    out AttendanceProgress progress, out day);
                reason = StatusMessage(status, progress, day);
                return status == AttendanceClaimStatus.Claimed;
            }
        }

        public bool HasClaimableReward(AttendanceCampaignDatabase database)
        {
            if (database == null || database.Campaigns == null) return false;
            for (int i = 0; i < database.Campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = database.Campaigns[i];
                if (campaign != null && CanClaim(campaign)) return true;
            }
            return false;
        }

        public AttendanceClaimResult Claim(AttendanceCampaignData campaign)
        {
            lock (syncRoot)
            {
                AttendanceClaimStatus status = ResolveClaimable(campaign,
                    out AttendanceProgress progress, out AttendanceDayDefinition day);
                if (status != AttendanceClaimStatus.Claimed)
                    return Result(status, campaign, day, default,
                        StatusMessage(status, progress, day));

                if (day.Reward == null || !day.Reward.IsValid)
                    return Result(AttendanceClaimStatus.InvalidReward, campaign, day,
                        default, "출석 보상 데이터가 올바르지 않습니다.");

                int nextSequence = progress.CurrentSequenceIndex + 1;
                bool completed = false;
                if (nextSequence >= campaign.DayCount)
                {
                    if (campaign.CycleMode == AttendanceCycleMode.Looping) nextSequence = 0;
                    else
                    {
                        nextSequence = campaign.DayCount;
                        completed = true;
                    }
                }
                int nextClaimCount = progress.ClaimCount >= int.MaxValue
                    ? int.MaxValue : progress.ClaimCount + 1;
                string id = campaign.CampaignId;
                string lastClaimedKey = Key(id, LastClaimedSuffix);
                string sequenceKey = Key(id, SequenceSuffix);
                string completedKey = Key(id, CompletedSuffix);
                string claimCountKey = Key(id, ClaimCountSuffix);
                string modifiedKey = Key(id, LastModifiedSuffix);
                bool hadLastClaimed = storage.HasKey(lastClaimedKey);
                bool hadSequence = storage.HasKey(sequenceKey);
                bool hadCompleted = storage.HasKey(completedKey);
                bool hadClaimCount = storage.HasKey(claimCountKey);
                bool hadModified = storage.HasKey(modifiedKey);
                string previousLastClaimed = storage.GetString(lastClaimedKey, string.Empty);
                int previousSequence = storage.GetInt(sequenceKey, 0);
                int previousCompleted = storage.GetInt(completedKey, 0);
                int previousClaimCount = storage.GetInt(claimCountKey, 0);
                string previousModified = storage.GetString(modifiedKey, string.Empty);
                Action stageProgress = () =>
                {
                    storage.SetString(lastClaimedKey, progress.LastAcceptedUtcDay);
                    storage.SetInt(sequenceKey, nextSequence);
                    storage.SetInt(completedKey, completed ? 1 : 0);
                    storage.SetInt(claimCountKey, nextClaimCount);
                    Touch(id);
                };
                Action rollbackProgress = () =>
                {
                    RestoreString(lastClaimedKey, hadLastClaimed, previousLastClaimed);
                    RestoreInt(sequenceKey, hadSequence, previousSequence);
                    RestoreInt(completedKey, hadCompleted, previousCompleted);
                    RestoreInt(claimCountKey, hadClaimCount, previousClaimCount);
                    RestoreString(modifiedKey, hadModified, previousModified);
                };
                string transactionId = "attendance:" + id + ":"
                    + progress.ResetGeneration.ToString(CultureInfo.InvariantCulture) + ":"
                    + progress.ClaimCount.ToString(CultureInfo.InvariantCulture);
                RewardGrantResult rewardResult = rewardService.Grant(transactionId, day.Reward,
                    stageProgress, rollbackProgress);
                if (rewardResult.Succeeded)
                    return Result(AttendanceClaimStatus.Claimed, campaign, day,
                        rewardResult, day.Reward.Summary + " 수령 완료");

                // A previous process may have committed the reward transaction and exited before
                // this service observed the result. The claim ordinal keeps the transaction ID
                // stable across UTC-day changes, so replay can only repair progress, never regrant.
                if (rewardResult.AlreadyProcessed)
                {
                    try
                    {
                        stageProgress();
                        storage.Save();
                        return Result(AttendanceClaimStatus.Claimed, campaign, day,
                            rewardResult, day.Reward.Summary + " 수령 상태 복구 완료");
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            rollbackProgress();
                            storage.Save();
                        }
                        catch (Exception)
                        {
                            // Keep the original repair failure as the reported result.
                        }
                        return Result(AttendanceClaimStatus.SaveFailed, campaign, day,
                            rewardResult, "출석 진행도를 저장하지 못했습니다: " + exception.Message);
                    }
                }
                return Result(AttendanceClaimStatus.RewardRejected, campaign, day,
                    rewardResult, "출석 보상과 진행도를 저장하지 못했습니다.");
            }
        }

        public AttendanceSaveSnapshot CaptureSnapshot(AttendanceCampaignDatabase database)
        {
            lock (syncRoot)
            {
                var snapshot = new AttendanceSaveSnapshot();
                snapshot.SetCapturedAt(FormatUtc(clock.UtcNow));
                if (database == null || database.Campaigns == null) return snapshot;
                for (int i = 0; i < database.Campaigns.Count; i++)
                {
                    AttendanceCampaignData campaign = database.Campaigns[i];
                    if (campaign != null) snapshot.Add(ReadProgress(campaign, false));
                }
                return snapshot;
            }
        }

        public bool RestoreSnapshot(AttendanceSaveSnapshot snapshot,
            AttendanceCampaignDatabase database)
        {
            if (snapshot == null || database == null) return false;
            lock (syncRoot)
            {
                try
                {
                    var byId = new Dictionary<string, AttendanceProgress>(StringComparer.Ordinal);
                    if (snapshot.Campaigns != null)
                    {
                        for (int i = 0; i < snapshot.Campaigns.Count; i++)
                        {
                            AttendanceProgress progress = snapshot.Campaigns[i];
                            if (progress != null && !string.IsNullOrWhiteSpace(progress.CampaignId))
                                byId[progress.CampaignId] = progress;
                        }
                    }

                    for (int i = 0; i < database.Campaigns.Count; i++)
                    {
                        AttendanceCampaignData campaign = database.Campaigns[i];
                        if (campaign == null) continue;
                        DeleteProgressKeys(campaign.CampaignId, false);
                        if (!byId.TryGetValue(campaign.CampaignId, out AttendanceProgress progress))
                            continue;
                        WriteProgress(progress);
                    }
                    storage.Save();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public void ResetCampaign(AttendanceCampaignData campaign)
        {
            if (campaign == null) return;
            lock (syncRoot)
            {
                string id = campaign.CampaignId;
                int nextGeneration = Math.Max(0,
                    storage.GetInt(Key(id, GenerationSuffix), 0)) + 1;
                DeleteProgressKeys(id, true);
                storage.SetInt(Key(id, GenerationSuffix), nextGeneration);
                Touch(id);
                storage.Save();
            }
        }

        public void ResetAll(AttendanceCampaignDatabase database)
        {
            if (database == null || database.Campaigns == null) return;
            lock (syncRoot)
            {
                for (int i = 0; i < database.Campaigns.Count; i++)
                {
                    AttendanceCampaignData campaign = database.Campaigns[i];
                    if (campaign == null) continue;
                    string id = campaign.CampaignId;
                    int nextGeneration = Math.Max(0,
                        storage.GetInt(Key(id, GenerationSuffix), 0)) + 1;
                    DeleteProgressKeys(id, true);
                    storage.SetInt(Key(id, GenerationSuffix), nextGeneration);
                    Touch(id);
                }
                storage.Save();
            }
        }

        public static IReadOnlyList<string> GetKnownStorageKeys(string campaignId)
        {
            string id = string.IsNullOrWhiteSpace(campaignId) ? "invalid" : campaignId.Trim();
            return new[]
            {
                Key(id, LastAcceptedSuffix), Key(id, LastClaimedSuffix),
                Key(id, SequenceSuffix), Key(id, CompletedSuffix), Key(id, ClaimCountSuffix),
                Key(id, GenerationSuffix), Key(id, LastModifiedSuffix)
            };
        }

        AttendanceClaimStatus ResolveClaimable(AttendanceCampaignData campaign,
            out AttendanceProgress progress, out AttendanceDayDefinition day)
        {
            progress = null;
            day = null;
            if (campaign == null) return AttendanceClaimStatus.MissingCampaign;
            if (campaign.Schedule == null || !campaign.Schedule.IsValid)
                return AttendanceClaimStatus.InvalidSchedule;
            if (!campaign.Schedule.Contains(clock.UtcNow)) return AttendanceClaimStatus.NotActive;
            if (campaign.DayCount <= 0) return AttendanceClaimStatus.NoRewardDays;

            progress = ReadProgress(campaign, true);
            if (campaign.CycleMode == AttendanceCycleMode.FixedPeriod
                && (progress.Completed || progress.CurrentSequenceIndex >= campaign.DayCount))
                return AttendanceClaimStatus.CampaignCompleted;
            if (string.Equals(progress.LastClaimedUtcDay, progress.LastAcceptedUtcDay,
                StringComparison.Ordinal)) return AttendanceClaimStatus.AlreadyClaimedToday;

            int index = progress.CurrentSequenceIndex;
            if (campaign.CycleMode == AttendanceCycleMode.Looping)
                index %= campaign.DayCount;
            day = campaign.GetDay(index);
            return day == null ? AttendanceClaimStatus.NoRewardDays
                : AttendanceClaimStatus.Claimed;
        }

        AttendanceProgress ReadProgress(AttendanceCampaignData campaign, bool advanceHighWater)
        {
            string id = campaign.CampaignId;
            string currentDay = DayId(clock.UtcNow);
            string accepted = storage.GetString(Key(id, LastAcceptedSuffix), string.Empty);
            if (string.IsNullOrEmpty(accepted) || string.CompareOrdinal(currentDay, accepted) > 0)
            {
                accepted = currentDay;
                if (advanceHighWater)
                {
                    storage.SetString(Key(id, LastAcceptedSuffix), accepted);
                    storage.Save();
                }
            }

            int index = Math.Max(0, storage.GetInt(Key(id, SequenceSuffix), 0));
            bool completed = storage.GetInt(Key(id, CompletedSuffix), 0) == 1;
            if (campaign.CycleMode == AttendanceCycleMode.Looping && campaign.DayCount > 0)
                index %= campaign.DayCount;
            else if (campaign.DayCount >= 0)
                index = Math.Min(index, campaign.DayCount);

            return new AttendanceProgress(id, accepted,
                storage.GetString(Key(id, LastClaimedSuffix), string.Empty), index, completed,
                Math.Max(0, storage.GetInt(Key(id, ClaimCountSuffix), 0)),
                Math.Max(0, storage.GetInt(Key(id, GenerationSuffix), 0)),
                storage.GetString(Key(id, LastModifiedSuffix), string.Empty));
        }

        void WriteProgress(AttendanceProgress progress)
        {
            string id = progress.CampaignId;
            storage.SetString(Key(id, LastAcceptedSuffix), progress.LastAcceptedUtcDay);
            storage.SetString(Key(id, LastClaimedSuffix), progress.LastClaimedUtcDay);
            storage.SetInt(Key(id, SequenceSuffix), progress.CurrentSequenceIndex);
            storage.SetInt(Key(id, CompletedSuffix), progress.Completed ? 1 : 0);
            storage.SetInt(Key(id, ClaimCountSuffix), progress.ClaimCount);
            storage.SetInt(Key(id, GenerationSuffix), progress.ResetGeneration);
            storage.SetString(Key(id, LastModifiedSuffix), progress.LastModifiedUtc);
        }

        void DeleteProgressKeys(string campaignId, bool preserveGeneration)
        {
            storage.DeleteKey(Key(campaignId, LastAcceptedSuffix));
            storage.DeleteKey(Key(campaignId, LastClaimedSuffix));
            storage.DeleteKey(Key(campaignId, SequenceSuffix));
            storage.DeleteKey(Key(campaignId, CompletedSuffix));
            storage.DeleteKey(Key(campaignId, ClaimCountSuffix));
            storage.DeleteKey(Key(campaignId, LastModifiedSuffix));
            if (!preserveGeneration) storage.DeleteKey(Key(campaignId, GenerationSuffix));
        }

        void Touch(string campaignId) => storage.SetString(
            Key(campaignId, LastModifiedSuffix), FormatUtc(clock.UtcNow));

        void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) storage.SetInt(key, value);
            else storage.DeleteKey(key);
        }

        void RestoreString(string key, bool hadValue, string value)
        {
            if (hadValue) storage.SetString(key, value);
            else storage.DeleteKey(key);
        }

        static string Key(string campaignId, string suffix) => Prefix
            + (string.IsNullOrWhiteSpace(campaignId) ? "invalid" : campaignId.Trim()) + suffix;

        static string DayId(DateTime value) => ScheduleRange.NormalizeUtc(value)
            .ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        static string FormatUtc(DateTime value) => ScheduleRange.NormalizeUtc(value)
            .ToString("O", CultureInfo.InvariantCulture);

        static AttendanceClaimResult Result(AttendanceClaimStatus status,
            AttendanceCampaignData campaign, AttendanceDayDefinition day,
            RewardGrantResult rewardResult, string message) =>
            new AttendanceClaimResult(status, campaign, day, rewardResult, message);

        static string StatusMessage(AttendanceClaimStatus status,
            AttendanceProgress progress, AttendanceDayDefinition day)
        {
            switch (status)
            {
                case AttendanceClaimStatus.Claimed:
                    return day == null ? "수령할 보상이 있습니다."
                        : "Day " + day.DayNumber + " 보상을 수령할 수 있습니다.";
                case AttendanceClaimStatus.MissingCampaign: return "출석 캠페인이 없습니다.";
                case AttendanceClaimStatus.InvalidSchedule: return "캠페인 기간 설정이 올바르지 않습니다.";
                case AttendanceClaimStatus.NotActive: return "현재 진행 중인 출석 캠페인이 아닙니다.";
                case AttendanceClaimStatus.NoRewardDays: return "등록된 출석 보상이 없습니다.";
                case AttendanceClaimStatus.AlreadyClaimedToday: return "오늘의 출석 보상을 이미 받았습니다.";
                case AttendanceClaimStatus.CampaignCompleted: return "모든 출석 보상을 받았습니다.";
                case AttendanceClaimStatus.InvalidReward: return "출석 보상 데이터가 올바르지 않습니다.";
                case AttendanceClaimStatus.RewardRejected: return "출석 보상을 지급하지 못했습니다.";
                case AttendanceClaimStatus.SaveFailed: return "출석 진행도를 저장하지 못했습니다.";
                default: return progress == null ? string.Empty : progress.CampaignId;
            }
        }
    }
}
