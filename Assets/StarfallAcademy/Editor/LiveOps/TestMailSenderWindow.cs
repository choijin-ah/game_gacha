using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class TestMailSenderWindow : EditorWindow
    {
        MailTemplateDatabase database;
        [SerializeField] MailTemplateData selectedTemplate;
        [SerializeField] bool overrideTitle;
        [SerializeField] string titleOverride = string.Empty;
        [SerializeField] bool overrideBody;
        [SerializeField, TextArea(4, 8)] string bodyOverride = string.Empty;
        [SerializeField] bool overrideSender;
        [SerializeField] string senderOverride = string.Empty;
        [SerializeField] bool overrideExpiry;
        [SerializeField, Min(0)] int expiryHoursOverride = 168;
        [SerializeField] bool overrideAttachments;
        [SerializeField] RewardPackage attachmentsOverride = new RewardPackage();
        Vector2 scroll;

        [MenuItem("Starfall/LiveOps/Send Test Mail")]
        public static void Open()
        {
            var window = GetWindow<TestMailSenderWindow>("Send Test Mail");
            window.minSize = new Vector2(650, 620);
            window.Show();
        }

        public static void OpenWithTemplate(MailTemplateData template)
        {
            Open();
            var window = GetWindow<TestMailSenderWindow>();
            window.selectedTemplate = template;
            window.Repaint();
        }

        void OnEnable()
        {
            database = MailTemplateDatabaseBootstrap.LoadOrCreate();
            if (selectedTemplate == null && database != null && database.Templates.Count > 0)
                selectedTemplate = database.Templates.FirstOrDefault(template => template != null);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL TEST MAIL SENDER", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("콘텐츠 에셋을 변경하지 않고 현재 로컬 계정에 우편을 만듭니다.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            if (database == null)
            {
                EditorGUILayout.HelpBox("MailTemplateDatabase를 만들 수 없습니다.", MessageType.Error);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawTemplateSelection();
            EditorGUILayout.Space(8);
            DrawOverrides();
            EditorGUILayout.Space(8);
            DrawPreview();
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(Application.isPlaying
                ? "Play Mode의 현재 로컬 계정에 즉시 반영됩니다."
                : "Edit Mode PlayerPrefs의 로컬 계정에 저장됩니다. 다음 실행에서 우편함이 읽습니다.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(selectedTemplate == null))
            {
                if (GUILayout.Button("현재 로컬 계정에 발송", GUILayout.Height(42))) Send();
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawTemplateSelection()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("템플릿 선택", EditorStyles.boldLabel);
            string[] labels = database.Templates.Select(template => template == null
                ? "<Missing>" : template.Title + "  [" + template.TemplateId + "]").ToArray();
            int index = -1;
            for (int i = 0; i < database.Templates.Count; i++)
                if (database.Templates[i] == selectedTemplate) index = i;
            int next = labels.Length == 0 ? -1 : EditorGUILayout.Popup("템플릿",
                Mathf.Clamp(index, 0, labels.Length - 1), labels);
            if (next >= 0 && next < database.Templates.Count)
                selectedTemplate = database.Templates[next];
            if (selectedTemplate == null)
                EditorGUILayout.HelpBox("먼저 Mail Templates 창에서 템플릿을 만드세요.",
                    MessageType.Warning);
            else if (GUILayout.Button("템플릿 에셋 찾기"))
            {
                Selection.activeObject = selectedTemplate;
                EditorGUIUtility.PingObject(selectedTemplate);
            }
            EditorGUILayout.EndVertical();
        }

        void DrawOverrides()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("발송 시 덮어쓰기", EditorStyles.boldLabel);
            overrideTitle = EditorGUILayout.ToggleLeft("제목 덮어쓰기", overrideTitle);
            if (overrideTitle) titleOverride = EditorGUILayout.TextField("제목", titleOverride ?? string.Empty);
            overrideBody = EditorGUILayout.ToggleLeft("본문 덮어쓰기", overrideBody);
            if (overrideBody)
            {
                EditorGUILayout.LabelField("본문");
                bodyOverride = EditorGUILayout.TextArea(bodyOverride ?? string.Empty,
                    GUILayout.MinHeight(80));
            }
            overrideSender = EditorGUILayout.ToggleLeft("발신자 덮어쓰기", overrideSender);
            if (overrideSender)
                senderOverride = EditorGUILayout.TextField("발신자", senderOverride ?? string.Empty);
            overrideExpiry = EditorGUILayout.ToggleLeft("만료 시간 덮어쓰기", overrideExpiry);
            if (overrideExpiry)
                expiryHoursOverride = Mathf.Max(0,
                    EditorGUILayout.IntField("만료 시간", expiryHoursOverride));
            overrideAttachments = EditorGUILayout.ToggleLeft("첨부 보상 덮어쓰기", overrideAttachments);
            if (overrideAttachments)
            {
                SerializedObject serialized = new SerializedObject(this);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("attachmentsOverride"),
                    new GUIContent("첨부 보상"), true);
                serialized.ApplyModifiedProperties();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawPreview()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("발송 미리보기", EditorStyles.boldLabel);
            if (selectedTemplate == null)
            {
                EditorGUILayout.LabelField("템플릿을 선택하세요.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            string title = overrideTitle ? titleOverride : selectedTemplate.Title;
            string body = overrideBody ? bodyOverride : selectedTemplate.Body;
            string sender = overrideSender ? senderOverride : selectedTemplate.Sender;
            int expiry = overrideExpiry ? expiryHoursOverride : selectedTemplate.DefaultExpiryHours;
            RewardPackage reward = overrideAttachments ? attachmentsOverride : selectedTemplate.Attachments;
            EditorGUILayout.LabelField(title ?? string.Empty, EditorStyles.largeLabel);
            EditorGUILayout.LabelField("보낸 사람  " + (sender ?? string.Empty), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(expiry == 0 ? "만료 없음" : expiry + "시간 후 만료",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(body ?? string.Empty, EditorStyles.wordWrappedLabel,
                GUILayout.MinHeight(65));
            EditorGUILayout.HelpBox(reward == null || reward.IsEmpty
                ? "첨부 보상 없음" : reward.Summary,
                reward != null && reward.IsValid ? MessageType.Info : MessageType.Error);
            EditorGUILayout.EndVertical();
        }

        void Send()
        {
            if (selectedTemplate == null) return;
            if (!EditorUtility.DisplayDialog("테스트 우편 발송",
                "현재 로컬 계정에 테스트 우편을 발송할까요?", "발송", "취소")) return;
            var options = new MailSendOptions
            {
                OverrideTitle = overrideTitle,
                Title = titleOverride,
                OverrideBody = overrideBody,
                Body = bodyOverride,
                OverrideSender = overrideSender,
                Sender = senderOverride,
                OverrideExpiryHours = overrideExpiry,
                ExpiryHours = expiryHoursOverride,
                OverrideAttachments = overrideAttachments,
                Attachments = attachmentsOverride
            };
            MailSendResult result = MailService.Default.Send(selectedTemplate, options);
            if (result.Succeeded)
            {
                Debug.Log("[Starfall Mail] " + result.Message + "  id="
                    + result.Mail.MailInstanceId);
                ShowNotification(new GUIContent("테스트 우편 발송 완료"));
            }
            else
            {
                Debug.LogError("[Starfall Mail] " + result.Message);
                EditorUtility.DisplayDialog("발송 실패", result.Message, "확인");
            }
        }
    }
}
