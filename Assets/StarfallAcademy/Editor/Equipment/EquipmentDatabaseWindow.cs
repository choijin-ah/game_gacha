using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class EquipmentDatabaseWindow : EditorWindow
    {
        enum Tab { Equipment, Sets, DropTables }

        EquipmentDatabase database;
        SerializedObject databaseObject;
        UnityEngine.Object selected;
        UnityEditor.Editor inspector;
        Tab tab;
        string search = string.Empty;
        EquipmentSlot slotFilter;
        bool filterBySlot;
        Vector2 listScroll;
        Vector2 inspectorScroll;
        int previewLevel = 1;
        int simulationSeed = 12345;

        [MenuItem("Starfall/Data/Equipment Database")]
        public static void Open()
        {
            var window = GetWindow<EquipmentDatabaseWindow>("Equipment Database");
            window.minSize = new Vector2(980, 620);
            window.Show();
        }

        void OnEnable() => Reload();

        void OnDisable()
        {
            if (inspector != null) DestroyImmediate(inspector);
        }

        void Reload()
        {
            database = EquipmentDatabaseBootstrap.LoadOrCreate();
            databaseObject = database != null ? new SerializedObject(database) : null;
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL EQUIPMENT DATABASE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("장비 정의, 세트 효과와 드롭 테이블을 한 곳에서 관리합니다.",
                EditorStyles.miniLabel);
            DrawToolbar();
            tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "장비", "세트", "드롭 테이블" });
            DrawFilters();
            if (database == null)
            {
                EditorGUILayout.HelpBox("EquipmentDatabase를 만들 수 없습니다.", MessageType.Error);
                return;
            }
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ New", EditorStyles.toolbarButton, GUILayout.Width(65))) CreateAsset();
            using (new EditorGUI.DisabledScope(selected == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70))) Duplicate();
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(55))) Delete();
                if (GUILayout.Button("▲", EditorStyles.toolbarButton, GUILayout.Width(28))) Move(-1);
                if (GUILayout.Button("▼", EditorStyles.toolbarButton, GUILayout.Width(28))) Move(1);
            }
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(45)))
            {
                if (database != null) EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(62)))
                ContentValidationWindow.Open("Equipment");
            GUILayout.FlexibleSpace();
            if (database != null && GUILayout.Button("Ping DB", EditorStyles.toolbarButton, GUILayout.Width(58)))
                Ping(database);
            EditorGUILayout.EndHorizontal();
        }

        void DrawFilters()
        {
            EditorGUILayout.BeginHorizontal();
            search = EditorGUILayout.TextField("Search", search ?? string.Empty);
            if (tab == Tab.Equipment)
            {
                filterBySlot = EditorGUILayout.ToggleLeft("Slot filter", filterBySlot, GUILayout.Width(80));
                using (new EditorGUI.DisabledScope(!filterBySlot))
                    slotFilter = (EquipmentSlot)EditorGUILayout.EnumPopup(slotFilter, GUILayout.Width(130));
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            SerializedProperty list = CurrentList();
            EditorGUILayout.LabelField(TabTitle() + "  " + (list?.arraySize ?? 0), EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            if (list != null)
            {
                databaseObject.Update();
                for (int i = 0; i < list.arraySize; i++)
                {
                    UnityEngine.Object value = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (!Matches(value)) continue;
                    Color previous = GUI.backgroundColor;
                    if (value == selected) GUI.backgroundColor = new Color(.45f, .85f, 1f);
                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    GUI.backgroundColor = previous;
                    EditorGUILayout.LabelField(ListTitle(value), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(ListSubtitle(value), EditorStyles.miniLabel);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Edit", GUILayout.Width(55))) Select(value);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Ping", GUILayout.Width(45))) Ping(value);
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
            EditorGUILayout.LabelField("Details & Preview", EditorStyles.boldLabel);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUI.skin.box);
            if (selected == null)
            {
                EditorGUILayout.HelpBox("왼쪽 목록에서 항목을 선택하세요.", MessageType.Info);
            }
            else
            {
                if (inspector == null || inspector.target != selected)
                {
                    if (inspector != null) DestroyImmediate(inspector);
                    inspector = UnityEditor.Editor.CreateEditor(selected);
                }
                inspector.OnInspectorGUI();
                EditorGUILayout.Space(8);
                DrawPreview();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPreview()
        {
            if (selected is EquipmentDefinition definition)
            {
                previewLevel = EditorGUILayout.IntSlider("Preview level", previewLevel, 1,
                    definition.MaximumLevel);
                EditorGUILayout.HelpBox("Main stat: " + definition.MainStat + " "
                    + definition.GetValueAtLevel(previewLevel).ToString("0.##") + "\nCombat power: "
                    + definition.EstimateCombatPower(previewLevel).ToString("N0") + "\nNext cost: "
                    + definition.GetEnhancementCost(previewLevel).ToString("N0"), MessageType.Info);
            }
            else if (selected is EquipmentSetDefinition set)
            {
                EditorGUILayout.HelpBox("2-piece: " + EffectSummary(set.TwoPieceEffect)
                    + "\n4-piece: " + EffectSummary(set.FourPieceEffect), MessageType.Info);
            }
            else if (selected is EquipmentDropTable table)
            {
                simulationSeed = EditorGUILayout.IntField("Simulation seed", simulationSeed);
                EditorGUILayout.LabelField("Total weight", table.TotalWeight.ToString("0.###"));
                if (GUILayout.Button("Simulate 10,000 drops")) Simulate(table);
            }
        }

        void Simulate(EquipmentDropTable table)
        {
            var random = new System.Random(simulationSeed);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < 10000; i++)
            {
                EquipmentDefinition result = table.Roll(random);
                string id = result != null ? result.Id : "<none>";
                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
            }
            string summary = string.Join("\n", counts.OrderByDescending(pair => pair.Value)
                .Take(12).Select(pair => pair.Key + ": " + (pair.Value / 100f).ToString("0.00") + "%"));
            EditorUtility.DisplayDialog("Drop simulation", summary, "OK");
        }

        void CreateAsset()
        {
            Type type;
            string folder;
            string prefix;
            switch (tab)
            {
                case Tab.Sets:
                    type = typeof(EquipmentSetDefinition); folder = EquipmentDatabaseBootstrap.SetFolder;
                    prefix = "SET_"; break;
                case Tab.DropTables:
                    type = typeof(EquipmentDropTable); folder = EquipmentDatabaseBootstrap.DropTableFolder;
                    prefix = "DROP_"; break;
                default:
                    type = typeof(EquipmentDefinition); folder = EquipmentDatabaseBootstrap.EquipmentFolder;
                    prefix = "EQ_"; break;
            }
            Directory.CreateDirectory(folder);
            ScriptableObject value = CreateInstance(type);
            value.name = tab == Tab.Equipment ? "New Equipment" : tab == Tab.Sets ? "New Set" : "New Drop Table";
            SerializedObject serialized = new SerializedObject(value);
            SerializedProperty id = serialized.FindProperty(tab == Tab.Equipment ? "equipmentId"
                : tab == Tab.Sets ? "setId" : "dropTableId");
            if (id != null) id.stringValue = prefix + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + value.name + ".asset");
            AssetDatabase.CreateAsset(value, path);
            Undo.RecordObject(database, "Add equipment content");
            AddToDatabase(value);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Select(value);
            Ping(value);
        }

        void Duplicate()
        {
            if (!(selected is ScriptableObject source)) return;
            ScriptableObject copy = Instantiate(source);
            copy.name = source.name + " Copy";
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                Path.GetDirectoryName(sourcePath)?.Replace('\\', '/') + "/" + copy.name + ".asset");
            AssetDatabase.CreateAsset(copy, path);
            SerializedObject serialized = new SerializedObject(copy);
            SerializedProperty id = serialized.FindProperty(tab == Tab.Equipment ? "equipmentId"
                : tab == Tab.Sets ? "setId" : "dropTableId");
            if (id != null) id.stringValue += "_copy";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Undo.RecordObject(database, "Duplicate equipment content");
            AddToDatabase(copy);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Select(copy);
        }

        void Delete()
        {
            if (selected == null || !EditorUtility.DisplayDialog("Delete content",
                "Delete '" + selected.name + "'? This removes the asset.", "Delete", "Cancel")) return;
            UnityEngine.Object deleting = selected;
            Select(null);
            Undo.RecordObject(database, "Delete equipment content");
            database.Remove(deleting);
            EditorUtility.SetDirty(database);
            Undo.DestroyObjectImmediate(deleting);
            AssetDatabase.SaveAssets();
        }

        void Move(int direction)
        {
            SerializedProperty list = CurrentList();
            if (list == null || selected == null) return;
            databaseObject.Update();
            int index = -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == selected) { index = i; break; }
            int next = Mathf.Clamp(index + direction, 0, list.arraySize - 1);
            if (index < 0 || index == next) return;
            Undo.RecordObject(database, "Reorder equipment content");
            list.MoveArrayElement(index, next);
            databaseObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(database);
        }

        SerializedProperty CurrentList()
        {
            if (databaseObject == null) return null;
            return databaseObject.FindProperty(tab == Tab.Equipment ? "equipment"
                : tab == Tab.Sets ? "sets" : "dropTables");
        }

        void AddToDatabase(UnityEngine.Object value)
        {
            if (value is EquipmentDefinition definition) database.Add(definition);
            else if (value is EquipmentSetDefinition set) database.Add(set);
            else if (value is EquipmentDropTable table) database.Add(table);
        }

        bool Matches(UnityEngine.Object value)
        {
            if (value == null) return false;
            if (!string.IsNullOrWhiteSpace(search) && ListTitle(value).IndexOf(search.Trim(),
                StringComparison.OrdinalIgnoreCase) < 0 && ListSubtitle(value).IndexOf(search.Trim(),
                StringComparison.OrdinalIgnoreCase) < 0) return false;
            return !filterBySlot || tab != Tab.Equipment
                || value is EquipmentDefinition definition && definition.Slot == slotFilter;
        }

        string TabTitle() => tab == Tab.Equipment ? "Equipment" : tab == Tab.Sets ? "Sets" : "Drop tables";
        static string ListTitle(UnityEngine.Object value) => value is EquipmentDefinition equipment
            ? equipment.DisplayName + "  [" + equipment.Id + "]"
            : value is EquipmentSetDefinition set ? set.DisplayName + "  [" + set.Id + "]"
            : value is EquipmentDropTable table ? table.name + "  [" + table.Id + "]" : value?.name ?? "Missing";
        static string ListSubtitle(UnityEngine.Object value) => value is EquipmentDefinition equipment
            ? equipment.Slot + " · " + equipment.Rarity + " · " + (equipment.Set != null ? equipment.Set.DisplayName : "No set")
            : value is EquipmentSetDefinition ? "2-piece / 4-piece effects"
            : value is EquipmentDropTable table ? table.Candidates.Count + " candidates · weight " + table.TotalWeight.ToString("0.##") : string.Empty;
        static string EffectSummary(EquipmentSetEffect effect) => effect == null ? "None"
            : effect.Stat + " " + effect.Value.ToString("0.##") + (effect.Percentage ? "%" : string.Empty)
                + (string.IsNullOrWhiteSpace(effect.Description) ? string.Empty : " · " + effect.Description);

        void Select(UnityEngine.Object value)
        {
            selected = value;
            previewLevel = 1;
            if (inspector != null) { DestroyImmediate(inspector); inspector = null; }
            Repaint();
        }

        static void Ping(UnityEngine.Object value)
        {
            if (value == null) return;
            Selection.activeObject = value;
            EditorGUIUtility.PingObject(value);
        }
    }
}
