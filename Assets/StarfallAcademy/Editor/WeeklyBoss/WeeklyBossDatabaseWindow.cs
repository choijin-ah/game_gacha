using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class WeeklyBossDatabaseWindow : EditorWindow
    {
        WeeklyBossDatabase database;
        WeeklyBossDefinition selected;
        UnityEditor.Editor inspector;
        Vector2 listScroll;
        Vector2 inspectorScroll;
        string search = string.Empty;
        long previewDamage = 250000;
        int previewTurns = 20;
        int previewDefeatedAllies;
        bool previewKill;

        internal static void Open(WeeklyBossDatabase target)
        {
            var window = GetWindow<WeeklyBossDatabaseWindow>("Weekly Boss Database");
            window.minSize = new Vector2(1040, 650);
            window.database = target;
            window.Show();
        }

        void OnEnable()
        {
            database ??= WeeklyBossDatabaseBootstrap.EnsureDatabase();
            ContentValidationRegistry.Register(WeeklyBossValidation.ProviderId, WeeklyBossValidation.Collect);
        }

        void OnDisable()
        {
            if (inspector != null) DestroyImmediate(inspector);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL WEEKLY BOSS DATABASE", EditorStyles.boldLabel);
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("새 보스", EditorStyles.toolbarButton, GUILayout.Width(70))) CreateBoss();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(50))) DuplicateBoss();
                if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(50))) DeleteBoss();
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(-1);
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(1);
            }
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(50))) AssetDatabase.SaveAssets();
            if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(50)))
                ContentValidationWindow.Open(WeeklyBossValidation.ProviderId);
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(210));
            EditorGUILayout.EndHorizontal();
        }

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            EditorGUILayout.LabelField("보스 목록", EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            if (database != null)
            {
                for (int i = 0; i < database.Bosses.Count; i++)
                {
                    WeeklyBossDefinition boss = database.Bosses[i];
                    if (boss == null || !Matches(boss)) continue;
                    Color previous = GUI.backgroundColor;
                    if (boss == selected) GUI.backgroundColor = new Color(.55f, .75f, 1f);
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUI.backgroundColor = previous;
                    EditorGUILayout.LabelField(boss.Id + "  " + boss.DisplayName, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField((boss.BaseStage != null ? boss.BaseStage.DisplayName : "기반 스테이지 없음")
                        + "  ·  난이도 " + boss.Difficulties.Count, EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("편집")) Select(boss);
                    if (GUILayout.Button("Project", GUILayout.Width(70))) Ping(boss);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 보스를 선택하거나 새 보스를 만드세요.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUI.skin.box);
            if (inspector == null || inspector.target != selected)
            {
                if (inspector != null) DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(selected);
            }
            inspector.OnInspectorGUI();
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("기반 스테이지 열기") && selected.BaseStage != null) Ping(selected.BaseStage);
            if (GUILayout.Button("보상 점수 오름차순 정렬")) SortRewardTiers();
            EditorGUILayout.EndHorizontal();
            DrawScorePreview();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawScorePreview()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("점수 시뮬레이션", EditorStyles.boldLabel);
            previewDamage = EditorGUILayout.LongField("누적 피해", Math.Max(0, previewDamage));
            previewTurns = EditorGUILayout.IntField("ACTION", Math.Max(0, previewTurns));
            previewDefeatedAllies = EditorGUILayout.IntSlider("전투 불능 아군", previewDefeatedAllies, 0, 4);
            previewKill = EditorGUILayout.Toggle("보스 격파", previewKill);
            if (selected.Difficulties.Count == 0) return;
            var result = new BattleResult(BattleMode.WeeklyBoss,
                previewKill ? BattleEndReason.EnemiesDefeated : BattleEndReason.TurnLimit,
                previewKill ? BattleOutcome.Victory : BattleOutcome.Ongoing, true,
                previewTurns, previewDefeatedAllies, previewDamage);
            for (int i = 0; i < selected.Difficulties.Count; i++)
            {
                WeeklyBossDifficulty difficulty = selected.Difficulties[i];
                if (difficulty == null) continue;
                int value = WeeklyBossScoreCalculator.Calculate(result, difficulty);
                EditorGUILayout.LabelField(difficulty.DisplayName, value.ToString("N0"));
            }
        }

        bool Matches(WeeklyBossDefinition boss) => string.IsNullOrWhiteSpace(search)
            || boss.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
            || boss.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        void CreateBoss()
        {
            Directory.CreateDirectory(WeeklyBossDatabaseBootstrap.DataFolder);
            var boss = ScriptableObject.CreateInstance<WeeklyBossDefinition>();
            string id = NextBossId();
            SerializedObject serializedBoss = new SerializedObject(boss);
            serializedBoss.FindProperty("bossId").stringValue = id;
            serializedBoss.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                WeeklyBossDatabaseBootstrap.DataFolder + "/WeeklyBoss_" + id + ".asset");
            AssetDatabase.CreateAsset(boss, path);
            Undo.RegisterCreatedObjectUndo(boss, "Create Weekly Boss");
            AddReference(boss);
            Select(boss);
        }

        void DuplicateBoss()
        {
            if (selected == null) return;
            string source = AssetDatabase.GetAssetPath(selected);
            string target = AssetDatabase.GenerateUniqueAssetPath(
                WeeklyBossDatabaseBootstrap.DataFolder + "/" + selected.name + " Copy.asset");
            if (!AssetDatabase.CopyAsset(source, target)) return;
            WeeklyBossDefinition copy = AssetDatabase.LoadAssetAtPath<WeeklyBossDefinition>(target);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Weekly Boss");
            SerializedObject serializedCopy = new SerializedObject(copy);
            serializedCopy.FindProperty("bossId").stringValue = NextBossId();
            serializedCopy.ApplyModifiedProperties();
            AddReference(copy);
            Select(copy);
        }

        string NextBossId()
        {
            int number = 1;
            while (true)
            {
                string candidate = "WB_" + number.ToString("000");
                bool exists = false;
                for (int i = 0; i < database.Bosses.Count; i++)
                {
                    WeeklyBossDefinition boss = database.Bosses[i];
                    if (boss != null && string.Equals(boss.Id, candidate,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) return candidate;
                number++;
            }
        }

        void DeleteBoss()
        {
            if (selected == null || !EditorUtility.DisplayDialog("주간 보스 삭제",
                selected.DisplayName + " 에셋을 삭제할까요?", "삭제", "취소")) return;
            RemoveReference(selected);
            string path = AssetDatabase.GetAssetPath(selected);
            Select(null);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }

        void AddReference(WeeklyBossDefinition boss)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("bosses");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = boss;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        void RemoveReference(WeeklyBossDefinition boss)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("bosses");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == boss)
                { list.DeleteArrayElementAtIndex(i); break; }
            so.ApplyModifiedProperties();
        }

        void MoveSelected(int delta)
        {
            if (selected == null) return;
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("bosses");
            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == selected) { index = i; break; }
            int target = Mathf.Clamp(index + delta, 0, list.arraySize - 1);
            if (index >= 0 && target != index) list.MoveArrayElement(index, target);
            so.ApplyModifiedProperties();
        }

        void SortRewardTiers()
        {
            Undo.RecordObject(selected, "Sort Weekly Boss Rewards");
            SerializedObject so = new SerializedObject(selected);
            SerializedProperty tiers = so.FindProperty("scoreRewardTiers");
            for (int i = 0; i < tiers.arraySize - 1; i++)
                for (int j = i + 1; j < tiers.arraySize; j++)
                    if (tiers.GetArrayElementAtIndex(j).FindPropertyRelative("requiredScore").intValue
                        < tiers.GetArrayElementAtIndex(i).FindPropertyRelative("requiredScore").intValue)
                        tiers.MoveArrayElement(j, i);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
        }

        void Select(WeeklyBossDefinition boss)
        {
            selected = boss;
            if (inspector != null) { DestroyImmediate(inspector); inspector = null; }
            Repaint();
        }

        static void Ping(UnityEngine.Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }
    }
}
