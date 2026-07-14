using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class AttendanceCalendarWindow : EditorWindow
    {
        AttendanceCampaignDatabase database;
        AttendanceCampaignData selected;
        Vector2 listScroll;
        Vector2 detailScroll;
        string search = string.Empty;
        string previewUtcText;

        [MenuItem("Starfall/LiveOps/Attendance Calendar")]
        public static void Open()
        {
            var window = GetWindow<AttendanceCalendarWindow>("Attendance Calendar");
            window.minSize = new Vector2(980, 650);
            window.Show();
        }

        void OnEnable()
        {
            database = AttendanceCampaignDatabaseBootstrap.LoadOrCreate();
            previewUtcText = ContentTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            if (selected == null && database != null && database.Campaigns.Count > 0)
                selected = database.Campaigns[0];
        }

        void OnGUI()
        {
            DrawHeader();
            if (database == null)
            {
                EditorGUILayout.HelpBox("AttendanceCampaignDatabase를 만들 수 없습니다.",
                    MessageType.Error);
                if (GUILayout.Button("다시 만들기"))
                    database = AttendanceCampaignDatabaseBootstrap.LoadOrCreate();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawCampaignList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL ATTENDANCE CALENDAR", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("순환형·기간형 출석 캠페인과 일차별 보상을 편집합니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ 새 캠페인", EditorStyles.toolbarButton, GUILayout.Width(105)))
                CreateCampaign();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    DuplicateCampaign();
                if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    DeleteCampaign();
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(-1);
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(1);
            }
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(52))) SaveAssets();
            if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(52)))
                ContentValidationWindow.Open(LiveOpsValidators.AttendanceProviderId);
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search ?? string.Empty,
                GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(220));
            EditorGUILayout.EndHorizontal();
        }

        void DrawCampaignList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            EditorGUILayout.LabelField("캠페인  " + database.Campaigns.Count,
                EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            IEnumerable<AttendanceCampaignData> filtered = database.Campaigns
                .Where(campaign => campaign != null && MatchesSearch(campaign));
            bool any = false;
            foreach (AttendanceCampaignData campaign in filtered)
            {
                any = true;
                Color previous = GUI.backgroundColor;
                if (campaign == selected) GUI.backgroundColor = new Color(.54f, .78f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = previous;
                EditorGUILayout.LabelField(campaign.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(campaign.CampaignId + "  ·  " + campaign.CycleMode,
                    EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(campaign.DayCount + "일  ·  "
                    + StateLabel(campaign.Schedule.GetState(ContentTime.UtcNow)),
                    EditorStyles.miniLabel);
                if (GUILayout.Button("편집", GUILayout.Width(48))) selected = campaign;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (!any)
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(search)
                    ? "등록된 출석 캠페인이 없습니다." : "검색 결과가 없습니다.",
                    MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 캠페인을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("캠페인 상세", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Project에서 찾기", GUILayout.Width(120))) Ping(selected);
            EditorGUILayout.EndHorizontal();

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUI.skin.box);
            SerializedObject serialized = new SerializedObject(selected);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty("campaignId"),
                new GUIContent("캠페인 ID"));
            EditorGUILayout.PropertyField(serialized.FindProperty("displayName"),
                new GUIContent("표시 이름"));
            EditorGUILayout.PropertyField(serialized.FindProperty("cycleMode"),
                new GUIContent("진행 방식"));
            EditorGUILayout.PropertyField(serialized.FindProperty("schedule"),
                new GUIContent("진행 기간"), true);
            SerializedProperty days = serialized.FindProperty("days");
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("일차별 보상", EditorStyles.boldLabel);
            DrawDayToolbar(serialized, days);
            EditorGUILayout.PropertyField(days, true);
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(selected);
            }
            else serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(10);
            DrawRewardTotals();
            EditorGUILayout.Space(8);
            DrawPreviewClock();
            EditorGUILayout.Space(8);
            DrawCalendarPreview();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDayToolbar(SerializedObject serialized, SerializedProperty days)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ 일차 추가", GUILayout.Width(92)))
            {
                Undo.RecordObject(selected, "Add attendance day");
                int index = days.arraySize;
                days.arraySize++;
                SerializedProperty day = days.GetArrayElementAtIndex(index);
                day.FindPropertyRelative("dayNumber").intValue = index + 1;
                ResetReward(day.FindPropertyRelative("reward"));
                serialized.ApplyModifiedProperties();
            }
            using (new EditorGUI.DisabledScope(days.arraySize == 0))
            {
                if (GUILayout.Button("마지막 삭제", GUILayout.Width(84)))
                {
                    Undo.RecordObject(selected, "Remove attendance day");
                    days.DeleteArrayElementAtIndex(days.arraySize - 1);
                    serialized.ApplyModifiedProperties();
                }
                if (GUILayout.Button("1부터 번호 정리", GUILayout.Width(110)))
                {
                    Undo.RecordObject(selected, "Normalize attendance days");
                    for (int i = 0; i < days.arraySize; i++)
                        days.GetArrayElementAtIndex(i).FindPropertyRelative("dayNumber").intValue = i + 1;
                    serialized.ApplyModifiedProperties();
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(days.arraySize + "일", EditorStyles.miniLabel,
                GUILayout.Width(34));
            EditorGUILayout.EndHorizontal();
        }

        void DrawRewardTotals()
        {
            long credits = 0, materials = 0, experience = 0, premium = 0;
            var itemTotals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selected.Days.Count; i++)
            {
                RewardPackage reward = selected.Days[i]?.Reward;
                if (reward == null) continue;
                credits += reward.Currency.Credits;
                materials += reward.Currency.SkillMaterials;
                experience += reward.Currency.AccountExperience;
                premium += reward.Currency.PremiumCurrency;
                for (int j = 0; j < reward.ItemRewards.Count; j++)
                {
                    ItemReward item = reward.ItemRewards[j];
                    if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) continue;
                    itemTotals.TryGetValue(item.ItemId, out long amount);
                    itemTotals[item.ItemId] = amount + item.Amount;
                }
            }
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("전체 보상 합계", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Credits " + credits.ToString("N0") + "   ·   Materials "
                + materials.ToString("N0") + "   ·   EXP " + experience.ToString("N0")
                + "   ·   Premium " + premium.ToString("N0"));
            foreach (KeyValuePair<string, long> item in itemTotals.OrderBy(pair => pair.Key))
                EditorGUILayout.LabelField(item.Key + " × " + item.Value.ToString("N0"),
                    EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void DrawPreviewClock()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("날짜 시뮬레이션", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            previewUtcText = EditorGUILayout.TextField("테스트 UTC", previewUtcText ?? string.Empty);
            if (GUILayout.Button("현재", GUILayout.Width(50)))
                previewUtcText = ContentTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            EditorGUILayout.EndHorizontal();
            bool valid = TryPreviewUtc(out DateTime preview);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("-1일")) previewUtcText = preview.AddDays(-1).ToString("O");
                if (GUILayout.Button("+1일")) previewUtcText = preview.AddDays(1).ToString("O");
                if (GUILayout.Button("+1주")) previewUtcText = preview.AddDays(7).ToString("O");
                if (GUILayout.Button("Time Simulator에 적용"))
                    ContentTime.TrySetOverride(preview);
            }
            EditorGUILayout.EndHorizontal();
            if (!valid)
                EditorGUILayout.HelpBox("ISO-8601 UTC 형식으로 입력해 주세요.", MessageType.Error);
            else
                EditorGUILayout.HelpBox("상태: " + StateLabel(selected.Schedule.GetState(preview))
                    + "  ·  신규 플레이어 기준 다음 보상 Day 1", MessageType.Info);
            EditorGUILayout.EndVertical();
        }

        void DrawCalendarPreview()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("사용자 화면 미리보기", EditorStyles.boldLabel);
            int count = Mathf.Min(7, selected.DayCount);
            for (int row = 0; row < 2; row++)
            {
                EditorGUILayout.BeginHorizontal();
                int start = row == 0 ? 0 : 4;
                int end = row == 0 ? Mathf.Min(4, count) : count;
                for (int i = start; i < end; i++)
                {
                    AttendanceDayDefinition day = selected.GetDay(i);
                    EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MinWidth(120),
                        GUILayout.Height(82));
                    EditorGUILayout.LabelField("Day " + (day?.DayNumber ?? i + 1),
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(day?.Reward?.Summary ?? "보상 없음",
                        EditorStyles.wordWrappedMiniLabel, GUILayout.Height(42));
                    EditorGUILayout.EndVertical();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            if (selected.DayCount > 7)
                EditorGUILayout.LabelField("미리보기는 첫 7일만 표시합니다.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void CreateCampaign()
        {
            AttendanceCampaignData campaign = AttendanceCampaignDatabaseBootstrap.CreateCampaign();
            if (campaign == null) return;
            Undo.RecordObject(database, "Register attendance campaign");
            database.Add(campaign);
            EditorUtility.SetDirty(database);
            selected = campaign;
            SaveAssets();
        }

        void DuplicateCampaign()
        {
            AttendanceCampaignData copy = AttendanceCampaignDatabaseBootstrap.DuplicateCampaign(selected);
            if (copy == null) return;
            Undo.RecordObject(database, "Register duplicated attendance campaign");
            database.Add(copy);
            EditorUtility.SetDirty(database);
            selected = copy;
            SaveAssets();
        }

        void DeleteCampaign()
        {
            if (selected == null || !EditorUtility.DisplayDialog("출석 캠페인 삭제",
                selected.DisplayName + " 에셋을 삭제할까요?", "삭제", "취소")) return;
            AttendanceCampaignData target = selected;
            string path = AssetDatabase.GetAssetPath(target);
            Undo.RecordObject(database, "Delete attendance campaign");
            database.Remove(target);
            selected = database.Campaigns.FirstOrDefault(campaign => campaign != null);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        }

        void MoveSelected(int offset)
        {
            Undo.RecordObject(database, "Reorder attendance campaigns");
            if (database.Move(selected, offset))
            {
                EditorUtility.SetDirty(database);
                SaveAssets();
            }
        }

        void SaveAssets()
        {
            if (database != null) EditorUtility.SetDirty(database);
            if (selected != null) EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
        }

        bool MatchesSearch(AttendanceCampaignData campaign)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string needle = search.Trim();
            return campaign.CampaignId.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || campaign.DisplayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool TryPreviewUtc(out DateTime value)
        {
            if (DateTime.TryParse(previewUtcText, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed))
            {
                value = ScheduleRange.NormalizeUtc(parsed);
                return true;
            }
            value = ContentTime.UtcNow;
            return false;
        }

        static void ResetReward(SerializedProperty reward)
        {
            if (reward == null) return;
            SerializedProperty currency = reward.FindPropertyRelative("currencyReward");
            string[] names = { "credits", "skillMaterials", "accountExperience", "premiumCurrency" };
            for (int i = 0; i < names.Length; i++)
                currency.FindPropertyRelative(names[i]).intValue = 0;
            reward.FindPropertyRelative("itemRewards").arraySize = 0;
        }

        static string StateLabel(ScheduleState state)
        {
            switch (state)
            {
                case ScheduleState.Upcoming: return "예정";
                case ScheduleState.Active: return "진행 중";
                case ScheduleState.Ended: return "종료";
                default: return "기간 오류";
            }
        }

        static void Ping(UnityEngine.Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
