using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class ChallengeTowerDatabaseWindow : EditorWindow
    {
        TowerDatabase database;
        TowerFloorData selected;
        UnityEditor.Editor inspector;
        Vector2 listScroll;
        Vector2 inspectorScroll;
        string search = string.Empty;
        int rangeStart = 1;
        int rangeEnd = 30;
        int powerStep = 500;
        TowerModifierType bulkModifier = TowerModifierType.EnemyMaxHp;
        float bulkModifierValue = .1f;

        internal static void Open(TowerDatabase target)
        {
            var window = GetWindow<ChallengeTowerDatabaseWindow>("Challenge Tower Database");
            window.minSize = new Vector2(1120, 680);
            window.database = target;
            window.Show();
        }

        void OnEnable()
        {
            database ??= TowerDatabaseBootstrap.EnsureDatabase();
            ContentValidationRegistry.Register(TowerValidation.ProviderId, TowerValidation.Collect);
        }

        void OnDisable()
        {
            if (inspector != null) DestroyImmediate(inspector);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL CHALLENGE TOWER DATABASE", EditorStyles.boldLabel);
            DrawToolbar();
            DrawBulkTools();
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("새 층", EditorStyles.toolbarButton, GUILayout.Width(55))) CreateFloor();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("복제", EditorStyles.toolbarButton, GUILayout.Width(45))) DuplicateFloor();
                if (GUILayout.Button("삭제", EditorStyles.toolbarButton, GUILayout.Width(45))) DeleteFloor();
                if (GUILayout.Button("이전 층 복사", EditorStyles.toolbarButton, GUILayout.Width(85))) CopyPrevious();
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(-1);
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveSelected(1);
            }
            if (GUILayout.Button("1~30층 생성", EditorStyles.toolbarButton, GUILayout.Width(85))) GenerateThirty();
            if (GUILayout.Button("재정렬", EditorStyles.toolbarButton, GUILayout.Width(50))) Renumber();
            if (GUILayout.Button("빈 층", EditorStyles.toolbarButton, GUILayout.Width(50))) FindEmpty();
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(45))) AssetDatabase.SaveAssets();
            if (GUILayout.Button("검증", EditorStyles.toolbarButton, GUILayout.Width(45)))
                ContentValidationWindow.Open(TowerValidation.ProviderId);
            GUILayout.FlexibleSpace();
            search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"), GUILayout.Width(190));
            EditorGUILayout.EndHorizontal();
        }

        void DrawBulkTools()
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            EditorGUILayout.LabelField("구간", GUILayout.Width(32));
            rangeStart = Mathf.Max(1, EditorGUILayout.IntField(rangeStart, GUILayout.Width(46)));
            EditorGUILayout.LabelField("~", GUILayout.Width(12));
            rangeEnd = Mathf.Max(rangeStart, EditorGUILayout.IntField(rangeEnd, GUILayout.Width(46)));
            powerStep = EditorGUILayout.IntField("층별 전투력 증가", powerStep, GUILayout.Width(190));
            if (GUILayout.Button("적용", GUILayout.Width(50))) ApplyPowerRamp();
            bulkModifier = (TowerModifierType)EditorGUILayout.EnumPopup(bulkModifier, GUILayout.Width(115));
            bulkModifierValue = EditorGUILayout.FloatField(bulkModifierValue, GUILayout.Width(55));
            if (GUILayout.Button("Modifier 추가", GUILayout.Width(100))) AddBulkModifier();
            if (GUILayout.Button("5층마다 보스", GUILayout.Width(100))) MarkBossFloors();
            EditorGUILayout.EndHorizontal();
        }

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(355));
            EditorGUILayout.LabelField("층 목록  " + (database?.Floors.Count ?? 0), EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            if (database != null)
            {
                for (int i = 0; i < database.Floors.Count; i++)
                {
                    TowerFloorData floor = database.Floors[i];
                    if (floor == null || !Matches(floor)) continue;
                    Color previous = GUI.backgroundColor;
                    if (floor == selected) GUI.backgroundColor = new Color(.55f, .75f, 1f);
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUI.backgroundColor = previous;
                    EditorGUILayout.LabelField(floor.FloorNumber.ToString("00") + "  "
                        + (floor.BaseStage != null ? floor.BaseStage.DisplayName : "EMPTY"), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("권장 " + floor.RecommendedPower.ToString("N0")
                        + "  ·  Modifier " + floor.Modifiers.Count, EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("편집")) Select(floor);
                    if (GUILayout.Button("Stage", GUILayout.Width(60)) && floor.BaseStage != null) Ping(floor.BaseStage);
                    if (GUILayout.Button("Project", GUILayout.Width(65))) Ping(floor);
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
                EditorGUILayout.HelpBox("왼쪽에서 층을 선택하거나 1~30층 기본 데이터를 생성하세요.", MessageType.Info);
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
            var modifier = new TowerModifierService(selected.Modifiers);
            EditorGUILayout.HelpBox("전투 규칙 미리보기\n" + modifier.Summary
                + "\n최초 보상: " + (selected.FirstClearReward?.Summary ?? "없음"), MessageType.Info);
            if (GUILayout.Button("Project 창에서 기반 스테이지 열기") && selected.BaseStage != null)
                Ping(selected.BaseStage);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        bool Matches(TowerFloorData floor) => string.IsNullOrWhiteSpace(search)
            || floor.FloorNumber.ToString().Contains(search)
            || floor.BaseStage != null && floor.BaseStage.DisplayName.IndexOf(search,
                StringComparison.OrdinalIgnoreCase) >= 0;

        void CreateFloor()
        {
            int number = 1;
            while (database.FindFloor(number) != null) number++;
            Select(CreateFloorAsset(number, FindFallbackStage(number)));
        }

        TowerFloorData CreateFloorAsset(int number, StageData stage)
        {
            Directory.CreateDirectory(TowerDatabaseBootstrap.DataFolder);
            var floor = ScriptableObject.CreateInstance<TowerFloorData>();
            SerializedObject so = new SerializedObject(floor);
            so.FindProperty("floorNumber").intValue = number;
            so.FindProperty("baseStage").objectReferenceValue = stage;
            so.FindProperty("recommendedPowerOverride").intValue = stage != null
                ? stage.RecommendedPower + Math.Max(0, number - 1) * 500 : number * 1000;
            SerializedProperty starConditions = so.FindProperty("starConditions");
            starConditions.arraySize = 3;
            starConditions.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.Clear;
            starConditions.GetArrayElementAtIndex(1).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.TurnLimit;
            starConditions.GetArrayElementAtIndex(1).FindPropertyRelative("threshold").intValue = 24 + number;
            starConditions.GetArrayElementAtIndex(2).FindPropertyRelative("type").enumValueIndex =
                (int)TowerStarConditionType.MaximumDefeatedAllies;
            starConditions.GetArrayElementAtIndex(2).FindPropertyRelative("threshold").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(TowerDatabaseBootstrap.DataFolder
                + "/TowerFloor_" + number.ToString("00") + ".asset");
            AssetDatabase.CreateAsset(floor, path);
            Undo.RegisterCreatedObjectUndo(floor, "Create Tower Floor");
            AddReference(floor);
            return floor;
        }

        void DuplicateFloor()
        {
            if (selected == null) return;
            int number = 1;
            while (database.FindFloor(number) != null) number++;
            string target = AssetDatabase.GenerateUniqueAssetPath(TowerDatabaseBootstrap.DataFolder
                + "/TowerFloor_" + number.ToString("00") + ".asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(selected), target)) return;
            TowerFloorData copy = AssetDatabase.LoadAssetAtPath<TowerFloorData>(target);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate Tower Floor");
            SerializedObject so = new SerializedObject(copy);
            so.FindProperty("floorNumber").intValue = number;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddReference(copy);
            Select(copy);
        }

        void DeleteFloor()
        {
            if (selected == null || !EditorUtility.DisplayDialog("탑 층 삭제",
                selected.FloorNumber + "층 에셋을 삭제할까요?", "삭제", "취소")) return;
            RemoveReference(selected);
            string path = AssetDatabase.GetAssetPath(selected);
            Select(null);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }

        void GenerateThirty()
        {
            for (int number = 1; number <= 30; number++)
                if (database.FindFloor(number) == null)
                    CreateFloorAsset(number, FindFallbackStage(number));
            AssetDatabase.SaveAssets();
        }

        StageData FindFallbackStage(int floorNumber)
        {
            string[] guids = AssetDatabase.FindAssets("t:StageData");
            var stages = new List<StageData>();
            for (int i = 0; i < guids.Length; i++)
            {
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (stage != null) stages.Add(stage);
            }
            if (stages.Count == 0) return null;
            if (floorNumber % 5 == 0)
                for (int i = 0; i < stages.Count; i++) if (stages[i].BossStage) return stages[i];
            return stages[(floorNumber - 1) % stages.Count];
        }

        void CopyPrevious()
        {
            TowerFloorData previous = database.FindFloor(selected.FloorNumber - 1);
            if (previous == null) return;
            int number = selected.FloorNumber;
            Undo.RecordObject(selected, "Copy Previous Tower Floor");
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(previous), selected);
            SerializedObject so = new SerializedObject(selected);
            so.FindProperty("floorNumber").intValue = number;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
        }

        void ApplyPowerRamp()
        {
            for (int floorNumber = rangeStart; floorNumber <= rangeEnd; floorNumber++)
            {
                TowerFloorData floor = database.FindFloor(floorNumber);
                if (floor == null) continue;
                Undo.RecordObject(floor, "Apply Tower Power Ramp");
                SerializedObject so = new SerializedObject(floor);
                int baseline = floor.BaseStage != null ? floor.BaseStage.RecommendedPower : 0;
                so.FindProperty("recommendedPowerOverride").intValue = Math.Max(0,
                    baseline + (floorNumber - rangeStart) * powerStep);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(floor);
            }
        }

        void AddBulkModifier()
        {
            for (int floorNumber = rangeStart; floorNumber <= rangeEnd; floorNumber++)
            {
                TowerFloorData floor = database.FindFloor(floorNumber);
                if (floor == null) continue;
                Undo.RecordObject(floor, "Apply Tower Modifier");
                SerializedObject so = new SerializedObject(floor);
                SerializedProperty modifiers = so.FindProperty("modifiers");
                SerializedProperty modifier = null;
                for (int i = 0; i < modifiers.arraySize; i++)
                {
                    SerializedProperty candidate = modifiers.GetArrayElementAtIndex(i);
                    if (candidate.FindPropertyRelative("type").enumValueIndex == (int)bulkModifier)
                    {
                        modifier = candidate;
                        break;
                    }
                }
                if (modifier == null)
                {
                    modifiers.arraySize++;
                    modifier = modifiers.GetArrayElementAtIndex(modifiers.arraySize - 1);
                }
                modifier.FindPropertyRelative("modifierId").stringValue = bulkModifier.ToString().ToLowerInvariant();
                modifier.FindPropertyRelative("type").enumValueIndex = (int)bulkModifier;
                modifier.FindPropertyRelative("value").floatValue = bulkModifierValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(floor);
            }
        }

        void MarkBossFloors()
        {
            StageData bossStage = null;
            string[] guids = AssetDatabase.FindAssets("t:StageData");
            for (int i = 0; i < guids.Length; i++)
            {
                StageData candidate = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (candidate != null && candidate.BossStage) { bossStage = candidate; break; }
            }
            if (bossStage == null) return;
            for (int i = 0; i < database.Floors.Count; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null || floor.FloorNumber % 5 != 0) continue;
                Undo.RecordObject(floor, "Mark Tower Boss Floor");
                SerializedObject so = new SerializedObject(floor);
                so.FindProperty("baseStage").objectReferenceValue = bossStage;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(floor);
            }
        }

        void Renumber()
        {
            for (int i = 0; i < database.Floors.Count; i++)
            {
                TowerFloorData floor = database.Floors[i];
                if (floor == null) continue;
                Undo.RecordObject(floor, "Renumber Tower Floors");
                SerializedObject so = new SerializedObject(floor);
                so.FindProperty("floorNumber").intValue = i + 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(floor);
            }
        }

        void FindEmpty()
        {
            for (int i = 0; i < database.Floors.Count; i++)
                if (database.Floors[i] == null || database.Floors[i].BaseStage == null)
                { Select(database.Floors[i]); return; }
        }

        void AddReference(TowerFloorData floor)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("floors");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = floor;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        void RemoveReference(TowerFloorData floor)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("floors");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == floor)
                { list.DeleteArrayElementAtIndex(i); break; }
            so.ApplyModifiedProperties();
        }

        void MoveSelected(int delta)
        {
            SerializedObject so = new SerializedObject(database);
            SerializedProperty list = so.FindProperty("floors");
            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == selected) { index = i; break; }
            int target = Mathf.Clamp(index + delta, 0, list.arraySize - 1);
            if (index >= 0 && target != index) list.MoveArrayElement(index, target);
            so.ApplyModifiedProperties();
        }

        void Select(TowerFloorData floor)
        {
            selected = floor;
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
