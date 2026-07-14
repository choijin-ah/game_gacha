using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class LiveOpsValidators
    {
        internal const string AttendanceProviderId = "LiveOps.Attendance";
        internal const string MailProviderId = "LiveOps.Mail";

        [InitializeOnLoadMethod]
        static void Register()
        {
            ContentValidationRegistry.Register(AttendanceProviderId, ValidateAttendance);
            ContentValidationRegistry.Register(MailProviderId, ValidateMail);
        }

        [MenuItem("Starfall/Validate/LiveOps Schedule")]
        static void OpenAttendanceValidation() =>
            ContentValidationWindow.Open(AttendanceProviderId);

        [MenuItem("Starfall/Validate/Mail Templates")]
        static void OpenMailValidation() =>
            ContentValidationWindow.Open(MailProviderId);

        internal static IEnumerable<ContentValidationIssue> ValidateAttendance()
        {
            var issues = new List<ContentValidationIssue>();
            AttendanceCampaignDatabase database = AttendanceCampaignDatabaseBootstrap.LoadExisting();
            if (database == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning,
                    "Attendance", "Database", "AttendanceCampaignDatabase 에셋이 없습니다."));
                return issues;
            }

            var ids = new Dictionary<string, AttendanceCampaignData>(StringComparer.Ordinal);
            for (int i = 0; i < database.Campaigns.Count; i++)
            {
                AttendanceCampaignData campaign = database.Campaigns[i];
                string location = "Entry " + (i + 1);
                if (campaign == null)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                        "Attendance", location, "캠페인 참조가 비어 있습니다.", database));
                    continue;
                }
                location = campaign.CampaignId;
                if (string.IsNullOrWhiteSpace(campaign.CampaignId))
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "campaignId가 비어 있습니다.", campaign));
                else if (ids.TryGetValue(campaign.CampaignId, out AttendanceCampaignData first))
                {
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "campaignId가 중복되었습니다. 첫 에셋: " + first.name, campaign));
                }
                else ids.Add(campaign.CampaignId, campaign);

                if (campaign.Schedule == null || !campaign.Schedule.IsValid)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "시작·종료 UTC 기간이 올바르지 않습니다.", campaign));
                if (campaign.DayCount == 0)
                    issues.Add(Issue(ContentValidationSeverity.Error, location,
                        "출석 일차가 0개입니다.", campaign));

                var dayNumbers = new HashSet<int>();
                int previousDay = 0;
                for (int dayIndex = 0; dayIndex < campaign.Days.Count; dayIndex++)
                {
                    AttendanceDayDefinition day = campaign.Days[dayIndex];
                    string dayLocation = location + " / Day " + (dayIndex + 1);
                    if (day == null)
                    {
                        issues.Add(Issue(ContentValidationSeverity.Error, dayLocation,
                            "일차 데이터가 비어 있습니다.", campaign));
                        continue;
                    }
                    if (!dayNumbers.Add(day.DayNumber))
                        issues.Add(Issue(ContentValidationSeverity.Error, dayLocation,
                            "같은 dayNumber가 중복되었습니다.", campaign));
                    if (day.DayNumber <= previousDay)
                        issues.Add(Issue(ContentValidationSeverity.Warning, dayLocation,
                            "일차 번호가 오름차순이 아닙니다.", campaign));
                    previousDay = day.DayNumber;
                    if (day.Reward == null || day.Reward.IsEmpty)
                        issues.Add(Issue(ContentValidationSeverity.Error, dayLocation,
                            "보상이 비어 있습니다.", campaign));
                    else if (!day.Reward.IsValid)
                        issues.Add(Issue(ContentValidationSeverity.Error, dayLocation,
                            "보상 수량, 빈 아이템 또는 중복 아이템을 확인하세요.", campaign));
                }
            }

            for (int i = 0; i < database.Campaigns.Count; i++)
            for (int j = i + 1; j < database.Campaigns.Count; j++)
            {
                AttendanceCampaignData left = database.Campaigns[i];
                AttendanceCampaignData right = database.Campaigns[j];
                if (left == null || right == null || left.Schedule == null || right.Schedule == null
                    || !left.Schedule.Overlaps(right.Schedule)) continue;
                issues.Add(Issue(ContentValidationSeverity.Warning, left.CampaignId,
                    "출석 기간이 '" + right.CampaignId + "' 캠페인과 겹칩니다.", left));
            }
            return issues;
        }

        internal static IEnumerable<ContentValidationIssue> ValidateMail()
        {
            var issues = new List<ContentValidationIssue>();
            MailTemplateDatabase database = MailTemplateDatabaseBootstrap.LoadExisting();
            if (database == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning,
                    "Mail", "Database", "MailTemplateDatabase 에셋이 없습니다."));
                return issues;
            }

            var ids = new Dictionary<string, MailTemplateData>(StringComparer.Ordinal);
            for (int i = 0; i < database.Templates.Count; i++)
            {
                MailTemplateData template = database.Templates[i];
                string location = "Entry " + (i + 1);
                if (template == null)
                {
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                        "Mail", location, "템플릿 참조가 비어 있습니다.", database));
                    continue;
                }
                location = template.TemplateId;
                if (string.IsNullOrWhiteSpace(template.TemplateId))
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "templateId가 비어 있습니다.", template));
                else if (ids.TryGetValue(template.TemplateId, out MailTemplateData first))
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "templateId가 중복되었습니다. 첫 에셋: " + first.name, template));
                else ids.Add(template.TemplateId, template);

                if (string.IsNullOrWhiteSpace(template.Title))
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "제목이 비어 있습니다.", template));
                if (string.IsNullOrWhiteSpace(template.Body))
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "본문이 비어 있습니다.", template));
                SerializedObject serialized = new SerializedObject(template);
                if (serialized.FindProperty("defaultExpiryHours").intValue < 0)
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "기본 만료 시간은 음수일 수 없습니다.", template));
                if (template.Attachments == null || !template.Attachments.IsValid)
                    issues.Add(MailIssue(ContentValidationSeverity.Error, location,
                        "첨부 아이템과 보상 수량을 확인하세요.", template));
            }
            return issues;
        }

        static ContentValidationIssue Issue(ContentValidationSeverity severity,
            string location, string message, UnityEngine.Object context) =>
            new ContentValidationIssue(severity, "Attendance", location, message, context);

        static ContentValidationIssue MailIssue(ContentValidationSeverity severity,
            string location, string message, UnityEngine.Object context) =>
            new ContentValidationIssue(severity, "Mail", location, message, context);
    }
}
