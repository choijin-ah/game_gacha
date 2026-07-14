using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    internal static class WeeklyBossValidation
    {
        internal const string ProviderId = "Weekly Boss";

        static WeeklyBossValidation() => ContentValidationRegistry.Register(ProviderId, Collect);

        [MenuItem("Starfall/Validate/Weekly Boss Content")]
        static void Open() => ContentValidationWindow.Open(ProviderId);

        internal static IEnumerable<ContentValidationIssue> Collect()
        {
            var issues = new List<ContentValidationIssue>();
            string[] guids = AssetDatabase.FindAssets("t:WeeklyBossDefinition");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var definitions = new List<WeeklyBossDefinition>();
            for (int g = 0; g < guids.Length; g++)
            {
                WeeklyBossDefinition boss = AssetDatabase.LoadAssetAtPath<WeeklyBossDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[g]));
                if (boss == null) continue;
                definitions.Add(boss);
                string location = boss.Id;
                if (!ids.Add(boss.Id)) Error(issues, location, "bossId가 중복되었습니다.", boss);
                if (!IsStorageSafeId(boss.Id))
                    Error(issues, location, "bossId에는 문자, 숫자, 밑줄, 하이픈만 사용할 수 있습니다.", boss);
                if (boss.BaseStage == null) Error(issues, location, "기반 스테이지가 없습니다.", boss);
                else if (!boss.BaseStage.BossStage)
                    Warning(issues, location, "기반 스테이지가 보스 스테이지로 표시되지 않았습니다.", boss);
                if (boss.Availability != null && !boss.Availability.IsValid)
                    Error(issues, location, "활성 기간이 올바르지 않습니다.", boss);
                if (boss.Difficulties.Count == 0) Error(issues, location, "난이도가 없습니다.", boss);
                var difficultyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < boss.Difficulties.Count; i++)
                {
                    WeeklyBossDifficulty difficulty = boss.Difficulties[i];
                    if (difficulty == null) { Error(issues, location, "빈 난이도 항목이 있습니다.", boss); continue; }
                    if (!difficultyIds.Add(difficulty.Id))
                        Error(issues, location + "/" + difficulty.Id, "난이도 ID가 중복되었습니다.", boss);
                    if (!IsStorageSafeId(difficulty.Id))
                        Error(issues, location + "/" + difficulty.Id,
                            "난이도 ID에는 문자, 숫자, 밑줄, 하이픈만 사용할 수 있습니다.", boss);
                    if (difficulty.TurnLimit <= 0)
                        Error(issues, location + "/" + difficulty.Id, "제한 ACTION은 1 이상이어야 합니다.", boss);
                    if (difficulty.WeeklyAttempts <= 0)
                        Error(issues, location + "/" + difficulty.Id, "주간 도전 횟수는 1 이상이어야 합니다.", boss);
                }
                int previousScore = -1;
                for (int i = 0; i < boss.RewardTiers.Count; i++)
                {
                    WeeklyBossScoreRewardTier tier = boss.RewardTiers[i];
                    if (tier == null) { Error(issues, location, "빈 점수 보상 단계가 있습니다.", boss); continue; }
                    if (tier.RequiredScore < previousScore)
                        Error(issues, location + "/Reward " + (i + 1), "점수 기준이 오름차순이 아닙니다.", boss);
                    if (tier.Reward == null || tier.Reward.IsEmpty)
                        Warning(issues, location + "/Reward " + (i + 1), "보상이 비어 있습니다.", boss);
                    else if (!tier.Reward.IsValid)
                        Error(issues, location + "/Reward " + (i + 1), "보상이 올바르지 않습니다.", boss);
                    previousScore = tier.RequiredScore;
                }
            }
            for (int i = 0; i < definitions.Count; i++)
                for (int j = i + 1; j < definitions.Count; j++)
                    if (definitions[i].Availability != null && definitions[j].Availability != null
                        && definitions[i].Availability.Overlaps(definitions[j].Availability))
                        Warning(issues, definitions[i].Id + " / " + definitions[j].Id,
                            "주간 보스 로테이션 기간이 겹칩니다.", definitions[i]);
            return issues;
        }

        static bool IsStorageSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            }
            return true;
        }

        static void Error(ICollection<ContentValidationIssue> issues, string location,
            string message, UnityEngine.Object context) => issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error, ProviderId, location, message, context));

        static void Warning(ICollection<ContentValidationIssue> issues, string location,
            string message, UnityEngine.Object context) => issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Warning, ProviderId, location, message, context));
    }

}
