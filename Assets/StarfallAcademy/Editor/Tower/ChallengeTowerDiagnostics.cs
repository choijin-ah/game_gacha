using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class ChallengeTowerDiagnostics
    {
        [MenuItem("Starfall/Diagnostics/Challenge Tower Diagnostics")]
        static void Run()
        {
            StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(
                "Assets/StarfallAcademy/Data/Stages/Stage_01.asset");
            if (stage == null) throw new InvalidOperationException("진단용 Stage 1이 없습니다.");
            TowerFloorData first = CreateFloor(1, stage);
            TowerFloorData second = CreateFloor(2, stage);
            TowerDatabase database = CreateDatabase(first, second);
            try
            {
                var storage = new InMemoryMetaStorage();
                var service = new TowerProgressService(storage);
                Require(service.IsUnlocked(first, database), "1층이 잠겨 있습니다.");
                Require(!service.IsUnlocked(second, database), "선행 층 없이 2층이 열렸습니다.");
                Require(service.TryBeginRun(first, database,
                    out TowerRunContext run, out string error), error);
                Require(run.Rules.Mode == BattleMode.ChallengeTower
                    && run.Rules.RuntimeModifier != null,
                    "탑 전투 규칙 또는 Modifier가 RunContext에 연결되지 않았습니다.");
                var clear = new BattleResult(BattleMode.ChallengeTower,
                    BattleEndReason.EnemiesDefeated, BattleOutcome.Victory, true, 10, 0, 1000);
                BattleModeCompletion completion = run.Complete(clear);
                Require(completion.Succeeded, "1층 결과 저장이 실패했습니다.");
                Require(service.IsUnlocked(second, database),
                    "1층 클리어 후 2층이 열리지 않았습니다.");
                int best = service.GetSnapshot(first).Stars;
                Require(best == 3, "별 조건이 전투 결과에 반영되지 않았습니다.");
                Require(service.ClaimCumulativeRewards(database) == 0,
                    "누적 별 보상이 중복 지급되었습니다.");

                Require(service.TryBeginRun(first, database,
                    out TowerRunContext retry, out error), error);
                retry.Complete(new BattleResult(BattleMode.ChallengeTower,
                    BattleEndReason.EnemiesDefeated, BattleOutcome.Victory, true, 999, 4, 1));
                Require(service.GetSnapshot(first).Stars == best,
                    "낮은 별 기록이 최고 기록을 덮어썼습니다.");

                string json = service.ExportPlayerDataJson(database);
                TowerPlayerDataSnapshot captured = service.CapturePlayerData(database);
                Require(captured.totalStars == 3 && captured.floors.Count == 2
                    && captured.claimedCumulativeRewardTierIndexes.Count == 1,
                    "Player Data 캡처에 탑 기록이 누락되었습니다.");
                service.ResetPlayerData(database);
                Require(!service.GetSnapshot(first).Cleared
                    && !service.IsUnlocked(second, database), "탑 저장 데이터 초기화가 실패했습니다.");
                Require(service.TryImportPlayerDataJson(database, json, out error), error);
                Require(service.GetSnapshot(first).Cleared
                    && service.GetSnapshot(first).Stars == 3
                    && service.IsUnlocked(second, database), "탑 JSON 복원이 원본과 다릅니다.");

                VerifyModifier();
                VerifySaveFailureRollback(database, first, clear);
                VerifyRewardMarkerSaveFailure(database, first);
                Debug.Log("[Starfall] Challenge Tower diagnostics passed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(database);
            }
        }

        static TowerDatabase CreateDatabase(TowerFloorData first, TowerFloorData second)
        {
            TowerDatabase database = ScriptableObject.CreateInstance<TowerDatabase>();
            database.Add(first);
            database.Add(second);
            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty rewards = serialized.FindProperty("cumulativeStarRewards");
            rewards.arraySize = 1;
            SerializedProperty rewardTier = rewards.GetArrayElementAtIndex(0);
            rewardTier.FindPropertyRelative("requiredTotalStars").intValue = 3;
            rewardTier.FindPropertyRelative("reward").FindPropertyRelative("currencyReward")
                .FindPropertyRelative("credits").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return database;
        }

        static TowerFloorData CreateFloor(int number, StageData stage)
        {
            TowerFloorData floor = ScriptableObject.CreateInstance<TowerFloorData>();
            SerializedObject so = new SerializedObject(floor);
            so.FindProperty("floorNumber").intValue = number;
            so.FindProperty("baseStage").objectReferenceValue = stage;
            so.FindProperty("firstClearReward").FindPropertyRelative("currencyReward")
                .FindPropertyRelative("credits").intValue = 1;
            SerializedProperty stars = so.FindProperty("starConditions");
            stars.arraySize = 3;
            stars.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.Clear;
            stars.GetArrayElementAtIndex(1).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.TurnLimit;
            stars.GetArrayElementAtIndex(1).FindPropertyRelative("threshold").intValue = 20;
            stars.GetArrayElementAtIndex(2).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.MaximumDefeatedAllies;
            stars.GetArrayElementAtIndex(2).FindPropertyRelative("threshold").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return floor;
        }

        static void VerifyModifier()
        {
            var modifier = new TowerModifierService(new[]
            {
                JsonUtility.FromJson<TowerModifierDefinition>(
                    "{\"modifierId\":\"hp\",\"type\":0,\"value\":0.5}")
            });
            var stats = new BattleBaseStats(100, 10, 0, 10);
            modifier.ModifyStats(BattleTeam.Enemy, stats);
            Require(Math.Abs(stats.MaxHp - 150f) < .01f,
                "Modifier가 전투 스탯에 반영되지 않았습니다.");
        }

        static void VerifySaveFailureRollback(TowerDatabase database, TowerFloorData floor,
            BattleResult clear)
        {
            var storage = new FailOnceMetaStorage();
            var service = new TowerProgressService(storage);
            var rewardService = new RewardService(storage);
            int creditsBefore = rewardService.Credits;
            Require(service.TryBeginRun(floor, database,
                out TowerRunContext run, out string error), error);
            storage.FailNextSave = true;
            Require(!run.Complete(clear).Succeeded, "저장 실패 결과가 성공으로 처리되었습니다.");
            Require(!service.GetSnapshot(floor).Cleared && service.GetSnapshot(floor).Stars == 0,
                "저장 실패 후 탑 진행도가 롤백되지 않았습니다.");
            Require(rewardService.Credits == creditsBefore,
                "최초 클리어 저장 실패 후 보상이 롤백되지 않았습니다.");
        }

        static void VerifyRewardMarkerSaveFailure(TowerDatabase database, TowerFloorData floor)
        {
            var storage = new FailOnceMetaStorage();
            var service = new TowerProgressService(storage);
            var rewardService = new RewardService(storage);
            service.RestorePlayerData(database, new TowerPlayerDataSnapshot
            {
                totalStars = 3,
                floors = new List<TowerPlayerDataEntry>
                {
                    new TowerPlayerDataEntry
                    {
                        floorNumber = floor.FloorNumber,
                        cleared = true,
                        stars = 3
                    }
                }
            });
            int creditsBefore = rewardService.Credits;

            storage.FailNextSave = true;
            Require(service.ClaimCumulativeRewards(database) == 0,
                "누적 보상/claim marker 저장 실패가 성공으로 처리되었습니다.");
            Require(service.CapturePlayerData(database).claimedCumulativeRewardTierIndexes.Count == 0
                && rewardService.Credits == creditsBefore,
                "누적 보상/claim marker 저장 실패 후 상태가 함께 롤백되지 않았습니다.");

            Require(service.ClaimCumulativeRewards(database) == 1
                && service.CapturePlayerData(database).claimedCumulativeRewardTierIndexes.Count == 1
                && rewardService.Credits == creditsBefore + 1,
                "저장 실패 후 누적 보상 재시도가 정확히 한 번 지급되지 않았습니다.");
            Require(service.ClaimCumulativeRewards(database) == 0
                && rewardService.Credits == creditsBefore + 1,
                "누적 보상 재시도 후 중복 지급이 발생했습니다.");
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
