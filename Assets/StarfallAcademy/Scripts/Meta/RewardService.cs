using System;
using System.Security.Cryptography;
using System.Text;

namespace StarfallAcademy.Lobby
{
    public enum RewardGrantStatus
    {
        Granted,
        DuplicateTransaction,
        InvalidTransactionId,
        InvalidReward,
        CommitFailed
    }

    public readonly struct RewardGrantResult
    {
        public RewardGrantResult(RewardGrantStatus status, RewardBundle reward,
            int credits, int skillMaterials, AccountExperienceResult experienceResult)
            : this(status, reward, credits, skillMaterials, 0, experienceResult)
        {
        }

        public RewardGrantResult(RewardGrantStatus status, RewardBundle reward,
            int credits, int skillMaterials, int premiumCurrency,
            AccountExperienceResult experienceResult)
        {
            Status = status;
            Reward = reward;
            Credits = credits;
            SkillMaterials = skillMaterials;
            PremiumCurrency = premiumCurrency;
            ExperienceResult = experienceResult;
        }

        public RewardGrantStatus Status { get; }
        public RewardBundle Reward { get; }
        public int Credits { get; }
        public int SkillMaterials { get; }
        public int PremiumCurrency { get; }
        public AccountExperienceResult ExperienceResult { get; }
        public bool Succeeded => Status == RewardGrantStatus.Granted;
        public bool AlreadyProcessed => Status == RewardGrantStatus.DuplicateTransaction;
    }

    // 기존 PlayerWallet 키와 공존하며 transactionId 단위로 중복 지급을 차단합니다.
    public sealed class RewardService
    {
        const string CreditsKey = "StarfallAcademy.Credits";
        const string SkillMaterialsKey = "StarfallAcademy.SkillMaterials";
        const string PremiumCurrencyKey = "StarfallAcademy.PremiumCurrency";
        const string ProcessedTransactionPrefix = "StarfallAcademy.Meta.Reward.Transaction.";

        static readonly object GlobalSyncRoot = new object();
        readonly IMetaStorage storage;
        readonly PlayerProfileService profile;

        public static RewardService Default { get; } = new RewardService(
            PlayerPrefsMetaStorage.Shared, PlayerProfileService.Default);

        public RewardService(IMetaStorage storage, PlayerProfileService profile = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.profile = profile ?? new PlayerProfileService(storage);
        }

        public int Credits => Math.Max(0, storage.GetInt(CreditsKey, PlayerWallet.DefaultCredits));

        public int SkillMaterials =>
            Math.Max(0, storage.GetInt(SkillMaterialsKey, PlayerWallet.DefaultSkillMaterials));

        public int PremiumCurrency =>
            Math.Max(0, storage.GetInt(PremiumCurrencyKey, PlayerWallet.DefaultPremiumCurrency));

        public PlayerProfileService Profile => profile;

        public RewardGrantResult GrantReward(string transactionId, RewardBundle reward)
        {
            return GrantReward(transactionId, reward, null, null);
        }

        // Battle results supply both commit and rollback participants so related
        // PlayerPrefs state shares the reward transaction's save and recovery path.
        internal RewardGrantResult GrantReward(string transactionId, RewardBundle reward,
            Action beforeCommit, Action rollbackParticipant)
        {
            string normalizedId = transactionId?.Trim();
            if (string.IsNullOrEmpty(normalizedId))
                return Result(RewardGrantStatus.InvalidTransactionId, reward);
            if (!reward.IsValid)
                return Result(RewardGrantStatus.InvalidReward, reward);

            lock (GlobalSyncRoot)
            {
                string transactionKey = GetTransactionKey(normalizedId);
                if (storage.GetInt(transactionKey, 0) == 1)
                    return Result(RewardGrantStatus.DuplicateTransaction, reward);

                bool hadCredits = storage.HasKey(CreditsKey);
                bool hadMaterials = storage.HasKey(SkillMaterialsKey);
                bool hadPremium = storage.HasKey(PremiumCurrencyKey);
                bool hadTransaction = storage.HasKey(transactionKey);
                int previousCredits = Credits;
                int previousMaterials = SkillMaterials;
                int previousPremium = PremiumCurrency;
                int previousTransaction = storage.GetInt(transactionKey, 0);
                int previousLevel = profile.Level;
                int previousExperience = profile.Experience;

                try
                {
                    storage.SetInt(CreditsKey, SaturatingAdd(previousCredits, reward.Credits));
                    storage.SetInt(SkillMaterialsKey,
                        SaturatingAdd(previousMaterials, reward.SkillMaterials));
                    storage.SetInt(PremiumCurrencyKey,
                        SaturatingAdd(previousPremium, reward.PremiumCurrency));

                    AccountExperienceResult experienceResult = reward.AccountExperience > 0
                        ? profile.AddExperienceDeferred(reward.AccountExperience)
                        : UnchangedExperienceResult();

                    beforeCommit?.Invoke();
                    storage.SetInt(transactionKey, 1);
                    storage.Save();
                    return new RewardGrantResult(RewardGrantStatus.Granted, reward,
                        Credits, SkillMaterials, PremiumCurrency, experienceResult);
                }
                catch (Exception)
                {
                    try
                    {
                        RestoreInt(CreditsKey, hadCredits, previousCredits);
                        RestoreInt(SkillMaterialsKey, hadMaterials, previousMaterials);
                        RestoreInt(PremiumCurrencyKey, hadPremium, previousPremium);
                        profile.RestoreStateDeferred(previousLevel, previousExperience);
                        RestoreInt(transactionKey, hadTransaction, previousTransaction);
                        rollbackParticipant?.Invoke();
                        storage.Save();
                    }
                    catch (Exception)
                    {
                        // Values are staged back to their snapshots whenever the storage permits it.
                        // A later successful save will persist the restored state.
                    }
                    return Result(RewardGrantStatus.CommitFailed, reward);
                }
            }
        }

        public bool TryGrantReward(string transactionId, RewardBundle reward,
            out RewardGrantResult result)
        {
            result = GrantReward(transactionId, reward);
            return result.Succeeded;
        }

        public bool HasProcessed(string transactionId)
        {
            string normalizedId = transactionId?.Trim();
            return !string.IsNullOrEmpty(normalizedId)
                && storage.GetInt(GetTransactionKey(normalizedId), 0) == 1;
        }

        // 일반 게임 코드에서 사용할 기본 PlayerPrefs 저장소 편의 API입니다.
        public static RewardGrantResult Grant(string transactionId, RewardBundle reward) =>
            Default.GrantReward(transactionId, reward);

        public static bool IsProcessed(string transactionId) => Default.HasProcessed(transactionId);

        RewardGrantResult Result(RewardGrantStatus status, RewardBundle reward) =>
            new RewardGrantResult(status, reward, Credits, SkillMaterials, PremiumCurrency,
                UnchangedExperienceResult());

        AccountExperienceResult UnchangedExperienceResult() => new AccountExperienceResult(
            0, 0, profile.Level, profile.Level, profile.Experience, Array.Empty<AccountFeature>());

        void RestoreInt(string key, bool hadValue, int value)
        {
            if (hadValue) storage.SetInt(key, value);
            else storage.DeleteKey(key);
        }

        static int SaturatingAdd(int current, int amount)
        {
            long total = (long)Math.Max(0, current) + Math.Max(0, amount);
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        static string GetTransactionKey(string transactionId)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(transactionId));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return ProcessedTransactionPrefix + builder;
            }
        }
    }
}
