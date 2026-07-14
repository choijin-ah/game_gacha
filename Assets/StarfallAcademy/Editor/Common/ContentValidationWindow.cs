using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public enum ContentValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class ContentValidationIssue
    {
        public ContentValidationIssue(ContentValidationSeverity severity, string category,
            string location, string message, UnityEngine.Object context = null)
        {
            Severity = severity;
            Category = category ?? "Content";
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
            Context = context;
        }

        public ContentValidationSeverity Severity { get; }
        public string Category { get; }
        public string Location { get; }
        public string Message { get; }
        public UnityEngine.Object Context { get; }
    }

    public static class ContentValidationRegistry
    {
        static readonly Dictionary<string, Func<IEnumerable<ContentValidationIssue>>> Providers =
            new Dictionary<string, Func<IEnumerable<ContentValidationIssue>>>(StringComparer.Ordinal);

        public static void Register(string id, Func<IEnumerable<ContentValidationIssue>> provider)
        {
            if (string.IsNullOrWhiteSpace(id) || provider == null) return;
            Providers[id.Trim()] = provider;
        }

        public static List<ContentValidationIssue> Collect(string providerId = null)
        {
            var results = new List<ContentValidationIssue>();
            IEnumerable<KeyValuePair<string, Func<IEnumerable<ContentValidationIssue>>>> selected =
                string.IsNullOrWhiteSpace(providerId)
                    ? Providers.OrderBy(pair => pair.Key)
                    : Providers.Where(pair => pair.Key == providerId);
            foreach (KeyValuePair<string, Func<IEnumerable<ContentValidationIssue>>> pair in selected)
            {
                try
                {
                    IEnumerable<ContentValidationIssue> issues = pair.Value();
                    if (issues != null) results.AddRange(issues.Where(issue => issue != null));
                }
                catch (Exception exception)
                {
                    results.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                        pair.Key, "Validator", exception.Message));
                }
            }
            return results;
        }

        public static IReadOnlyCollection<string> ProviderIds => Providers.Keys;
    }

    /// <summary>Bridges the pre-existing stage/story content into the shared validation window.</summary>
    static class CoreContentValidationProviders
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            ContentValidationRegistry.Register("Stages", ValidateStages);
            ContentValidationRegistry.Register("Story", ValidateStory);
        }

        static IEnumerable<ContentValidationIssue> ValidateStages()
        {
            var issues = new List<ContentValidationIssue>();
            var ids = new Dictionary<string, StageData>(StringComparer.Ordinal);
            string[] guids = AssetDatabase.FindAssets("t:StageData");
            if (guids.Length == 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    "Stage", "Database", "StageData 에셋이 없습니다."));
                return issues;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (stage == null) continue;
                var serialized = new SerializedObject(stage);
                string rawId = serialized.FindProperty("stageId")?.stringValue ?? string.Empty;
                string id = rawId.Trim();
                if (string.IsNullOrWhiteSpace(id))
                    issues.Add(StageIssue(ContentValidationSeverity.Error, stage.name,
                        "stageId가 비어 있습니다.", stage));
                else if (ids.TryGetValue(id, out StageData first))
                    issues.Add(StageIssue(ContentValidationSeverity.Error, id,
                        "stageId가 중복되었습니다. 첫 에셋: " + first.name, stage));
                else ids.Add(id, stage);

                if (string.IsNullOrWhiteSpace(stage.DisplayName))
                    issues.Add(StageIssue(ContentValidationSeverity.Error, id,
                        "표시 이름이 비어 있습니다.", stage));
                if (stage.EnemyCount <= 0)
                    issues.Add(StageIssue(ContentValidationSeverity.Error, id,
                        "적 구성이 비어 있습니다.", stage));
                if (stage.FirstClearRewardPackage != null
                    && !stage.FirstClearRewardPackage.IsValid)
                    issues.Add(StageIssue(ContentValidationSeverity.Error, id,
                        "최초 클리어 RewardPackage가 올바르지 않습니다.", stage));
                if (stage.RepeatClearRewardPackage != null
                    && !stage.RepeatClearRewardPackage.IsValid)
                    issues.Add(StageIssue(ContentValidationSeverity.Error, id,
                        "반복 클리어 RewardPackage가 올바르지 않습니다.", stage));
            }
            return issues;
        }

        static IEnumerable<ContentValidationIssue> ValidateStory()
        {
            var issues = new List<ContentValidationIssue>();
            string[] guids = AssetDatabase.FindAssets("t:StoryDatabase");
            if (guids.Length == 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error,
                    "Story", "Database", "StoryDatabase 에셋이 없습니다."));
                return issues;
            }

            for (int databaseIndex = 0; databaseIndex < guids.Length; databaseIndex++)
            {
                StoryDatabase database = AssetDatabase.LoadAssetAtPath<StoryDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[databaseIndex]));
                if (database == null) continue;
                var episodeIds = new HashSet<string>(StringComparer.Ordinal);
                for (int episodeIndex = 0; episodeIndex < database.Episodes.Count; episodeIndex++)
                {
                    StoryEpisode episode = database.Episodes[episodeIndex];
                    if (episode == null)
                    {
                        issues.Add(StoryIssue(ContentValidationSeverity.Error,
                            database.name + " / Entry " + (episodeIndex + 1),
                            "에피소드 참조가 비어 있습니다.", database));
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(episode.Id) || !episodeIds.Add(episode.Id))
                        issues.Add(StoryIssue(ContentValidationSeverity.Error, episode.Id,
                            "episodeId가 비어 있거나 중복되었습니다.", episode));
                }

                for (int episodeIndex = 0; episodeIndex < database.Episodes.Count; episodeIndex++)
                {
                    StoryEpisode episode = database.Episodes[episodeIndex];
                    if (episode == null) continue;
                    if (!string.IsNullOrWhiteSpace(episode.PrerequisiteEpisodeId)
                        && database.FindEpisode(episode.PrerequisiteEpisodeId) == null)
                        issues.Add(StoryIssue(ContentValidationSeverity.Error, episode.Id,
                            "선행 에피소드 참조가 없습니다: " + episode.PrerequisiteEpisodeId,
                            episode));

                    var lineIds = new HashSet<string>(StringComparer.Ordinal);
                    for (int lineIndex = 0; lineIndex < episode.Lines.Count; lineIndex++)
                    {
                        StoryLine line = episode.Lines[lineIndex];
                        string location = episode.Id + " / Line " + (lineIndex + 1);
                        if (line == null || string.IsNullOrWhiteSpace(line.Id)
                            || !lineIds.Add(line.Id))
                        {
                            issues.Add(StoryIssue(ContentValidationSeverity.Error, location,
                                "lineId가 비어 있거나 중복되었습니다.", episode));
                            continue;
                        }
                        if (line.Choices == null) continue;
                        for (int choiceIndex = 0; choiceIndex < line.Choices.Count; choiceIndex++)
                        {
                            StoryChoice choice = line.Choices[choiceIndex];
                            if (choice == null) continue;
                            StoryEpisode targetEpisode = string.IsNullOrWhiteSpace(choice.NextEpisodeId)
                                ? episode : database.FindEpisode(choice.NextEpisodeId);
                            if (targetEpisode == null)
                                issues.Add(StoryIssue(ContentValidationSeverity.Error, location,
                                    "선택지가 없는 에피소드를 참조합니다: " + choice.NextEpisodeId,
                                    episode));
                            else if (!string.IsNullOrWhiteSpace(choice.NextLineId)
                                && targetEpisode.FindLine(choice.NextLineId) == null)
                                issues.Add(StoryIssue(ContentValidationSeverity.Error, location,
                                    "선택지가 없는 라인을 참조합니다: " + choice.NextLineId,
                                    episode));
                        }
                    }
                }
            }
            return issues;
        }

        static ContentValidationIssue StageIssue(ContentValidationSeverity severity,
            string location, string message, UnityEngine.Object context) =>
            new ContentValidationIssue(severity, "Stage", location, message, context);

        static ContentValidationIssue StoryIssue(ContentValidationSeverity severity,
            string location, string message, UnityEngine.Object context) =>
            new ContentValidationIssue(severity, "Story", location, message, context);
    }

    public sealed class ContentValidationWindow : EditorWindow
    {
        readonly List<ContentValidationIssue> issues = new List<ContentValidationIssue>();
        Vector2 scroll;
        string search = string.Empty;
        bool showInfo = true;
        bool showWarnings = true;
        bool showErrors = true;
        string providerId;

        [MenuItem("Starfall/Validate/All Content")]
        public static void OpenAll() => Open(null);

        public static void Open(string provider)
        {
            var window = GetWindow<ContentValidationWindow>("Content Validation");
            window.minSize = new Vector2(760, 480);
            window.providerId = provider;
            window.Refresh();
            window.Show();
        }

        void OnEnable() => Refresh();

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL CONTENT VALIDATION", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(providerId)
                ? "All registered content validators" : providerId, EditorStyles.miniLabel);
            DrawToolbar();
            DrawSummary();
            DrawIssues();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70))) Refresh();
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(55))) issues.Clear();
            GUILayout.Space(6);
            showErrors = GUILayout.Toggle(showErrors, "Errors", EditorStyles.toolbarButton, GUILayout.Width(60));
            showWarnings = GUILayout.Toggle(showWarnings, "Warnings", EditorStyles.toolbarButton, GUILayout.Width(72));
            showInfo = GUILayout.Toggle(showInfo, "Info", EditorStyles.toolbarButton, GUILayout.Width(45));
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search ?? string.Empty, GUI.skin.FindStyle("ToolbarSearchTextField"),
                GUILayout.Width(230));
            EditorGUILayout.EndHorizontal();
        }

        void DrawSummary()
        {
            int errors = issues.Count(issue => issue.Severity == ContentValidationSeverity.Error);
            int warnings = issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning);
            MessageType type = errors > 0 ? MessageType.Error
                : warnings > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(errors + " error(s), " + warnings + " warning(s), "
                + (issues.Count - errors - warnings) + " info item(s).", type);
        }

        void DrawIssues()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            IEnumerable<ContentValidationIssue> filtered = issues.Where(IsVisible);
            if (!filtered.Any())
            {
                EditorGUILayout.HelpBox(issues.Count == 0
                    ? "No validation issues were found." : "No issues match the current filter.",
                    MessageType.Info);
            }
            foreach (ContentValidationIssue issue in filtered)
            {
                Color previous = GUI.backgroundColor;
                GUI.backgroundColor = issue.Severity == ContentValidationSeverity.Error
                    ? new Color(1f, .55f, .55f) : issue.Severity == ContentValidationSeverity.Warning
                        ? new Color(1f, .82f, .45f) : new Color(.65f, .82f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = previous;
                Rect header = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(header, issue.Severity + "   " + issue.Category + " / "
                    + issue.Location, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
                if (issue.Context != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select asset", GUILayout.Width(90))) Ping(issue.Context);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();

                if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2
                    && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition)
                    && issue.Context != null)
                {
                    Ping(issue.Context);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        bool IsVisible(ContentValidationIssue issue)
        {
            if (issue.Severity == ContentValidationSeverity.Error && !showErrors) return false;
            if (issue.Severity == ContentValidationSeverity.Warning && !showWarnings) return false;
            if (issue.Severity == ContentValidationSeverity.Info && !showInfo) return false;
            if (string.IsNullOrWhiteSpace(search)) return true;
            string needle = search.Trim();
            return issue.Category.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || issue.Location.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                || issue.Message.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Refresh()
        {
            issues.Clear();
            issues.AddRange(ContentValidationRegistry.Collect(providerId));
            Repaint();
        }

        static void Ping(UnityEngine.Object context)
        {
            Selection.activeObject = context;
            EditorGUIUtility.PingObject(context);
        }
    }
}
