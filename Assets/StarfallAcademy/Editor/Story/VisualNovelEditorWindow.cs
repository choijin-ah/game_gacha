using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class VisualNovelEditorWindow : EditorWindow
    {
        const float EpisodePanelWidth = 230f;
        const float LinePanelWidth = 270f;

        StoryDatabase database;
        StoryCategory selectedCategory;
        StoryEpisode selectedEpisode;
        int selectedLineIndex = -1;
        SerializedObject episodeObject;
        Vector2 episodeScroll;
        Vector2 lineScroll;
        Vector2 detailScroll;
        string episodeSearch = string.Empty;

        [MenuItem("Starfall/Story/Visual Novel Editor")]
        public static void Open()
        {
            var window = GetWindow<VisualNovelEditorWindow>("Visual Novel Editor");
            window.minSize = new Vector2(1050f, 650f);
            window.Show();
        }

        void OnEnable()
        {
            database = StoryDatabaseBootstrap.EnsureDatabase();
            SelectFirstVisibleEpisode();
        }

        void OnGUI()
        {
            DrawToolbar();
            if (database == null)
            {
                EditorGUILayout.HelpBox("StoryDatabase를 불러오지 못했습니다.", MessageType.Error);
                if (GUILayout.Button("데이터베이스 다시 만들기")) database = StoryDatabaseBootstrap.EnsureDatabase();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawEpisodePanel();
            DrawLinePanel();
            DrawDetailPanel();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("STARFALL VISUAL NOVEL", EditorStyles.boldLabel, GUILayout.Width(190f));
            StoryDatabase changed = (StoryDatabase)EditorGUILayout.ObjectField(database, typeof(StoryDatabase), false,
                GUILayout.Width(250f));
            if (changed != database)
            {
                database = changed != null ? changed : StoryDatabaseBootstrap.EnsureDatabase();
                SelectEpisode(null);
                SelectFirstVisibleEpisode();
            }
            if (GUILayout.Button("새 에피소드", EditorStyles.toolbarButton, GUILayout.Width(90f))) CreateEpisode();
            if (GUILayout.Button("Excel/CSV 가져오기", EditorStyles.toolbarButton, GUILayout.Width(125f)))
                StoryImportWindow.Open(database);
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(55f))) SaveAll();
            GUILayout.FlexibleSpace();
            GUILayout.Label("에피소드와 대사를 선택하면 오른쪽에서 즉시 미리볼 수 있습니다.", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawEpisodePanel()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(EpisodePanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("스토리 분류", EditorStyles.boldLabel);
            DrawCategoryButtons();
            EditorGUILayout.Space(4f);
            episodeSearch = EditorGUILayout.TextField(episodeSearch, EditorStyles.toolbarSearchField);

            episodeScroll = EditorGUILayout.BeginScrollView(episodeScroll, GUILayout.ExpandHeight(true));
            IReadOnlyList<StoryEpisode> episodes = database.GetEpisodes(selectedCategory);
            int visibleCount = 0;
            foreach (StoryEpisode episode in episodes)
            {
                if (episode == null || (!string.IsNullOrWhiteSpace(episodeSearch) &&
                    episode.Title.IndexOf(episodeSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                    episode.Id.IndexOf(episodeSearch, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;

                visibleCount++;
                Color oldColor = GUI.backgroundColor;
                if (episode == selectedEpisode) GUI.backgroundColor = new Color(.3f, .75f, 1f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.button);
                GUI.backgroundColor = oldColor;
                if (GUILayout.Button($"{episode.SortOrder:00}  {episode.Title}", EditorStyles.boldLabel))
                    SelectEpisode(episode);
                EditorGUILayout.LabelField(episode.Id + $"  ·  {episode.Lines.Count}줄", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            if (visibleCount == 0)
                EditorGUILayout.HelpBox("이 분류에 에피소드가 없습니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(selectedEpisode == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▲", GUILayout.Width(36f))) MoveEpisode(-1);
                if (GUILayout.Button("▼", GUILayout.Width(36f))) MoveEpisode(1);
                if (GUILayout.Button("등록 해제")) RemoveEpisode();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawCategoryButtons()
        {
            foreach (StoryCategory category in Enum.GetValues(typeof(StoryCategory)))
            {
                Color oldColor = GUI.backgroundColor;
                if (selectedCategory == category) GUI.backgroundColor = new Color(.32f, .8f, 1f);
                if (GUILayout.Button(CategoryLabel(category), GUILayout.Height(26f)))
                {
                    selectedCategory = category;
                    SelectFirstVisibleEpisode();
                }
                GUI.backgroundColor = oldColor;
            }
        }

        void DrawLinePanel()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LinePanelWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField(selectedEpisode != null ? selectedEpisode.Title : "대사 라인", EditorStyles.boldLabel);
            if (selectedEpisode == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 에피소드를 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            lineScroll = EditorGUILayout.BeginScrollView(lineScroll, GUILayout.ExpandHeight(true));
            for (int i = 0; i < selectedEpisode.Lines.Count; i++)
            {
                StoryLine line = selectedEpisode.Lines[i];
                if (line == null) continue;
                Color oldColor = GUI.backgroundColor;
                if (i == selectedLineIndex) GUI.backgroundColor = new Color(.38f, .82f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.button);
                GUI.backgroundColor = oldColor;
                if (GUILayout.Button($"{i + 1:000}  {line.Id}", EditorStyles.boldLabel)) selectedLineIndex = i;
                string speaker = string.IsNullOrWhiteSpace(line.SpeakerName) ? "내레이션" : line.SpeakerName;
                string preview = (line.Text ?? string.Empty).Replace('\n', ' ');
                if (preview.Length > 34) preview = preview.Substring(0, 34) + "…";
                EditorGUILayout.LabelField($"{speaker}  |  {preview}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ 추가")) InsertLine(selectedEpisode.Lines.Count);
            using (new EditorGUI.DisabledScope(selectedLineIndex < 0))
            {
                if (GUILayout.Button("앞에 삽입")) InsertLine(selectedLineIndex);
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(selectedLineIndex < 0))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("복제")) DuplicateLine();
                if (GUILayout.Button("삭제")) DeleteLine();
                if (GUILayout.Button("▲", GUILayout.Width(34f))) MoveLine(-1);
                if (GUILayout.Button("▼", GUILayout.Width(34f))) MoveLine(1);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (selectedEpisode == null)
            {
                EditorGUILayout.HelpBox("새 에피소드를 만들거나 기존 에피소드를 선택하세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EnsureSerializedEpisode();
            episodeObject.UpdateIfRequiredOrScript();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawEpisodeFields();

            SerializedProperty lines = episodeObject.FindProperty("lines");
            if (selectedLineIndex >= 0 && selectedLineIndex < lines.arraySize)
            {
                SerializedProperty line = lines.GetArrayElementAtIndex(selectedLineIndex);
                DrawLinePreview(selectedEpisode.Lines[selectedLineIndex]);
                DrawLineFields(line);
            }
            else
                EditorGUILayout.HelpBox("가운데에서 편집할 대사 라인을 선택하세요.", MessageType.Info);

            if (episodeObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(selectedEpisode);
                Repaint();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawEpisodeFields()
        {
            EditorGUILayout.LabelField("에피소드 설정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            DrawProperty("episodeId", "에피소드 ID");
            DrawProperty("title", "제목");
            DrawProperty("category", "분류");
            DrawProperty("sortOrder", "정렬 순서");
            DrawProperty("summary", "줄거리");
            DrawProperty("thumbnail", "썸네일");
            DrawProperty("banner", "배너");
            DrawProperty("focusCharacter", "중심 캐릭터");
            DrawProperty("initiallyUnlocked", "기본 해금");
            DrawProperty("unlockKey", "해금 키");
            DrawProperty("prerequisiteEpisodeId", "선행 에피소드 ID");
            EditorGUILayout.EndVertical();
        }

        void DrawLineFields(SerializedProperty line)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField($"라인 {selectedLineIndex + 1:000} 상세 편집", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            Property(line, "lineId", "라인 ID");
            Property(line, "speaker", "화자 캐릭터");
            Property(line, "speakerNameOverride", "화자명 덮어쓰기");
            Property(line, "speakerPosition", "화자 위치");
            Property(line, "text", "대사");
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("캐릭터 · 표정", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("left"), new GUIContent("왼쪽"), true);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("center"), new GUIContent("가운데"), true);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("right"), new GUIContent("오른쪽"), true);
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("배경 · 오디오", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            Property(line, "background", "배경");
            Property(line, "cg", "CG");
            Property(line, "bgm", "BGM");
            Property(line, "sfx", "효과음");
            Property(line, "voice", "음성");
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("연출 · 재생", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            Property(line, "transition", "전환");
            Property(line, "effects", "화면 효과");
            Property(line, "shakeStrength", "흔들림 강도");
            Property(line, "effectDuration", "효과 시간");
            Property(line, "textSpeed", "글자 출력 간격");
            Property(line, "autoDuration", "자동 진행 대기");
            EditorGUILayout.PropertyField(line.FindPropertyRelative("choices"), new GUIContent("선택지"), true);
            EditorGUILayout.EndVertical();
        }

        void DrawLinePreview(StoryLine line)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("선택 라인 미리보기", EditorStyles.boldLabel);
            Rect preview = GUILayoutUtility.GetRect(600f, 300f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(preview, new Color(.035f, .045f, .07f, 1f));

            if (line.Background != null) DrawSprite(preview, line.Background, ScaleMode.ScaleAndCrop);
            if (line.Cg != null) DrawSprite(preview, line.Cg, ScaleMode.ScaleAndCrop, new Color(1f, 1f, 1f, .88f));

            DrawCharacter(preview, line.Left, .2f);
            DrawCharacter(preview, line.Center, .5f);
            DrawCharacter(preview, line.Right, .8f);

            Rect box = new Rect(preview.x + 14f, preview.yMax - 104f, preview.width - 28f, 90f);
            EditorGUI.DrawRect(box, new Color(.015f, .02f, .035f, .9f));
            GUI.Box(box, GUIContent.none);
            string speaker = string.IsNullOrWhiteSpace(line.SpeakerName) ? "내레이션" : line.SpeakerName;
            GUI.Label(new Rect(box.x + 16f, box.y + 9f, box.width - 32f, 22f), speaker, EditorStyles.boldLabel);
            var textStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, fontSize = 13 };
            GUI.Label(new Rect(box.x + 16f, box.y + 32f, box.width - 32f, 48f), line.Text, textStyle);

            if (line.Effects != StoryScreenEffect.None)
                GUI.Label(new Rect(preview.x + 10f, preview.y + 8f, preview.width - 20f, 20f),
                    "FX  " + line.Effects, EditorStyles.miniBoldLabel);
        }

        static void DrawCharacter(Rect preview, StoryCharacterDisplay display, float centerX)
        {
            if (display == null || !display.Visible || display.ResolvedSprite == null) return;
            float height = preview.height * .86f;
            float width = height * .62f;
            Rect rect = new Rect(preview.x + preview.width * centerX - width * .5f + display.Offset.x,
                preview.yMax - height - 24f - display.Offset.y, width, height);
            DrawSprite(rect, display.ResolvedSprite, ScaleMode.ScaleToFit, display.Tint, display.FlipX);
            if (!string.IsNullOrWhiteSpace(display.ExpressionKey))
                GUI.Label(new Rect(rect.x, rect.y, rect.width, 18f), display.ExpressionKey, EditorStyles.centeredGreyMiniLabel);
        }

        static void DrawSprite(Rect target, Sprite sprite, ScaleMode scaleMode, Color? tint = null, bool flipX = false)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect textureRect = sprite.textureRect;
            Rect uv = new Rect(textureRect.x / sprite.texture.width, textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width, textureRect.height / sprite.texture.height);
            if (flipX) { uv.x += uv.width; uv.width = -uv.width; }
            Color previous = GUI.color;
            GUI.color = tint ?? Color.white;
            if (scaleMode == ScaleMode.ScaleToFit)
            {
                float ratio = textureRect.width / Mathf.Max(1f, textureRect.height);
                Rect fitted = target;
                if (target.width / target.height > ratio)
                {
                    fitted.width = target.height * ratio;
                    fitted.x += (target.width - fitted.width) * .5f;
                }
                else
                {
                    fitted.height = target.width / ratio;
                    fitted.y += (target.height - fitted.height) * .5f;
                }
                GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv, true);
            }
            else
                GUI.DrawTextureWithTexCoords(target, sprite.texture, uv, true);
            GUI.color = previous;
        }

        void CreateEpisode()
        {
            StoryDatabaseBootstrap.EnsureFolders();
            var episode = CreateInstance<StoryEpisode>();
            episode.name = "new_story_episode";
            episode.Id = "story_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            episode.Title = "새 에피소드";
            episode.Category = selectedCategory;
            episode.SortOrder = database.GetEpisodes(selectedCategory).Count;
            episode.AddLine(new StoryLine
            {
                Id = "line_001",
                Text = "새 대사를 입력하세요.",
                Transition = StoryTransition.CrossFade
            });

            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(StoryDatabaseBootstrap.EpisodeFolder, "StoryEpisode.asset").Replace('\\', '/'));
            AssetDatabase.CreateAsset(episode, path);
            Undo.RecordObject(database, "Create story episode");
            database.AddEpisode(episode);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SelectEpisode(episode);
            Selection.activeObject = episode;
            EditorGUIUtility.PingObject(episode);
        }

        void RemoveEpisode()
        {
            if (selectedEpisode == null) return;
            int result = EditorUtility.DisplayDialogComplex("에피소드 등록 해제", selectedEpisode.Title +
                "\n\n데이터베이스에서만 뺄까요? 에셋까지 삭제할 수도 있습니다.",
                "등록만 해제", "취소", "에셋도 삭제");
            if (result == 1) return;

            StoryEpisode removed = selectedEpisode;
            string assetPath = AssetDatabase.GetAssetPath(removed);
            Undo.RecordObject(database, "Remove story episode");
            database.RemoveEpisode(removed);
            EditorUtility.SetDirty(database);
            SelectEpisode(null);
            SelectFirstVisibleEpisode();
            if (result == 2 && !string.IsNullOrWhiteSpace(assetPath)) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.SaveAssets();
        }

        void MoveEpisode(int direction)
        {
            if (selectedEpisode == null) return;
            var categoryEpisodes = new List<StoryEpisode>(database.GetEpisodes(selectedCategory));
            int categoryIndex = categoryEpisodes.IndexOf(selectedEpisode);
            int targetCategoryIndex = categoryIndex + direction;
            if (categoryIndex < 0 || targetCategoryIndex < 0 || targetCategoryIndex >= categoryEpisodes.Count) return;

            StoryEpisode other = categoryEpisodes[targetCategoryIndex];
            Undo.RecordObjects(new UnityEngine.Object[] { selectedEpisode, other }, "Reorder story episode");
            int oldOrder = selectedEpisode.SortOrder;
            selectedEpisode.SortOrder = other.SortOrder;
            other.SortOrder = oldOrder;
            if (selectedEpisode.SortOrder == other.SortOrder)
            {
                selectedEpisode.SortOrder = targetCategoryIndex;
                other.SortOrder = categoryIndex;
            }
            EditorUtility.SetDirty(selectedEpisode);
            EditorUtility.SetDirty(other);
        }

        void InsertLine(int index)
        {
            if (selectedEpisode == null) return;
            Undo.RecordObject(selectedEpisode, "Insert story line");
            selectedEpisode.InsertLine(index, CreateDefaultLine(index + 1));
            selectedLineIndex = Mathf.Clamp(index, 0, selectedEpisode.Lines.Count - 1);
            EditorUtility.SetDirty(selectedEpisode);
            RebuildSerializedEpisode();
        }

        void DuplicateLine()
        {
            if (!HasSelectedLine()) return;
            Undo.RecordObject(selectedEpisode, "Duplicate story line");
            StoryLine copy = selectedEpisode.Lines[selectedLineIndex].DeepCopy();
            copy.Id = MakeUniqueLineId(copy.Id + "_copy");
            selectedEpisode.InsertLine(selectedLineIndex + 1, copy);
            selectedLineIndex++;
            EditorUtility.SetDirty(selectedEpisode);
            RebuildSerializedEpisode();
        }

        void DeleteLine()
        {
            if (!HasSelectedLine()) return;
            Undo.RecordObject(selectedEpisode, "Delete story line");
            selectedEpisode.RemoveLineAt(selectedLineIndex);
            selectedLineIndex = Mathf.Min(selectedLineIndex, selectedEpisode.Lines.Count - 1);
            EditorUtility.SetDirty(selectedEpisode);
            RebuildSerializedEpisode();
        }

        void MoveLine(int direction)
        {
            if (!HasSelectedLine()) return;
            int target = selectedLineIndex + direction;
            if (target < 0 || target >= selectedEpisode.Lines.Count) return;
            Undo.RecordObject(selectedEpisode, "Move story line");
            selectedEpisode.MoveLine(selectedLineIndex, target);
            selectedLineIndex = target;
            EditorUtility.SetDirty(selectedEpisode);
            RebuildSerializedEpisode();
        }

        StoryLine CreateDefaultLine(int preferredNumber)
        {
            return new StoryLine
            {
                Id = MakeUniqueLineId($"line_{Mathf.Max(1, preferredNumber):000}"),
                Text = "새 대사를 입력하세요.",
                Transition = StoryTransition.CrossFade
            };
        }

        string MakeUniqueLineId(string desired)
        {
            string candidate = string.IsNullOrWhiteSpace(desired) ? "line_001" : desired;
            int suffix = 2;
            while (selectedEpisode != null && selectedEpisode.FindLine(candidate) != null)
                candidate = desired + "_" + suffix++;
            return candidate;
        }

        bool HasSelectedLine()
        {
            return selectedEpisode != null && selectedLineIndex >= 0 && selectedLineIndex < selectedEpisode.Lines.Count;
        }

        void SelectEpisode(StoryEpisode episode)
        {
            selectedEpisode = episode;
            selectedLineIndex = episode != null && episode.Lines.Count > 0 ? 0 : -1;
            RebuildSerializedEpisode();
            Repaint();
        }

        void SelectFirstVisibleEpisode()
        {
            if (database == null) return;
            IReadOnlyList<StoryEpisode> episodes = database.GetEpisodes(selectedCategory);
            SelectEpisode(episodes.Count > 0 ? episodes[0] : null);
        }

        void EnsureSerializedEpisode()
        {
            if (episodeObject == null || episodeObject.targetObject != selectedEpisode)
                RebuildSerializedEpisode();
        }

        void RebuildSerializedEpisode()
        {
            episodeObject = selectedEpisode != null ? new SerializedObject(selectedEpisode) : null;
        }

        void DrawProperty(string propertyName, string label)
        {
            SerializedProperty property = episodeObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        static void Property(SerializedProperty parent, string name, string label)
        {
            SerializedProperty property = parent.FindPropertyRelative(name);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label), true);
        }

        static string CategoryLabel(StoryCategory category)
        {
            switch (category)
            {
                case StoryCategory.Main: return "메인 스토리";
                case StoryCategory.Event: return "이벤트 스토리";
                case StoryCategory.Character: return "캐릭터 스토리";
                default: return "사이드 스토리";
            }
        }

        void SaveAll()
        {
            if (episodeObject != null) episodeObject.ApplyModifiedProperties();
            if (selectedEpisode != null) EditorUtility.SetDirty(selectedEpisode);
            if (database != null) EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("스토리 데이터를 저장했습니다."));
        }
    }
}
