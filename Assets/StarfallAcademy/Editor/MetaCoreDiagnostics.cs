using System;
using System.Collections.Generic;
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
            VerifyClockRollbackProtection();
            VerifyDailyMissions();
            VerifyPlayerPrefsJournal();
            VerifyGachaRarityRules();
            VerifyLiveOpsTransactions();
            VerifyEquipmentEnhancementRollback();
            VerifyEquipmentDropRollback();
            VerifyFormationMutationRollback();
            VerifyGachaHistoryRoundTrip();
            VerifySaveVersionContract();
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

        static void VerifyClockRollbackProtection()
        {
            var staminaStorage = new InMemoryMetaStorage();
            var staminaProfile = new PlayerProfileService(staminaStorage);
            var staminaClock = new ManualUtcClock(
                new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc));
            var stamina = new StaminaService(staminaStorage, staminaProfile, staminaClock);
            Require(stamina.TrySpend(10), "Rollback test could not spend stamina.");
            staminaClock.Advance(StaminaService.RecoveryInterval);
            Require(stamina.Current == 111, "Rollback test did not recover one stamina.");
            staminaClock.UtcNow = new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc);
            Require(stamina.Current == 111,
                "Moving the clock backwards changed stamina or its recovery anchor.");
            staminaClock.UtcNow = new DateTime(2026, 7, 13, 10, 6, 0, DateTimeKind.Utc);
            Require(stamina.Current == 111,
                "Returning to an already-accounted future time recovered stamina twice.");
            staminaClock.Advance(StaminaService.RecoveryInterval);
            Require(stamina.Current == 112,
                "Stamina did not resume after passing the preserved recovery anchor.");

            var missionStorage = new InMemoryMetaStorage();
            var missionProfile = new PlayerProfileService(missionStorage);
            var missionRewards = new RewardService(missionStorage, missionProfile);
            var missionClock = new ManualUtcClock(
                new DateTime(2026, 7, 13, 23, 50, 0, DateTimeKind.Utc));
            var missions = new MissionService(missionStorage, missionClock, missionRewards);
            missions.AddProgress(DailyMissionType.Login);
            Require(missions.ClaimReward(DailyMissionType.Login).Succeeded,
                "Rollback test could not claim the first daily reward.");
            missionClock.Advance(TimeSpan.FromMinutes(11));
            missions.AddProgress(DailyMissionType.Login);
            Require(missions.ClaimReward(DailyMissionType.Login).Succeeded,
                "Rollback test could not claim the next-day reward.");
            int creditsAfterNextDay = missionRewards.Credits;

            missionClock.UtcNow = new DateTime(2026, 7, 13, 23, 50, 0, DateTimeKind.Utc);
            Require(missions.GetProgress(DailyMissionType.Login).Claimed,
                "Clock rollback reset an already accepted mission day.");
            missionClock.UtcNow = new DateTime(2026, 7, 14, 0, 1, 0, DateTimeKind.Utc);
            Require(missions.ClaimReward(DailyMissionType.Login).Status
                    == MissionClaimStatus.AlreadyClaimed
                && missionRewards.Credits == creditsAfterNextDay,
                "Clock rollback/forward granted the same daily reward twice.");
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

        static void VerifyPlayerPrefsJournal()
        {
            Require(PlayerPrefsMetaStorage.VerifyTransactionJournal(out string error),
                "PlayerPrefs journal integration failed: " + error);
        }

        static void VerifyGachaRarityRules()
        {
            Require(!GachaService.ValidateRarityPools(15f, 82f, true, 10,
                    true, true, false, out _),
                "A banner with a missing active 3-star pool was accepted.");
            Require(GachaService.ValidateRarityPools(97f, 0f, true, 10,
                    true, true, false, out _),
                "The explicit 5-star 3% / 4-star 97% configuration was rejected.");

            const int samples = 200000;
            int top = 0, four = 0, three = 0;
            var random = new System.Random(7319);
            for (int i = 0; i < samples; i++)
            {
                int rarity = GachaService.SelectRarity(random.NextDouble() * 100.0,
                    3f, 15f, false);
                if (rarity >= 5) top++;
                else if (rarity == 4) four++;
                else three++;
            }

            Require(Math.Abs(top / (float)samples - .03f) < .003f,
                "The cumulative gacha roll did not preserve the absolute 5-star rate.");
            Require(Math.Abs(four / (float)samples - .15f) < .004f,
                "The cumulative gacha roll did not preserve the absolute 4-star rate.");
            Require(Math.Abs(three / (float)samples - .82f) < .005f,
                "The cumulative gacha roll did not preserve the remaining 3-star rate.");
        }

        static void VerifyLiveOpsTransactions()
        {
            AttendanceCampaignData campaign = CreateAttendanceCampaign();
            MailTemplateData template = CreateMailTemplate();
            try
            {
                var clock = new ManualUtcClock(new DateTime(2026, 7, 14, 0, 0, 0,
                    DateTimeKind.Utc));
                var storage = new InMemoryMetaStorage();
                var profile = new PlayerProfileService(storage);
                var packageRewards = new RewardPackageService(storage, profile);
                var attendance = new AttendanceService(storage, clock, packageRewards);
                int creditsBefore = new RewardService(storage, profile).Credits;
                AttendanceClaimResult attendanceClaim = attendance.Claim(campaign);
                Require(attendanceClaim.Succeeded
                    && new RewardService(storage, profile).Credits == creditsBefore + 1234,
                    "Attendance reward/progress transaction failed.");
                Require(!attendance.Claim(campaign).Succeeded,
                    "Attendance allowed the same UTC-day claim twice.");

                var failureStorage = new FailOnceSaveStorage();
                var failureProfile = new PlayerProfileService(failureStorage);
                var failureRewards = new RewardPackageService(failureStorage, failureProfile);
                var failingAttendance = new AttendanceService(failureStorage, clock, failureRewards);
                int failureCredits = new RewardService(failureStorage, failureProfile).Credits;
                failingAttendance.GetProgress(campaign);
                failureStorage.FailNextSave = true;
                Require(!failingAttendance.Claim(campaign).Succeeded
                    && new RewardService(failureStorage, failureProfile).Credits == failureCredits
                    && failingAttendance.GetProgress(campaign).CurrentSequenceIndex == 0,
                    "Attendance save failure did not roll back reward and progress together.");

                var mailStorage = new InMemoryMetaStorage();
                var mailProfile = new PlayerProfileService(mailStorage);
                var mailRewards = new RewardPackageService(mailStorage, mailProfile);
                var mail = new MailService(mailStorage, clock, mailRewards);
                MailSendResult sent = mail.Send(template);
                Require(sent.Succeeded && sent.Mail != null,
                    "Diagnostic mail could not be sent.");
                int mailCredits = new RewardService(mailStorage, mailProfile).Credits;
                MailClaimResult claimed = mail.Claim(sent.Mail.MailInstanceId);
                Require(sent.Succeeded && claimed.Succeeded
                    && new RewardService(mailStorage, mailProfile).Credits == mailCredits + 4321
                    && !mail.Claim(sent.Mail.MailInstanceId).Succeeded,
                    "Mail claim was not idempotent.");

                var failingMailStorage = new FailOnceSaveStorage();
                var failingMailProfile = new PlayerProfileService(failingMailStorage);
                var failingMailRewards = new RewardPackageService(failingMailStorage,
                    failingMailProfile);
                var failingMail = new MailService(failingMailStorage, clock,
                    failingMailRewards);
                MailSendResult failingSent = failingMail.Send(template);
                Require(failingSent.Succeeded && failingSent.Mail != null,
                    "Failure-path diagnostic mail could not be sent.");
                int failingMailCredits = new RewardService(failingMailStorage,
                    failingMailProfile).Credits;
                failingMailStorage.FailNextSave = true;
                Require(!failingMail.Claim(failingSent.Mail.MailInstanceId).Succeeded
                    && new RewardService(failingMailStorage, failingMailProfile).Credits
                        == failingMailCredits
                    && !failingMail.GetMails()[0].IsClaimed,
                    "Mail save failure did not roll back reward and claim marker together.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(campaign);
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        static void VerifyGachaHistoryRoundTrip()
        {
            string previous = GachaHistoryService.ExportJson(true);
            try
            {
                Require(GachaHistoryService.TryImportJson(
                    "{\"version\":1,\"entries\":[]}", out string importError),
                    "Gacha history import failed: " + importError);
                Require(GachaHistoryService.Load().Count == 0,
                    "Gacha history did not load the imported empty snapshot.");
                string exported = GachaHistoryService.ExportJson(true);
                GachaHistoryService.Clear();
                Require(GachaHistoryService.TryImportJson(exported, out string restoreError)
                    && GachaHistoryService.Load().Count == 0,
                    "Gacha history export/import round trip failed: " + restoreError);
            }
            finally
            {
                GachaHistoryService.TryImportJson(previous, out _);
            }
        }

        static void VerifyEquipmentEnhancementRollback()
        {
            EquipmentDefinition definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            EquipmentDatabase database = ScriptableObject.CreateInstance<EquipmentDatabase>();
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("equipmentId").stringValue = "diagnostic_equipment";
                serialized.FindProperty("displayName").stringValue = "Diagnostic Equipment";
                serialized.FindProperty("maximumLevel").intValue = 5;
                serialized.FindProperty("enhancementBaseCost").intValue = 100;
                serialized.FindProperty("enhancementCostPerLevel").intValue = 10;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                database.Add(definition);

                var storage = new FailOnceSaveStorage();
                var inventory = new EquipmentInventoryService(storage);
                EquipmentInstance item = inventory.Add(definition.Id, 1, "diagnostic-instance");
                int creditsBefore = new RewardService(storage,
                    new PlayerProfileService(storage)).Credits;
                storage.FailNextSave = true;
                var enhancement = new EquipmentEnhancementService(storage);
                Require(!enhancement.TryEnhance(item.instanceId, database, out _)
                    && inventory.GetAll()[0].level == 1
                    && new RewardService(storage, new PlayerProfileService(storage)).Credits
                        == creditsBefore,
                    "Equipment enhancement save failure did not restore inventory and credits.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(database);
            }
        }

        static void VerifyEquipmentDropRollback()
        {
            EquipmentDefinition definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            EquipmentDropTable table = ScriptableObject.CreateInstance<EquipmentDropTable>();
            try
            {
                var definitionData = new SerializedObject(definition);
                definitionData.FindProperty("equipmentId").stringValue =
                    "diagnostic_drop_equipment";
                definitionData.FindProperty("displayName").stringValue =
                    "Diagnostic Drop Equipment";
                definitionData.ApplyModifiedPropertiesWithoutUndo();

                var tableData = new SerializedObject(table);
                tableData.FindProperty("minimumDrops").intValue = 2;
                tableData.FindProperty("maximumDrops").intValue = 2;
                SerializedProperty candidates = tableData.FindProperty("candidates");
                candidates.arraySize = 1;
                SerializedProperty candidate = candidates.GetArrayElementAtIndex(0);
                candidate.FindPropertyRelative("equipment").objectReferenceValue = definition;
                candidate.FindPropertyRelative("weight").floatValue = 1f;
                tableData.ApplyModifiedPropertiesWithoutUndo();

                var storage = new FailOnceSaveStorage();
                var inventory = new EquipmentInventoryService(storage);
                var drops = new EquipmentDropService(inventory);
                storage.FailNextSave = true;
                bool threw = false;
                try { drops.Grant("diagnostic-drop", table, 12345); }
                catch (Exception) { threw = true; }
                Require(threw && inventory.GetAll().Count == 0,
                    "Equipment drop save failure left a partial inventory grant.");

                IReadOnlyList<EquipmentInstance> granted =
                    drops.Grant("diagnostic-drop", table, 12345);
                Require(granted.Count == 2 && inventory.GetAll().Count == 2
                    && granted[0].instanceId != granted[1].instanceId,
                    "Equipment drop retry did not grant the deterministic batch.");
                drops.Grant("diagnostic-drop", table, 12345);
                Require(inventory.GetAll().Count == 2,
                    "Equipment drop transaction retry created duplicate instances.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        static void VerifyFormationMutationRollback()
        {
            const string legacyKey = "StarfallAcademy.Formation";
            var storage = new FailOnceSaveStorage();
            var formations = new FormationPresetService(storage);
            Require(formations.Rename(0, "Baseline"),
                "Formation rollback diagnostic could not create its baseline.");
            storage.SetString(legacyKey, "baseline-legacy");
            storage.Save();
            string baselinePresets = storage.GetString(FormationPresetService.StorageKey,
                string.Empty);

            storage.FailNextSave = true;
            Require(!formations.Rename(0, "Changed")
                && storage.GetString(FormationPresetService.StorageKey, string.Empty)
                    == baselinePresets,
                "Failed formation rename did not restore the preset snapshot.");

            storage.FailNextSave = true;
            Require(!formations.Select(1) && formations.ActivePresetIndex == 0,
                "Failed formation selection changed the active preset.");

            storage.FailNextSave = true;
            Require(!formations.Save(0, Array.Empty<CharacterData>())
                && storage.GetString(FormationPresetService.StorageKey, string.Empty)
                    == baselinePresets
                && storage.GetString(legacyKey, string.Empty) == "baseline-legacy",
                "Failed formation member save did not restore both preset keys.");

            storage.FailNextSave = true;
            Require(!formations.TryImportJson(
                    "{\"version\":1,\"activeIndex\":2,\"presets\":[]}", null, out _)
                && storage.GetString(FormationPresetService.StorageKey, string.Empty)
                    == baselinePresets,
                "Failed formation import replaced the previous presets.");

            storage.FailNextSave = true;
            bool resetThrew = false;
            try { formations.Reset(); }
            catch (Exception) { resetThrew = true; }
            Require(resetThrew
                && storage.GetString(FormationPresetService.StorageKey, string.Empty)
                    == baselinePresets
                && storage.GetString(legacyKey, string.Empty) == "baseline-legacy",
                "Failed formation reset did not restore both preset keys.");

            var migrationStorage = new FailOnceSaveStorage();
            migrationStorage.SetString(legacyKey, "legacy-a|legacy-b");
            migrationStorage.Save();
            migrationStorage.FailNextSave = true;
            bool migrationThrew = false;
            try { new FormationPresetService(migrationStorage).MigrateLegacyIfNeeded(); }
            catch (Exception) { migrationThrew = true; }
            Require(migrationThrew
                && !migrationStorage.HasKey(FormationPresetService.StorageKey)
                && migrationStorage.GetString(legacyKey, string.Empty)
                    == "legacy-a|legacy-b",
                "Failed formation migration did not restore the legacy snapshot.");
        }

        static void VerifySaveVersionContract()
        {
            Require(PlayerSaveVersion.LatestVersion >= 2,
                "Save data version marker was not advanced for expanded content.");
            var storage = new InMemoryMetaStorage();
            storage.SetString("StarfallAcademy.Formation", "diagnostic-a|diagnostic-b");
            var formations = new FormationPresetService(storage);
            Require(formations.MigrateLegacyIfNeeded()
                && storage.HasKey(FormationPresetService.StorageKey),
                "Legacy formation was not migrated into versioned presets.");
            formations.Reset();
            Require(!storage.HasKey(FormationPresetService.StorageKey)
                && !storage.HasKey("StarfallAcademy.Formation"),
                "Formation reset left a legacy key that would remigrate immediately.");
        }

        static AttendanceCampaignData CreateAttendanceCampaign()
        {
            AttendanceCampaignData campaign = ScriptableObject.CreateInstance<AttendanceCampaignData>();
            var serialized = new SerializedObject(campaign);
            serialized.FindProperty("campaignId").stringValue = "diagnostic_attendance";
            serialized.FindProperty("displayName").stringValue = "Diagnostic Attendance";
            SerializedProperty days = serialized.FindProperty("days");
            days.arraySize = 1;
            SerializedProperty day = days.GetArrayElementAtIndex(0);
            day.FindPropertyRelative("dayNumber").intValue = 1;
            day.FindPropertyRelative("reward").FindPropertyRelative("currencyReward")
                .FindPropertyRelative("credits").intValue = 1234;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return campaign;
        }

        static MailTemplateData CreateMailTemplate()
        {
            MailTemplateData template = ScriptableObject.CreateInstance<MailTemplateData>();
            var serialized = new SerializedObject(template);
            serialized.FindProperty("templateId").stringValue = "diagnostic_mail";
            serialized.FindProperty("title").stringValue = "Diagnostic Mail";
            serialized.FindProperty("body").stringValue = "Diagnostic body";
            serialized.FindProperty("defaultExpiryHours").intValue = 24;
            serialized.FindProperty("attachments").FindPropertyRelative("currencyReward")
                .FindPropertyRelative("credits").intValue = 4321;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
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
