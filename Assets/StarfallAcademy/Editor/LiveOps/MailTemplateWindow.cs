using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class MailTemplateWindow : EditorWindow
    {
        MailTemplateDatabase database;
        MailTemplateData selected;
        Vector2 listScroll;
        Vector2 detailScroll;
        string search = string.Empty;

        [MenuItem("Starfall/LiveOps/Mail Templates")]
        public static void Open()
        {
            var window = GetWindow<MailTemplateWindow>("Mail Templates");
            window.minSize = new Vector2(940, 620);
            window.Show();
        }

        void OnEnable()
        {
            database = MailTemplateDatabaseBootstrap.LoadOrCreate();
            if (selected == null && database != null && database.Templates.Count > 0)
                selected = database.Templates[0];
        }

        void OnGUI()
        {
            DrawHeader();
            if (database == null)
            {
                EditorGUILayout.HelpBox("MailTemplateDatabase를 만들 수 없습니다.",
                    MessageType.Error);
                if (GUILayout.Button("다시 만들기"))
                    database = MailTemplateDatabaseBootstrap.LoadOrCreate();
                return;
            }
            EditorGUILayout.BeginHorizontal();
            DrawTemplateList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL MAIL TEMPLATE TOOL", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("우편 본문, 만료 시간과 첨부 보상을 편집합니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ 새 템플릿", EditorStyles.toolbarButton, GUILayout.Width(105)))
                CreateTemplate();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    DuplicateTemplate();
                if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(52)))
                    DeleteTemplate();
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(-1);
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(1);
            }
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(52))) SaveAssets();
            if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(52)))
                ContentValidationWindow.Open(LiveOpsValidators.MailProviderId);
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search ?? string.Empty,
                GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(220));
            EditorGUILayout.EndHorizontal();
        }

        void DrawTemplateList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            EditorGUILayout.LabelField("템플릿  " + database.Templates.Count,
                EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            IEnumerable<MailTemplateData> filtered = database.Templates
                .Where(template => template != null && MatchesSearch(template));
            bool any = false;
            foreach (MailTemplateData template in filtered)
            {
                any = true;
                Color previous = GUI.backgroundColor;
                if (template == selected) GUI.backgroundColor = new Color(.54f, .78f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = previous;
                EditorGUILayout.LabelField(template.Title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(template.TemplateId, EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(template.DefaultExpiryHours == 0 ? "만료 없음"
                    : template.DefaultExpiryHours + "시간", EditorStyles.miniLabel);
                if (GUILayout.Button("편집", GUILayout.Width(48))) selected = template;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (!any)
                EditorGUILayout.HelpBox(string.IsNullOrWhiteSpace(search)
                    ? "등록된 우편 템플릿이 없습니다." : "검색 결과가 없습니다.",
                    MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDetail()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 템플릿을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("템플릿 상세", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("테스트 발송", GUILayout.Width(90)))
                TestMailSenderWindow.OpenWithTemplate(selected);
            if (GUILayout.Button("Project에서 찾기", GUILayout.Width(120))) Ping(selected);
            EditorGUILayout.EndHorizontal();

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll, GUI.skin.box);
            SerializedObject serialized = new SerializedObject(selected);
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty("templateId"),
                new GUIContent("템플릿 ID"));
            EditorGUILayout.PropertyField(serialized.FindProperty("title"), new GUIContent("제목"));
            EditorGUILayout.PropertyField(serialized.FindProperty("sender"), new GUIContent("발신자"));
            EditorGUILayout.PropertyField(serialized.FindProperty("defaultExpiryHours"),
                new GUIContent("기본 만료 시간"));
            EditorGUILayout.PropertyField(serialized.FindProperty("body"), new GUIContent("본문"));
            EditorGUILayout.PropertyField(serialized.FindProperty("attachments"),
                new GUIContent("첨부 보상"), true);
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(selected);
            }
            else serialized.ApplyModifiedProperties();

            EditorGUILayout.Space(12);
            DrawMailPreview();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawMailPreview()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.MinHeight(240));
            EditorGUILayout.LabelField("사용자 화면 미리보기", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(selected.Title, new GUIStyle(EditorStyles.largeLabel)
            {
                fontStyle = FontStyle.Bold
            });
            EditorGUILayout.LabelField("보낸 사람  " + selected.Sender, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(selected.DefaultExpiryHours == 0 ? "만료 기한 없음"
                : "발송 후 " + selected.DefaultExpiryHours + "시간 동안 수령 가능",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(selected.Body, EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(90));
            EditorGUILayout.Space(6);
            RewardPackage attachments = selected.Attachments;
            EditorGUILayout.HelpBox(attachments == null || attachments.IsEmpty
                ? "첨부 보상 없음" : "첨부  ·  " + attachments.Summary,
                attachments != null && attachments.IsValid ? MessageType.Info : MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        void CreateTemplate()
        {
            MailTemplateData template = MailTemplateDatabaseBootstrap.CreateTemplate();
            if (template == null) return;
            Undo.RecordObject(database, "Register mail template");
            database.Add(template);
            EditorUtility.SetDirty(database);
            selected = template;
            SaveAssets();
        }

        void DuplicateTemplate()
        {
            MailTemplateData copy = MailTemplateDatabaseBootstrap.DuplicateTemplate(selected);
            if (copy == null) return;
            Undo.RecordObject(database, "Register duplicated mail template");
            database.Add(copy);
            EditorUtility.SetDirty(database);
            selected = copy;
            SaveAssets();
        }

        void DeleteTemplate()
        {
            if (selected == null || !EditorUtility.DisplayDialog("우편 템플릿 삭제",
                selected.Title + " 에셋을 삭제할까요?", "삭제", "취소")) return;
            MailTemplateData target = selected;
            string path = AssetDatabase.GetAssetPath(target);
            Undo.RecordObject(database, "Delete mail template");
            database.Remove(target);
            selected = database.Templates.FirstOrDefault(template => template != null);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        }

        void MoveSelected(int offset)
        {
            Undo.RecordObject(database, "Reorder mail templates");
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

        bool MatchesSearch(MailTemplateData template)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            string needle = search.Trim();
            return template.TemplateId.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || template.Title.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || template.Sender.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void Ping(UnityEngine.Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
