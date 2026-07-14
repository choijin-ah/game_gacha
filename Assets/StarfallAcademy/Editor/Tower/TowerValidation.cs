using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    internal static class TowerValidation
    {
        internal const string ProviderId = "Challenge Tower";

        static TowerValidation() => ContentValidationRegistry.Register(ProviderId, Collect);

        [MenuItem("Starfall/Validate/Challenge Tower Content")]
        static void Open() => ContentValidationWindow.Open(ProviderId);

        internal static IEnumerable<ContentValidationIssue> Collect()
        {
            var issues = new List<ContentValidationIssue>();
            TowerDatabase database = AssetDatabase.LoadAssetAtPath<TowerDatabase>(
                TowerDatabaseBootstrap.DatabasePath);
            if (database == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    ProviderId, "Database", "TowerDatabase 에셋이 없습니다."));
                return issues;
            }
            var numbers = new HashSet<int>();
            int previousPower = -1;
            int highest = 0;
            for (int i = 0; i < database.Floors.Count; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null)
                {
                    Error(issues, "Index " + i, "빈 층 참조가 있습니다.", database);
                    continue;
                }
                string location = "Floor " + floor.FloorNumber;
                if (!numbers.Add(floor.FloorNumber)) Error(issues, location, "층 번호가 중복되었습니다.", floor);
                highest = Math.Max(highest, floor.FloorNumber);
                if (floor.BaseStage == null) Error(issues, location, "기반 스테이지가 없습니다.", floor);
                if (previousPower > floor.RecommendedPower)
                    Warning(issues, location, "권장 전투력이 이전 등록 층보다 낮습니다.", floor);
                previousPower = floor.RecommendedPower;
                var modifierIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var modifierTypes = new Dictionary<TowerModifierType, float>();
                for (int m = 0; m < floor.Modifiers.Count; m++)
                {
                    TowerModifierDefinition modifier = floor.Modifiers[m];
                    if (modifier == null) { Error(issues, location, "빈 Modifier가 있습니다.", floor); continue; }
                    if (!modifierIds.Add(modifier.Id))
                        Error(issues, location + "/" + modifier.Id, "Modifier ID가 중복되었습니다.", floor);
                    if (modifierTypes.TryGetValue(modifier.Type, out float existing)
                        && Math.Sign(existing) != Math.Sign(modifier.Value))
                        Error(issues, location, "서로 상충하는 Modifier가 있습니다.", floor);
                    modifierTypes[modifier.Type] = modifier.Value;
                }
                var stars = new HashSet<TowerStarConditionType>();
                for (int s = 0; s < floor.StarConditions.Count; s++)
                {
                    TowerStarCondition condition = floor.StarConditions[s];
                    if (condition == null) { Error(issues, location, "빈 별 조건이 있습니다.", floor); continue; }
                    if (!stars.Add(condition.Type))
                        Error(issues, location, "별 조건 종류가 중복되었습니다.", floor);
                }
                if (floor.FirstClearReward == null || floor.FirstClearReward.IsEmpty)
                    Warning(issues, location, "최초 클리어 보상이 비어 있습니다.", floor);
                else if (!floor.FirstClearReward.IsValid)
                    Error(issues, location, "최초 클리어 보상이 올바르지 않습니다.", floor);
            }
            if (!numbers.Contains(1)) Error(issues, "Floor 1", "탑은 1층부터 시작해야 합니다.", database);
            for (int floor = 1; floor <= highest; floor++)
                if (!numbers.Contains(floor)) Error(issues, "Floor " + floor, "층 번호가 누락되었습니다.", database);
            int previousStars = 0;
            for (int i = 0; i < database.CumulativeStarRewards.Count; i++)
            {
                TowerStarRewardTier tier = database.CumulativeStarRewards[i];
                if (tier == null) { Error(issues, "Star Reward " + i, "빈 누적 별 보상입니다.", database); continue; }
                if (tier.RequiredTotalStars <= previousStars)
                    Error(issues, "Star Reward " + i, "누적 별 보상 기준이 오름차순이 아닙니다.", database);
                if (tier.Reward == null || tier.Reward.IsEmpty)
                    Warning(issues, "Star Reward " + i, "누적 별 보상이 비어 있습니다.", database);
                else if (!tier.Reward.IsValid)
                    Error(issues, "Star Reward " + i, "누적 별 보상이 올바르지 않습니다.", database);
                previousStars = tier.RequiredTotalStars;
            }
            return issues;
        }

        static void Error(ICollection<ContentValidationIssue> issues, string location,
            string message, UnityEngine.Object context) => issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error, ProviderId, location, message, context));

        static void Warning(ICollection<ContentValidationIssue> issues, string location,
            string message, UnityEngine.Object context) => issues.Add(new ContentValidationIssue(
                ContentValidationSeverity.Warning, ProviderId, location, message, context));
    }

}
