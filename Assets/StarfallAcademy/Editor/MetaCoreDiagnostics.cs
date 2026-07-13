using System;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class MetaCoreDiagnostics
    {
        [MenuItem("Starfall/Diagnostics/Meta Core Diagnostics")]
        public static void RunCoreDiagnostics()
        {
            VerifyRewardsAndProfile();
            VerifyStaminaRecovery();
            VerifyDailyMissions();
            Debug.Log("[Starfall Meta] 인메모리 핵심 경계 검증을 통과했습니다.");
        }

        static void VerifyRewardsAndProfile()
        {
            var storage = new InMemoryMetaStorage();
            var profile = new PlayerProfileService(storage);
            var rewards = new RewardService(storage, profile);
            int creditsBefore = rewards.Credits;
            int materialsBefore = rewards.SkillMaterials;
            int premiumBefore = rewards.PremiumCurrency;
            int savesBefore = storage.SaveCount;
            int levelUpExperience = profile.GetRequiredExperienceForNextLevel(profile.Level);
            var bundle = new RewardBundle(100, 3, levelUpExperience, 7);

            RewardGrantResult first = rewards.GrantReward("diagnostic-reward", bundle);
            Require(rewards.PremiumCurrency == premiumBefore + 7,
                "Premium currency grant amount did not match.");
            Require(first.Succeeded, "첫 보상 지급이 실패했습니다.");
            Require(storage.SaveCount == savesBefore + 1,
                "Reward currencies, profile experience and transaction were not committed together.");
            Require(rewards.Credits == creditsBefore + 100, "크레딧 지급량이 맞지 않습니다.");
            Require(rewards.SkillMaterials == materialsBefore + 3, "스킬 재료 지급량이 맞지 않습니다.");
            Require(profile.Level == 2 && profile.Experience == 0, "계정 레벨업 처리가 맞지 않습니다.");
            Require(profile.IsUnlocked(AccountFeature.AutoBattle), "2레벨 자동 전투 해금이 누락되었습니다.");

            RewardGrantResult duplicate = rewards.GrantReward("diagnostic-reward", bundle);
            Require(rewards.PremiumCurrency == premiumBefore + 7,
                "Duplicate transaction changed premium currency.");
            Require(duplicate.AlreadyProcessed, "동일 transactionId가 중복으로 처리되었습니다.");
            Require(storage.SaveCount == savesBefore + 1,
                "Duplicate transaction unexpectedly performed another save.");
            Require(rewards.Credits == creditsBefore + 100 && rewards.SkillMaterials == materialsBefore + 3,
                "중복 지급 차단 후 재화가 변경되었습니다.");
            Require(!rewards.GrantReward("", bundle).Succeeded, "빈 transactionId가 허용되었습니다.");
            Require(!rewards.GrantReward("negative", new RewardBundle(-1, 0, 0)).Succeeded,
                "음수 보상이 허용되었습니다.");

            var failureStorage = new FailOnceSaveStorage();
            var failureProfile = new PlayerProfileService(failureStorage);
            var failureRewards = new RewardService(failureStorage, failureProfile);
            int failureCreditsBefore = failureRewards.Credits;
            failureStorage.FailNextSave = true;
            RewardGrantResult failed = failureRewards.GrantReward("diagnostic-failure",
                new RewardBundle(50, 2,
                    failureProfile.GetRequiredExperienceForNextLevel(failureProfile.Level), 5));
            Require(failed.Status == RewardGrantStatus.CommitFailed,
                "Injected reward save failure was not reported.");
            Require(failureRewards.Credits == failureCreditsBefore
                && failureRewards.SkillMaterials == PlayerWallet.DefaultSkillMaterials
                && failureRewards.PremiumCurrency == PlayerWallet.DefaultPremiumCurrency
                && failureProfile.Level == 1 && failureProfile.Experience == 0,
                "Failed reward transaction did not roll back staged values.");
        }

        static void VerifyStaminaRecovery()
        {
            var storage = new InMemoryMetaStorage();
            var profile = new PlayerProfileService(storage);
            var clock = new ManualUtcClock(new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));
            var stamina = new StaminaService(storage, profile, clock);

            StaminaSnapshot initial = stamina.GetSnapshot();
            Require(initial.Current == 120 && initial.Maximum == 120, "초기 행동력 또는 최대치가 맞지 않습니다.");
            Require(stamina.TrySpend(3) && stamina.Current == 117, "행동력 3 소비가 실패했습니다.");
            Require(!stamina.TrySpend(118), "보유량을 넘는 행동력 소비가 허용되었습니다.");

            clock.Advance(TimeSpan.FromMinutes(17));
            StaminaSnapshot afterSeventeenMinutes = stamina.GetSnapshot();
            Require(afterSeventeenMinutes.Current == 119, "6분당 1 자연 회복이 맞지 않습니다.");
            Require(afterSeventeenMinutes.TimeUntilNextRecovery == TimeSpan.FromMinutes(1),
                "남은 자연 회복 시간이 맞지 않습니다.");
            clock.Advance(TimeSpan.FromMinutes(1));
            Require(stamina.Current == 120, "18분 경과 후 행동력 3 회복이 완료되지 않았습니다.");

            Require(stamina.TrySpend(5), "충전 검증용 행동력 소비가 실패했습니다.");
            Require(stamina.Charge(10) == 5 && stamina.Current == stamina.Maximum,
                "충전이 최대치를 초과하거나 실제 충전량이 잘못되었습니다.");

            Require(stamina.Charge(60, true) == 60 && stamina.Current == 180,
                "Paid stamina over-cap charge did not grant the advertised amount.");
            Require(stamina.TrySpend(60) && stamina.Current == stamina.Maximum,
                "Over-cap stamina spend did not return to the natural maximum.");

            var staminaEconomy = new RewardService(storage, profile);
            int premiumBeforePurchase = staminaEconomy.PremiumCurrency;
            int savesBeforePurchase = storage.SaveCount;
            Require(stamina.TryPurchasePremiumCharge(50, 60, true, out int purchased)
                && purchased == 60 && stamina.Current == 180,
                "Premium stamina purchase did not grant the advertised amount.");
            Require(staminaEconomy.PremiumCurrency == premiumBeforePurchase - 50,
                "Premium stamina purchase did not charge the expected currency.");
            Require(storage.SaveCount == savesBeforePurchase + 1,
                "Premium currency and stamina were not committed in one save.");
            Require(stamina.TrySpend(60) && stamina.Current == stamina.Maximum,
                "Purchased over-cap stamina could not be consumed.");

            profile.AddExperience(profile.GetRequiredExperienceForNextLevel(profile.Level));
            StaminaSnapshot levelTwo = stamina.GetSnapshot();
            Require(levelTwo.Maximum == 122 && levelTwo.Current == 120,
                "계정 레벨 기반 행동력 최대치 증가가 맞지 않습니다.");
        }

        static void VerifyDailyMissions()
        {
            var storage = new InMemoryMetaStorage();
            var profile = new PlayerProfileService(storage);
            var rewards = new RewardService(storage, profile);
            var clock = new ManualUtcClock(new DateTime(2026, 7, 13, 23, 50, 0, DateTimeKind.Utc));
            var missions = new MissionService(storage, clock, rewards);

            missions.AddProgress(DailyMissionType.Login);
            missions.AddProgress(DailyMissionType.StaminaSpent, 30);
            missions.AddProgress(DailyMissionType.Enhancement);
            missions.AddProgress(DailyMissionType.BattleCompleted, 3);
            Require(missions.HasClaimableReward(), "완료된 일일 임무를 수령 가능으로 감지하지 못했습니다.");

            int creditsBefore = rewards.Credits;
            MissionClaimResult claimed = missions.ClaimReward(DailyMissionType.Login);
            Require(claimed.Succeeded && claimed.Progress.Claimed, "로그인 일일 보상 수령이 실패했습니다.");
            Require(rewards.Credits == creditsBefore + 5000, "로그인 일일 보상 수치가 맞지 않습니다.");
            Require(missions.ClaimReward(DailyMissionType.Login).Status == MissionClaimStatus.AlreadyClaimed,
                "같은 날 일일 보상을 두 번 수령했습니다.");

            clock.Advance(TimeSpan.FromMinutes(11));
            DailyMissionProgress reset = missions.GetProgress(DailyMissionType.Login);
            Require(reset.Current == 0 && !reset.Claimed && !reset.CanClaim,
                "UTC 날짜 변경 시 일일 임무가 초기화되지 않았습니다.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Starfall Meta Diagnostics] " + message);
        }

        sealed class FailOnceSaveStorage : IMetaStorage
        {
            // Exercises the reward rollback path without touching the player's PlayerPrefs.
            readonly InMemoryMetaStorage inner = new InMemoryMetaStorage();

            public bool FailNextSave { get; set; }
            public bool HasKey(string key) => inner.HasKey(key);
            public int GetInt(string key, int defaultValue = 0) => inner.GetInt(key, defaultValue);
            public string GetString(string key, string defaultValue = "") =>
                inner.GetString(key, defaultValue);
            public void SetInt(string key, int value) => inner.SetInt(key, value);
            public void SetString(string key, string value) => inner.SetString(key, value);
            public void DeleteKey(string key) => inner.DeleteKey(key);

            public void Save()
            {
                if (FailNextSave)
                {
                    FailNextSave = false;
                    throw new InvalidOperationException("Injected save failure.");
                }
                inner.Save();
            }
        }
    }
}
