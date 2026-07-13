using System;
using System.Collections.Generic;

namespace StarfallAcademy.Lobby
{
    public enum AccountFeature
    {
        AutoBattle,
        BattleSpeed,
        GrowthDungeon,
        Sweep,
        Equipment,
        WeeklyBoss,
        ChallengeTower
    }

    public readonly struct AccountExperienceResult
    {
        public AccountExperienceResult(int requestedExperience, int appliedExperience,
            int previousLevel, int currentLevel, int currentExperience,
            IReadOnlyList<AccountFeature> newlyUnlockedFeatures)
        {
            RequestedExperience = requestedExperience;
            AppliedExperience = appliedExperience;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            CurrentExperience = currentExperience;
            NewlyUnlockedFeatures = newlyUnlockedFeatures ?? Array.Empty<AccountFeature>();
        }

        public int RequestedExperience { get; }
        public int AppliedExperience { get; }
        public int PreviousLevel { get; }
        public int CurrentLevel { get; }
        public int CurrentExperience { get; }
        public bool LeveledUp => CurrentLevel > PreviousLevel;
        public IReadOnlyList<AccountFeature> NewlyUnlockedFeatures { get; }
    }

    // 계정 경험치 계산과 콘텐츠 해금 규칙을 UI에서 분리한 순수 서비스입니다.
    public sealed class PlayerProfileService
    {
        const string LevelKey = "StarfallAcademy.Meta.Profile.AccountLevel";
        const string ExperienceKey = "StarfallAcademy.Meta.Profile.AccountExperience";

        static readonly AccountFeature[] AllFeatures =
        {
            AccountFeature.AutoBattle,
            AccountFeature.BattleSpeed,
            AccountFeature.GrowthDungeon,
            AccountFeature.Sweep,
            AccountFeature.Equipment,
            AccountFeature.WeeklyBoss,
            AccountFeature.ChallengeTower
        };

        readonly IMetaStorage storage;
        readonly Func<int, int> requiredExperienceResolver;

        public static PlayerProfileService Default { get; } =
            new PlayerProfileService(PlayerPrefsMetaStorage.Shared);

        public PlayerProfileService(IMetaStorage storage, int maximumLevel = 60,
            Func<int, int> requiredExperienceResolver = null)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (maximumLevel < 1) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
            MaximumLevel = maximumLevel;
            this.requiredExperienceResolver = requiredExperienceResolver ?? DefaultRequiredExperience;
        }

        public int MaximumLevel { get; }

        public int Level => Clamp(storage.GetInt(LevelKey, 1), 1, MaximumLevel);

        public int Experience
        {
            get
            {
                if (Level >= MaximumLevel) return 0;
                return Clamp(storage.GetInt(ExperienceKey, 0), 0,
                    GetRequiredExperienceForNextLevel(Level) - 1);
            }
        }

        public static int CurrentLevel => Default.Level;
        public static int CurrentExperience => Default.Experience;

        public int GetRequiredExperienceForNextLevel(int level)
        {
            if (level >= MaximumLevel) return 0;
            int required = requiredExperienceResolver(Math.Max(1, level));
            return Math.Max(1, required);
        }

        public bool IsUnlocked(AccountFeature feature) => Level >= GetRequiredLevel(feature);

        public IReadOnlyList<AccountFeature> GetUnlockedFeatures()
        {
            var result = new List<AccountFeature>();
            for (int i = 0; i < AllFeatures.Length; i++)
            {
                if (IsUnlocked(AllFeatures[i])) result.Add(AllFeatures[i]);
            }
            return result;
        }

        public AccountExperienceResult AddExperience(int amount)
        {
            return ApplyExperience(amount, true);
        }

        internal AccountExperienceResult AddExperienceDeferred(int amount)
        {
            return ApplyExperience(amount, false);
        }

        internal void RestoreStateDeferred(int level, int experience)
        {
            level = Clamp(level, 1, MaximumLevel);
            experience = level >= MaximumLevel ? 0 : Clamp(experience, 0,
                GetRequiredExperienceForNextLevel(level) - 1);
            storage.SetInt(LevelKey, level);
            storage.SetInt(ExperienceKey, experience);
        }

        AccountExperienceResult ApplyExperience(int amount, bool save)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

            int previousLevel = Level;
            int level = previousLevel;
            int experience = Experience;
            long remaining = amount;

            while (remaining > 0 && level < MaximumLevel)
            {
                int required = GetRequiredExperienceForNextLevel(level);
                int needed = required - experience;
                if (remaining < needed)
                {
                    experience += (int)remaining;
                    remaining = 0;
                    break;
                }

                remaining -= needed;
                experience = 0;
                level++;
            }

            if (level >= MaximumLevel) experience = 0;
            int applied = amount - (int)Math.Min(remaining, int.MaxValue);
            storage.SetInt(LevelKey, level);
            storage.SetInt(ExperienceKey, experience);
            if (save) storage.Save();

            var unlocked = new List<AccountFeature>();
            for (int i = 0; i < AllFeatures.Length; i++)
            {
                int requiredLevel = GetRequiredLevel(AllFeatures[i]);
                if (requiredLevel > previousLevel && requiredLevel <= level)
                    unlocked.Add(AllFeatures[i]);
            }

            return new AccountExperienceResult(amount, applied, previousLevel, level,
                experience, unlocked);
        }

        public static AccountExperienceResult AddAccountExperience(int amount) =>
            Default.AddExperience(amount);

        public static int GetRequiredLevel(AccountFeature feature)
        {
            switch (feature)
            {
                case AccountFeature.AutoBattle: return 2;
                case AccountFeature.BattleSpeed: return 4;
                case AccountFeature.GrowthDungeon: return 6;
                case AccountFeature.Sweep: return 8;
                case AccountFeature.Equipment: return 10;
                case AccountFeature.WeeklyBoss: return 15;
                case AccountFeature.ChallengeTower: return 20;
                default: throw new ArgumentOutOfRangeException(nameof(feature));
            }
        }

        static int DefaultRequiredExperience(int level)
        {
            // MVP 임시 곡선: 1→2는 100, 이후 레벨마다 50씩 증가합니다.
            long required = 100L + Math.Max(0, level - 1) * 50L;
            return required >= int.MaxValue ? int.MaxValue : (int)required;
        }

        static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
