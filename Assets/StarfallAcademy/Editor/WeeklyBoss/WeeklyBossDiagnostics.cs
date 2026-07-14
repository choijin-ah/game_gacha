using System;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class WeeklyBossDiagnostics
    {
        [MenuItem("Starfall/Diagnostics/Weekly Boss Diagnostics")]
        static void Run()
        {
            const string stagePath = "Assets/StarfallAcademy/Data/Stages/Stage_10.asset";
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(stagePath);
            if (stage == null) throw new InvalidOperationException("진단용 Stage 10이 없습니다.");

            WeeklyBossDefinition boss = CreateBoss(stage);
            WeeklyBossDatabase database = ScriptableObject.CreateInstance<WeeklyBossDatabase>();
            database.Add(boss);
            try
            {
                WeeklyBossDifficulty difficulty = boss.Difficulties[0];
                var storage = new InMemoryMetaStorage();
                var clock = new ManualUtcClock(
                    new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc));
                var service = new WeeklyBossService(storage, clock);
                Require(service.TryBeginRun(boss, difficulty,
                    out WeeklyBossRunContext first, out string error), error);
                Require(first.Rules.Mode == BattleMode.WeeklyBoss
                    && first.Rules.HasTurnLimit && first.Rules.TurnLimit == 10
                    && first.Rules.AllowTimeoutResult && first.Rules.AllowNonKillResult,
                    "주간 보스 제한 ACTION 규칙이 RunContext에 반영되지 않았습니다.");
                var result = new BattleResult(BattleMode.WeeklyBoss,
                    BattleEndReason.TurnLimit, BattleOutcome.Ongoing, true, 10, 0, 50000);
                int score = WeeklyBossScoreCalculator.Calculate(result, difficulty);
                Require(score == WeeklyBossScoreCalculator.Calculate(result, difficulty),
                    "점수 계산이 결정적이지 않습니다.");
                Require(first.Complete(result).Succeeded, "주간 보스 결과 저장이 실패했습니다.");
                Require(service.GetSnapshot(boss, difficulty).BestScore == score,
                    "최고 점수가 저장되지 않았습니다.");
                Require(service.IsTierClaimed(boss, difficulty, 0),
                    "달성한 점수 보상이 기록되지 않았습니다.");
                Require(service.ClaimEligibleRewards(boss, difficulty,
                    WeeklyBossService.GetWeekId(clock.UtcNow), score) == 0,
                    "같은 주간 보상이 중복 지급되었습니다.");

                Require(service.TryBeginRun(boss, difficulty,
                    out WeeklyBossRunContext second, out error), error);
                second.Complete(new BattleResult(BattleMode.WeeklyBoss,
                    BattleEndReason.TurnLimit, BattleOutcome.Ongoing, true, 10, 4, 1));
                Require(service.GetSnapshot(boss, difficulty).BestScore == score,
                    "낮은 점수가 최고 기록을 덮어썼습니다.");
                Require(!service.TryBeginRun(boss, difficulty, out _, out _),
                    "주간 도전 횟수 제한이 적용되지 않았습니다.");

                string json = service.ExportPlayerDataJson(database);
                WeeklyBossPlayerDataSnapshot captured = service.CapturePlayerData(database);
                Require(captured.entries.Count == 1
                    && captured.entries[0].claimedRewardTierIndexes.Count == 1,
                    "Player Data 캡처에 주간 보스 기록이 누락되었습니다.");
                service.ResetPlayerData(database);
                Require(service.GetSnapshot(boss, difficulty).AttemptsUsed == 0
                    && service.GetSnapshot(boss, difficulty).BestScore == 0,
                    "주간 보스 저장 데이터 초기화가 실패했습니다.");
                Require(service.TryImportPlayerDataJson(database, json, out error), error);
                Require(service.GetSnapshot(boss, difficulty).AttemptsUsed == 2
                    && service.GetSnapshot(boss, difficulty).BestScore == score
                    && service.IsTierClaimed(boss, difficulty, 0),
                    "주간 보스 JSON 복원이 원본과 다릅니다.");

                VerifySaveFailureRollback(boss, difficulty, clock, result);
                VerifyRewardMarkerSaveFailure(boss, difficulty, clock);
                clock.Advance(TimeSpan.FromDays(7));
                Require(service.GetSnapshot(boss, difficulty).AttemptsRemaining == 2,
                    "주간 초기화가 적용되지 않았습니다.");
                Debug.Log("[Starfall] Weekly Boss diagnostics passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(database);
                UnityEngine.Object.DestroyImmediate(boss);
            }
        }

        static WeeklyBossDefinition CreateBoss(StageData stage)
        {
            var boss = ScriptableObject.CreateInstance<WeeklyBossDefinition>();
            var serialized = new SerializedObject(boss);
            serialized.FindProperty("bossId").stringValue = "diagnostic_boss";
            serialized.FindProperty("baseStage").objectReferenceValue = stage;
            SerializedProperty difficulties = serialized.FindProperty("difficultyEntries");
            difficulties.arraySize = 1;
            SerializedProperty entry = difficulties.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("difficultyId").stringValue = "normal";
            entry.FindPropertyRelative("turnLimit").intValue = 10;
            entry.FindPropertyRelative("weeklyAttempts").intValue = 2;
            SerializedProperty rewards = serialized.FindProperty("scoreRewardTiers");
            rewards.arraySize = 1;
            SerializedProperty rewardTier = rewards.GetArrayElementAtIndex(0);
            rewardTier.FindPropertyRelative("requiredScore").intValue = 1;
            rewardTier.FindPropertyRelative("reward").FindPropertyRelative("currencyReward")
                .FindPropertyRelative("credits").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return boss;
        }

        static void VerifySaveFailureRollback(WeeklyBossDefinition boss,
            WeeklyBossDifficulty difficulty, IUtcClock clock, BattleResult result)
        {
            var storage = new FailOnceMetaStorage();
            var service = new WeeklyBossService(storage, clock);
            Require(service.TryBeginRun(boss, difficulty,
                out WeeklyBossRunContext run, out string error), error);
            storage.FailNextSave = true;
            Require(!run.Complete(result).Succeeded, "저장 실패 결과가 성공으로 처리되었습니다.");
            WeeklyBossSnapshot snapshot = service.GetSnapshot(boss, difficulty);
            Require(snapshot.AttemptsUsed == 0 && snapshot.BestScore == 0,
                "저장 실패 후 도전 횟수 또는 점수가 롤백되지 않았습니다.");
        }

        static void VerifyRewardMarkerSaveFailure(WeeklyBossDefinition boss,
            WeeklyBossDifficulty difficulty, IUtcClock clock)
        {
            var storage = new FailOnceMetaStorage();
            var service = new WeeklyBossService(storage, clock);
            var rewardService = new RewardService(storage);
            int creditsBefore = rewardService.Credits;
            string weekId = WeeklyBossService.GetWeekId(clock.UtcNow);

            storage.FailNextSave = true;
            Require(service.ClaimEligibleRewards(boss, difficulty, weekId, int.MaxValue) == 0,
                "보상/claim marker 저장 실패가 성공으로 처리되었습니다.");
            Require(!service.IsTierClaimed(boss, difficulty, 0, weekId)
                && rewardService.Credits == creditsBefore,
                "보상/claim marker 저장 실패 후 주간 보상 상태가 함께 롤백되지 않았습니다.");

            Require(service.ClaimEligibleRewards(boss, difficulty, weekId, int.MaxValue) == 1
                && service.IsTierClaimed(boss, difficulty, 0, weekId)
                && rewardService.Credits == creditsBefore + 1,
                "저장 실패 후 주간 보상 재시도가 정확히 한 번 지급되지 않았습니다.");
            Require(service.ClaimEligibleRewards(boss, difficulty, weekId, int.MaxValue) == 0
                && rewardService.Credits == creditsBefore + 1,
                "주간 보상 재시도 후 중복 지급이 발생했습니다.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        sealed class FailOnceMetaStorage : IMetaStorage
        {
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
