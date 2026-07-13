using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public enum StoryImportMode
    {
        Merge,
        Replace
    }

    public sealed class StoryImportWindow : EditorWindow
    {
        internal const string TemplateHeader =
            "episode_id,episode_title,category,sort_order,summary,focus_character,thumbnail,banner,initially_unlocked,unlock_key,prerequisite_episode," +
            "line_id,speaker,speaker_name,speaker_position,text," +
            "left_character,left_expression_key,left_expression_sprite,left_visible,left_flip,left_tint,left_offset_x,left_offset_y," +
            "center_character,center_expression_key,center_expression_sprite,center_visible,center_flip,center_tint,center_offset_x,center_offset_y," +
            "right_character,right_expression_key,right_expression_sprite,right_visible,right_flip,right_tint,right_offset_x,right_offset_y," +
            "background,cg,bgm,sfx,voice,transition,effects,shake_strength,effect_duration,text_speed,auto_duration," +
            "choice1_text,choice1_next_episode,choice1_next_line,choice1_condition," +
            "choice2_text,choice2_next_episode,choice2_next_line,choice2_condition," +
            "choice3_text,choice3_next_episode,choice3_next_line,choice3_condition," +
            "choice4_text,choice4_next_episode,choice4_next_line,choice4_condition";

        StoryDatabase database;
        string sourcePath = string.Empty;
        StoryImportMode importMode = StoryImportMode.Merge;
        bool importAllSheets = true;
        IReadOnlyList<StorySheetTable> previewTables;
        string previewError;
        Vector2 scroll;

        [MenuItem("Starfall/Story/Import Excel")]
        public static void OpenFromMenu()
        {
            Open(StoryDatabaseBootstrap.EnsureDatabase());
        }

        public static void Open(StoryDatabase targetDatabase)
        {
            var window = GetWindow<StoryImportWindow>(true, "Story Excel Import", true);
            window.minSize = new Vector2(760f, 560f);
            window.database = targetDatabase != null ? targetDatabase : StoryDatabaseBootstrap.EnsureDatabase();
            window.Show();
        }

        void OnEnable()
        {
            if (database == null) database = StoryDatabaseBootstrap.EnsureDatabase();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("STORY SPREADSHEET IMPORT", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Excel(.xlsx), CSV, TSV의 한 행을 비주얼노벨 대사 한 줄로 가져옵니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8f);

            database = (StoryDatabase)EditorGUILayout.ObjectField("대상 데이터베이스", database, typeof(StoryDatabase), false);
            importMode = (StoryImportMode)EditorGUILayout.EnumPopup("가져오기 방식", importMode);
            if (importMode == StoryImportMode.Replace)
                EditorGUILayout.HelpBox("Replace는 데이터베이스의 등록 목록을 가져온 에피소드로 교체합니다. 기존 에셋 파일은 안전하게 남겨 둡니다.", MessageType.Warning);
            importAllSheets = EditorGUILayout.Toggle("모든 시트 읽기", importAllSheets);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("파일");
            sourcePath = EditorGUILayout.TextField(sourcePath);
            if (GUILayout.Button("찾기…", GUILayout.Width(70f))) Browse();
            if (GUILayout.Button("미리 읽기", GUILayout.Width(80f))) LoadPreview();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("템플릿 헤더 복사"))
            {
                EditorGUIUtility.systemCopyBuffer = TemplateHeader;
                ShowNotification(new GUIContent("헤더를 클립보드에 복사했습니다."));
            }
            if (GUILayout.Button("Excel 양식 다운로드…")) SaveExcelTemplate();
            if (GUILayout.Button("CSV 양식 저장…")) SaveCsvTemplate();
            if (GUILayout.Button("열 스키마 안내 선택"))
            {
                UnityEngine.Object schema = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "Assets/StarfallAcademy/Editor/Story/STORY_IMPORT_SCHEMA.md");
                Selection.activeObject = schema;
                EditorGUIUtility.PingObject(schema);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUI.skin.box);
            DrawSchemaSummary();
            DrawPreview();
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(database == null || string.IsNullOrWhiteSpace(sourcePath)))
            {
                if (GUILayout.Button("스토리 데이터 가져오기", GUILayout.Height(38f))) Import();
            }
        }

        void Browse()
        {
            string initial = string.IsNullOrWhiteSpace(sourcePath) ? string.Empty : Path.GetDirectoryName(sourcePath);
            string selected = EditorUtility.OpenFilePanel("스토리 Excel/CSV 선택", initial, string.Empty);
            if (string.IsNullOrWhiteSpace(selected)) return;
            string extension = Path.GetExtension(selected).ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".csv" && extension != ".tsv" && extension != ".txt")
            {
                EditorUtility.DisplayDialog("지원하지 않는 파일", ".xlsx, .csv, .tsv만 선택할 수 있습니다.", "확인");
                return;
            }
            sourcePath = selected;
            LoadPreview();
        }

        void LoadPreview()
        {
            try
            {
                previewTables = StorySpreadsheetReader.Read(sourcePath, importAllSheets);
                previewError = null;
            }
            catch (Exception exception)
            {
                previewTables = null;
                previewError = exception.Message;
            }
            Repaint();
        }

        void DrawSchemaSummary()
        {
            EditorGUILayout.LabelField("필수 열", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("episode_id · episode_title · category · line_id · text", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("category: Main / Event / Character / Side (메인/이벤트/캐릭터/사이드도 인식)", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("에셋 셀: Assets/... 경로, GUID, 또는 에셋 이름. 캐릭터는 CharacterData ID/표시 이름도 인식합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("스토리 오디오: bgm / sfx / voice 열에 AudioClip을 지정합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("effects: Shake|FlashWhite|FadeIn처럼 | 또는 , 로 여러 효과를 지정합니다.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6f);
        }

        void DrawPreview()
        {
            if (!string.IsNullOrWhiteSpace(previewError))
            {
                EditorGUILayout.HelpBox(previewError, MessageType.Error);
                return;
            }
            if (previewTables == null)
            {
                EditorGUILayout.HelpBox("파일을 선택하면 시트와 대사 행을 여기서 확인할 수 있습니다.", MessageType.Info);
                return;
            }

            int totalRows = previewTables.Sum(table => table.Rows.Count);
            EditorGUILayout.LabelField($"미리보기 · {previewTables.Count}개 시트 · {totalRows}개 행", EditorStyles.boldLabel);
            foreach (StorySheetTable table in previewTables)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField($"{table.Name}  ({table.Rows.Count}행, {table.Headers.Count}열)", EditorStyles.boldLabel);
                for (int i = 0; i < Mathf.Min(12, table.Rows.Count); i++)
                {
                    Dictionary<string, string> row = table.Rows[i];
                    string episode = Value(row, "episode_id", "에피소드_id", "에피소드id");
                    string line = Value(row, "line_id", "라인_id", "라인id");
                    string speaker = Value(row, "speaker_name", "speaker", "화자명", "화자");
                    string text = Value(row, "text", "대사", "본문").Replace('\n', ' ');
                    if (text.Length > 58) text = text.Substring(0, 58) + "…";
                    EditorGUILayout.LabelField($"{i + 1:000}  [{episode}/{line}] {speaker}: {text}", EditorStyles.miniLabel);
                }
                if (table.Rows.Count > 12) EditorGUILayout.LabelField($"… {table.Rows.Count - 12}행 더 있음", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        void SaveExcelTemplate()
        {
            string path = EditorUtility.SaveFilePanel("스토리 Excel 양식 저장", string.Empty,
                "Starfall_Story_Template.xlsx", "xlsx");
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                string[] headers = TemplateHeader.Split(',');
                StorySpreadsheetTemplateWriter.Write(path, headers, CreateTemplateRows(headers));
                EditorUtility.RevealInFinder(path);
                ShowNotification(new GUIContent("Excel 스토리 양식을 저장했습니다."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Excel 양식 저장 실패", exception.Message, "확인");
            }
        }

        void SaveCsvTemplate()
        {
            string path = EditorUtility.SaveFilePanel("스토리 CSV 템플릿 저장", string.Empty, "Starfall_Story_Template.csv", "csv");
            if (string.IsNullOrWhiteSpace(path)) return;
            string[] headers = TemplateHeader.Split(',');
            IEnumerable<string> samples = CreateTemplateRows(headers)
                .Select(values => string.Join(",", values.Select(CsvCell)));
            File.WriteAllText(path, TemplateHeader + Environment.NewLine +
                string.Join(Environment.NewLine, samples) + Environment.NewLine, new UTF8Encoding(true));
            EditorUtility.RevealInFinder(path);
        }

        static IReadOnlyList<string[]> CreateTemplateRows(IReadOnlyList<string> headers)
        {
            var first = new string[headers.Count];
            SetTemplateValue(headers, first, "episode_id", "main_01");
            SetTemplateValue(headers, first, "episode_title", "프롤로그");
            SetTemplateValue(headers, first, "category", "Main");
            SetTemplateValue(headers, first, "sort_order", "0");
            SetTemplateValue(headers, first, "summary", "첫 번째 이야기");
            SetTemplateValue(headers, first, "initially_unlocked", "true");
            SetTemplateValue(headers, first, "line_id", "line_001");
            SetTemplateValue(headers, first, "speaker_name", "아리아");
            SetTemplateValue(headers, first, "speaker_position", "Center");
            SetTemplateValue(headers, first, "text", "별이 내리는 밤이야.");
            SetTemplateValue(headers, first, "transition", "CrossFade");
            SetTemplateValue(headers, first, "effects", "FadeIn");
            SetTemplateValue(headers, first, "shake_strength", "8");
            SetTemplateValue(headers, first, "effect_duration", "0.35");
            SetTemplateValue(headers, first, "text_speed", "0.035");
            SetTemplateValue(headers, first, "auto_duration", "2");

            var second = new string[headers.Count];
            SetTemplateValue(headers, second, "episode_id", "main_01");
            SetTemplateValue(headers, second, "line_id", "line_002");
            SetTemplateValue(headers, second, "speaker_position", "Narrator");
            SetTemplateValue(headers, second, "text", "그날, 우리의 이야기가 시작되었다.");
            SetTemplateValue(headers, second, "transition", "None");

            return new[] { first, second };
        }

        static void SetTemplateValue(IReadOnlyList<string> headers, string[] values, string header, string value)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (!string.Equals(headers[i], header, StringComparison.Ordinal)) continue;
                values[i] = value;
                return;
            }
        }

        static string CsvCell(string value)
        {
            value = value ?? string.Empty;
            return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        void Import()
        {
            try
            {
                IReadOnlyList<StorySheetTable> tables = StorySpreadsheetReader.Read(sourcePath, importAllSheets);
                ImportResult result = StorySpreadsheetImporter.Import(database, tables, importMode);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                StoryReferenceValidator.ValidateAll();
                EditorUtility.DisplayDialog("스토리 가져오기 완료",
                    $"에피소드 {result.EpisodeCount}개, 대사 {result.LineCount}줄을 반영했습니다." +
                    (result.WarningCount > 0 ? $"\n확인할 경고: {result.WarningCount}개 (Console 참조)" : string.Empty), "확인");
                previewTables = tables;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("스토리 가져오기 실패", exception.Message, "확인");
            }
        }

        static string Value(Dictionary<string, string> row, params string[] names)
        {
            foreach (string name in names)
                if (row.TryGetValue(name, out string value)) return value ?? string.Empty;
            return string.Empty;
        }
    }

    internal readonly struct ImportResult
    {
        public int EpisodeCount { get; }
        public int LineCount { get; }
        public int WarningCount { get; }

        public ImportResult(int episodeCount, int lineCount, int warningCount)
        {
            EpisodeCount = episodeCount;
            LineCount = lineCount;
            WarningCount = warningCount;
        }
    }

    internal static class StorySpreadsheetImporter
    {
        public static ImportResult Import(StoryDatabase database, IReadOnlyList<StorySheetTable> tables, StoryImportMode mode)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (tables == null || tables.Count == 0) throw new InvalidDataException("가져올 시트가 없습니다.");

            var groups = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            int warnings = 0;
            foreach (StorySheetTable table in tables)
            {
                foreach (Dictionary<string, string> row in table.Rows)
                {
                    string episodeId = Get(row, "episode_id", "에피소드_id", "에피소드id").Trim();
                    if (string.IsNullOrWhiteSpace(episodeId))
                    {
                        warnings++;
                        Debug.LogWarning($"[Story Import] {table.Name}: episode_id가 빈 행을 건너뜁니다.");
                        continue;
                    }
                    if (!groups.TryGetValue(episodeId, out List<Dictionary<string, string>> rows))
                    {
                        rows = new List<Dictionary<string, string>>();
                        groups.Add(episodeId, rows);
                    }
                    rows.Add(row);
                }
            }
            if (groups.Count == 0) throw new InvalidDataException("episode_id가 들어 있는 유효한 행이 없습니다.");

            StoryDatabaseBootstrap.EnsureFolders();
            Undo.RegisterCompleteObjectUndo(database, "Import story spreadsheet");
            var importedEpisodes = new List<StoryEpisode>();
            int lineCount = 0;
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> group in groups)
            {
                StoryEpisode episode = database.FindEpisode(group.Key);
                if (episode == null) episode = FindEpisodeAsset(group.Key);
                bool created = episode == null;
                if (created)
                {
                    episode = ScriptableObject.CreateInstance<StoryEpisode>();
                    episode.name = SanitizeFileName(group.Key);
                    string path = AssetDatabase.GenerateUniqueAssetPath(
                        $"{StoryDatabaseBootstrap.EpisodeFolder}/{SanitizeFileName(group.Key)}.asset");
                    AssetDatabase.CreateAsset(episode, path);
                    Undo.RegisterCreatedObjectUndo(episode, "Create imported story episode");
                }
                else Undo.RecordObject(episode, "Import story episode");

                ApplyEpisode(episode, group.Key, group.Value[0]);
                lineCount += ApplyLines(episode, group.Value, mode == StoryImportMode.Merge && !created, ref warnings);
                EditorUtility.SetDirty(episode);
                importedEpisodes.Add(episode);
                if (mode == StoryImportMode.Merge) database.AddEpisode(episode);
            }

            if (mode == StoryImportMode.Replace) database.ReplaceEpisodes(importedEpisodes);
            EditorUtility.SetDirty(database);
            return new ImportResult(importedEpisodes.Count, lineCount, warnings);
        }

        static void ApplyEpisode(StoryEpisode episode, string episodeId, Dictionary<string, string> row)
        {
            episode.Id = episodeId;
            if (Has(row, "episode_title", "title", "에피소드_제목", "제목"))
                episode.Title = Get(row, "episode_title", "title", "에피소드_제목", "제목");
            if (Has(row, "category", "분류"))
                episode.Category = ParseCategory(Get(row, "category", "분류"), episode.Category);
            if (TryInt(Get(row, "sort_order", "order", "정렬_순서"), out int order)) episode.SortOrder = order;
            if (Has(row, "summary", "줄거리", "요약")) episode.Summary = Get(row, "summary", "줄거리", "요약");
            if (Has(row, "focus_character", "중심_캐릭터"))
                episode.FocusCharacter = StoryAssetResolver.Character(Get(row, "focus_character", "중심_캐릭터"));
            if (Has(row, "thumbnail", "썸네일")) episode.Thumbnail = StoryAssetResolver.Asset<Sprite>(Get(row, "thumbnail", "썸네일"));
            if (Has(row, "banner", "배너")) episode.Banner = StoryAssetResolver.Asset<Sprite>(Get(row, "banner", "배너"));
            if (Has(row, "initially_unlocked", "기본_해금"))
                episode.IsInitiallyUnlocked = ParseBool(Get(row, "initially_unlocked", "기본_해금"), false);
            if (Has(row, "unlock_key", "해금_키")) episode.UnlockKey = Get(row, "unlock_key", "해금_키");
            if (Has(row, "prerequisite_episode", "선행_에피소드"))
                episode.PrerequisiteEpisodeId = Get(row, "prerequisite_episode", "선행_에피소드");
        }

        static int ApplyLines(StoryEpisode episode, IReadOnlyList<Dictionary<string, string>> rows,
            bool merge, ref int warnings)
        {
            var result = merge ? new List<StoryLine>(episode.Lines) : new List<StoryLine>();
            var importedLineIds = new HashSet<string>(StringComparer.Ordinal);
            int applied = 0;
            foreach (Dictionary<string, string> row in rows)
            {
                string lineId = Get(row, "line_id", "라인_id", "라인id").Trim();
                if (string.IsNullOrWhiteSpace(lineId)) lineId = $"line_{applied + 1:000}";
                if (!importedLineIds.Add(lineId))
                {
                    warnings++;
                    Debug.LogWarning($"[Story Import] {episode.Id}/{lineId}: 같은 라인 ID가 반복되어 마지막 행의 내용으로 갱신합니다.", episode);
                }
                StoryLine line = result.Find(item => item != null && item.Id == lineId);
                if (line == null)
                {
                    line = new StoryLine { Id = lineId };
                    result.Add(line);
                }
                ApplyLine(line, row);
                applied++;
                if (string.IsNullOrWhiteSpace(line.Text))
                {
                    warnings++;
                    Debug.LogWarning($"[Story Import] {episode.Id}/{lineId}: 대사가 비어 있습니다.", episode);
                }
            }
            episode.ReplaceLines(result);
            return applied;
        }

        static void ApplyLine(StoryLine line, Dictionary<string, string> row)
        {
            if (Has(row, "speaker", "화자_캐릭터")) line.Speaker = StoryAssetResolver.Character(Get(row, "speaker", "화자_캐릭터"));
            if (Has(row, "speaker_name", "화자명", "화자_이름")) line.SpeakerNameOverride = Get(row, "speaker_name", "화자명", "화자_이름");
            if (Has(row, "speaker_position", "화자_위치"))
                line.SpeakerPosition = ParseSpeakerPosition(Get(row, "speaker_position", "화자_위치"), line.SpeakerPosition);
            if (Has(row, "text", "대사", "본문")) line.Text = Get(row, "text", "대사", "본문");

            ApplyDisplay(line.Left, row, "left", "왼쪽");
            ApplyDisplay(line.Center, row, "center", "가운데");
            ApplyDisplay(line.Right, row, "right", "오른쪽");

            if (Has(row, "background", "배경")) line.Background = StoryAssetResolver.Asset<Sprite>(Get(row, "background", "배경"));
            if (Has(row, "cg")) line.Cg = StoryAssetResolver.Asset<Sprite>(Get(row, "cg"));
            if (Has(row, "bgm")) line.Bgm = StoryAssetResolver.Asset<AudioClip>(Get(row, "bgm"));
            if (Has(row, "sfx", "효과음")) line.Sfx = StoryAssetResolver.Asset<AudioClip>(Get(row, "sfx", "효과음"));
            if (Has(row, "voice", "음성", "voice_clip"))
                line.Voice = StoryAssetResolver.Asset<AudioClip>(Get(row, "voice", "음성", "voice_clip"));
            if (Has(row, "transition", "전환")) line.Transition = ParseTransition(Get(row, "transition", "전환"), line.Transition);
            if (Has(row, "effects", "화면_효과")) line.Effects = ParseEffects(Get(row, "effects", "화면_효과"));
            if (TryFloat(Get(row, "shake_strength", "흔들림_강도"), out float shake)) line.ShakeStrength = shake;
            if (TryFloat(Get(row, "effect_duration", "효과_시간"), out float effectDuration)) line.EffectDuration = effectDuration;
            if (TryFloat(Get(row, "text_speed", "텍스트_속도"), out float textSpeed)) line.TextSpeed = textSpeed;
            if (TryFloat(Get(row, "auto_duration", "자동_대기"), out float autoDuration)) line.AutoDuration = autoDuration;

            bool hasChoiceColumn = Enumerable.Range(1, 4).Any(index => Has(row, $"choice{index}_text", $"선택지{index}"));
            if (hasChoiceColumn)
            {
                var choices = new List<StoryChoice>();
                for (int i = 1; i <= 4; i++)
                {
                    string text = Get(row, $"choice{i}_text", $"선택지{i}");
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    choices.Add(new StoryChoice
                    {
                        Text = text,
                        NextEpisodeId = Get(row, $"choice{i}_next_episode", $"선택지{i}_다음_에피소드"),
                        NextLineId = Get(row, $"choice{i}_next_line", $"선택지{i}_다음_라인"),
                        ConditionKey = Get(row, $"choice{i}_condition", $"선택지{i}_조건")
                    });
                }
                line.ReplaceChoices(choices);
            }
        }

        static void ApplyDisplay(StoryCharacterDisplay display, Dictionary<string, string> row,
            string prefix, string koreanPrefix)
        {
            if (Has(row, prefix + "_character", koreanPrefix + "_캐릭터"))
                display.Character = StoryAssetResolver.Character(Get(row, prefix + "_character", koreanPrefix + "_캐릭터"));
            if (Has(row, prefix + "_expression_key", koreanPrefix + "_표정키"))
                display.ExpressionKey = Get(row, prefix + "_expression_key", koreanPrefix + "_표정키");
            if (Has(row, prefix + "_expression_sprite", koreanPrefix + "_표정"))
                display.ExpressionSprite = StoryAssetResolver.Asset<Sprite>(Get(row, prefix + "_expression_sprite", koreanPrefix + "_표정"));
            if (Has(row, prefix + "_visible", koreanPrefix + "_표시"))
                display.Visible = ParseBool(Get(row, prefix + "_visible", koreanPrefix + "_표시"), display.Character != null || display.ExpressionSprite != null);
            else if (display.Character != null || display.ExpressionSprite != null) display.Visible = true;
            if (Has(row, prefix + "_flip", koreanPrefix + "_반전"))
                display.FlipX = ParseBool(Get(row, prefix + "_flip", koreanPrefix + "_반전"), false);
            if (Has(row, prefix + "_tint", koreanPrefix + "_색상") &&
                ColorUtility.TryParseHtmlString(Get(row, prefix + "_tint", koreanPrefix + "_색상"), out Color tint))
                display.Tint = tint;

            Vector2 offset = display.Offset;
            if (TryFloat(Get(row, prefix + "_offset_x", koreanPrefix + "_오프셋_x"), out float offsetX)) offset.x = offsetX;
            if (TryFloat(Get(row, prefix + "_offset_y", koreanPrefix + "_오프셋_y"), out float offsetY)) offset.y = offsetY;
            display.Offset = offset;
        }

        static StoryEpisode FindEpisodeAsset(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:StoryEpisode"))
            {
                StoryEpisode candidate = AssetDatabase.LoadAssetAtPath<StoryEpisode>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate != null && string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            return null;
        }

        static StoryCategory ParseCategory(string value, StoryCategory fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "main": case "메인": case "메인스토리": return StoryCategory.Main;
                case "event": case "이벤트": case "이벤트스토리": return StoryCategory.Event;
                case "character": case "캐릭터": case "캐릭터스토리": case "인연": return StoryCategory.Character;
                case "side": case "사이드": case "사이드스토리": case "외전": return StoryCategory.Side;
                default: return Enum.TryParse(value, true, out StoryCategory parsed) ? parsed : fallback;
            }
        }

        static StorySpeakerPosition ParseSpeakerPosition(string value, StorySpeakerPosition fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "narrator": case "내레이션": return StorySpeakerPosition.Narrator;
                case "left": case "왼쪽": return StorySpeakerPosition.Left;
                case "center": case "가운데": case "중앙": return StorySpeakerPosition.Center;
                case "right": case "오른쪽": return StorySpeakerPosition.Right;
                default: return Enum.TryParse(value, true, out StorySpeakerPosition parsed) ? parsed : fallback;
            }
        }

        static StoryTransition ParseTransition(string value, StoryTransition fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "컷": return StoryTransition.Cut;
                case "크로스페이드": return StoryTransition.CrossFade;
                case "암전": return StoryTransition.FadeToBlack;
                case "백색전환": return StoryTransition.FadeToWhite;
                case "왼쪽슬라이드": return StoryTransition.SlideLeft;
                case "오른쪽슬라이드": return StoryTransition.SlideRight;
                default: return Enum.TryParse(value, true, out StoryTransition parsed) ? parsed : fallback;
            }
        }

        static StoryScreenEffect ParseEffects(string value)
        {
            StoryScreenEffect result = StoryScreenEffect.None;
            string[] tokens = (value ?? string.Empty).Split(new[] { '|', ',', '+', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string tokenValue in tokens)
            {
                string token = tokenValue.Trim();
                if (Enum.TryParse(token, true, out StoryScreenEffect parsed)) { result |= parsed; continue; }
                switch (token)
                {
                    case "흔들림": result |= StoryScreenEffect.Shake; break;
                    case "백색플래시": result |= StoryScreenEffect.FlashWhite; break;
                    case "검정플래시": result |= StoryScreenEffect.FlashBlack; break;
                    case "페이드인": result |= StoryScreenEffect.FadeIn; break;
                    case "페이드아웃": result |= StoryScreenEffect.FadeOut; break;
                    case "비네트": result |= StoryScreenEffect.Vignette; break;
                }
            }
            return result;
        }

        static bool TryInt(string value, out int result)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ||
                   int.TryParse(value, out result);
        }

        static bool TryFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                   float.TryParse(value, out result);
        }

        static bool ParseBool(string value, bool fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "1": case "true": case "yes": case "y": case "예": case "사용": return true;
                case "0": case "false": case "no": case "n": case "아니오": case "미사용": return false;
                default: return fallback;
            }
        }

        static string Get(Dictionary<string, string> row, params string[] names)
        {
            foreach (string name in names)
                if (row.TryGetValue(name, out string value)) return value ?? string.Empty;
            return string.Empty;
        }

        static bool Has(Dictionary<string, string> row, params string[] names)
        {
            return names.Any(row.ContainsKey);
        }

        static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "StoryEpisode" : value;
            foreach (char character in Path.GetInvalidFileNameChars()) result = result.Replace(character, '_');
            return result;
        }
    }

    internal static class StoryAssetResolver
    {
        static readonly Dictionary<string, UnityEngine.Object> Cache = new Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

        static StoryAssetResolver()
        {
            EditorApplication.projectChanged += Clear;
        }

        [MenuItem("Starfall/Story/Clear Asset Resolver Cache")]
        public static void Clear()
        {
            Cache.Clear();
        }

        public static CharacterData Character(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string key = "CharacterData|" + value.Trim();
            if (Cache.TryGetValue(key, out UnityEngine.Object cached))
            {
                CharacterData cachedCharacter = cached as CharacterData;
                if (cachedCharacter != null) return cachedCharacter;
                Cache.Remove(key);
            }

            CharacterData direct = Asset<CharacterData>(value);
            if (direct != null) { Cache[key] = direct; return direct; }
            foreach (string guid in AssetDatabase.FindAssets("t:CharacterData"))
            {
                CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
                if (character != null && (string.Equals(character.Id, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(character.DisplayName, value, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(character.name, value, StringComparison.OrdinalIgnoreCase)))
                {
                    Cache[key] = character;
                    return character;
                }
            }
            return null;
        }

        public static T Asset<T>(string value) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            string key = typeof(T).FullName + "|" + value;
            if (Cache.TryGetValue(key, out UnityEngine.Object cached))
            {
                T cachedAsset = cached as T;
                if (cachedAsset != null) return cachedAsset;
                Cache.Remove(key);
            }

            string path = value;
            if (value.Length == 32 && value.All(Uri.IsHexDigit))
            {
                string guidPath = AssetDatabase.GUIDToAssetPath(value);
                if (!string.IsNullOrWhiteSpace(guidPath)) path = guidPath;
            }

            T asset = null;
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    asset = AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault(item =>
                        string.Equals(item.name, Path.GetFileNameWithoutExtension(value), StringComparison.OrdinalIgnoreCase));
            }
            if (asset == null)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    asset = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<T>().FirstOrDefault(item =>
                        string.Equals(item.name, value, StringComparison.OrdinalIgnoreCase));
                    if (asset != null) break;
                    T main = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                    if (main != null && string.Equals(Path.GetFileNameWithoutExtension(assetPath), value,
                        StringComparison.OrdinalIgnoreCase)) { asset = main; break; }
                }
            }
            if (asset != null) Cache[key] = asset;
            return asset;
        }
    }
}
