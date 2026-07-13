using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    static class CharacterDatabaseBootstrap
    {
        internal const string DatabasePath = "Assets/StarfallAcademy/Resources/Data/CharacterDatabase.asset";
        internal const string CharacterFolder = "Assets/StarfallAcademy/Data/Characters";

        static CharacterDatabaseBootstrap()
        {
            EditorApplication.delayCall += () => LoadOrCreate();
        }

        internal static CharacterDatabase LoadOrCreate()
        {
            CharacterDatabase database = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(DatabasePath);
            if (database != null) return database;

            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            Directory.CreateDirectory(CharacterFolder);
            AssetDatabase.Refresh();
            database = ScriptableObject.CreateInstance<CharacterDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }
    }

    public sealed class CharacterDatabaseWindow : EditorWindow
    {
        CharacterDatabase database;
        CharacterData selectedCharacter;
        CharacterData pendingCharacter;
        UnityEditor.Editor characterInspector;
        Vector2 listScroll;
        Vector2 inspectorScroll;

        [MenuItem("Starfall/Data/Character Database")]
        public static void Open()
        {
            var window = GetWindow<CharacterDatabaseWindow>("Character Database");
            window.minSize = new Vector2(860, 560);
            window.Show();
        }

        void OnEnable()
        {
            database = CharacterDatabaseBootstrap.LoadOrCreate();
        }

        void OnDisable()
        {
            if (characterInspector != null)
                DestroyImmediate(characterInspector);
        }

        void OnGUI()
        {
            DrawHeader();
            if (database == null)
            {
                EditorGUILayout.HelpBox("캐릭터 데이터베이스를 만들 수 없습니다.", MessageType.Error);
                if (GUILayout.Button("다시 만들기")) database = CharacterDatabaseBootstrap.LoadOrCreate();
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            DrawCharacterList();
            DrawInspector();
            EditorGUILayout.EndHorizontal();
        }

        void DrawHeader()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL CHARACTER TOOL", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("캐릭터 에셋을 만들고 편성 데이터베이스에 등록합니다.", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("＋ 새 캐릭터", EditorStyles.toolbarButton, GUILayout.Width(110)))
                CreateCharacter();
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
            GUILayout.FlexibleSpace();
            pendingCharacter = (CharacterData)EditorGUILayout.ObjectField(pendingCharacter, typeof(CharacterData), false,
                GUILayout.Width(190));
            using (new EditorGUI.DisabledScope(pendingCharacter == null))
            {
                if (GUILayout.Button("기존 에셋 등록", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    Undo.RecordObject(database, "Add character");
                    database.Add(pendingCharacter);
                    EditorUtility.SetDirty(database);
                    SelectCharacter(pendingCharacter);
                    pendingCharacter = null;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawCharacterList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(310));
            EditorGUILayout.LabelField("등록 캐릭터  " + database.Characters.Count, EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            if (database.Characters.Count == 0)
            {
                EditorGUILayout.Space(20);
                EditorGUILayout.HelpBox("등록된 캐릭터가 없습니다.\n상단의 '새 캐릭터'를 눌러 시작하세요.", MessageType.Info);
            }

            for (int i = 0; i < database.Characters.Count; i++)
            {
                CharacterData character = database.Characters[i];
                if (character == null) continue;
                Color previous = GUI.backgroundColor;
                if (character == selectedCharacter) GUI.backgroundColor = new Color(.45f, .85f, 1f);
                EditorGUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(64));
                GUI.backgroundColor = previous;
                Rect portraitRect = GUILayoutUtility.GetRect(52, 52, GUILayout.Width(52), GUILayout.Height(52));
                if (character.Portrait != null)
                    GUI.DrawTexture(portraitRect, character.Portrait.texture, ScaleMode.ScaleToFit, true);
                else
                    EditorGUI.DrawRect(portraitRect, new Color(.12f, .18f, .28f));

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField(character.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(character.Role + "  ·  ★" + character.Rarity + "  ·  Lv." + character.Level,
                    EditorStyles.miniLabel);
                if (GUILayout.Button("편집", GUILayout.Width(54))) SelectCharacter(character);
                EditorGUILayout.EndVertical();

                if (GUILayout.Button("×", GUILayout.Width(24), GUILayout.Height(24)))
                {
                    Undo.RecordObject(database, "Remove character");
                    database.Remove(character);
                    EditorUtility.SetDirty(database);
                    if (selectedCharacter == character) SelectCharacter(null);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("캐릭터 상세", EditorStyles.boldLabel);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUI.skin.box);
            if (selectedCharacter == null)
            {
                EditorGUILayout.Space(30);
                EditorGUILayout.HelpBox(
                    "왼쪽에서 캐릭터를 선택하세요.\n\nPortrait에 Sprite를 넣지 않아도 편성창에서는 이름 첫 글자로 표시됩니다.",
                    MessageType.Info);
            }
            else
            {
                if (characterInspector == null || characterInspector.target != selectedCharacter)
                {
                    if (characterInspector != null) DestroyImmediate(characterInspector);
                    characterInspector = UnityEditor.Editor.CreateEditor(selectedCharacter);
                }
                characterInspector.OnInspectorGUI();
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Skill 영역에서 캐릭터별 스킬 이름, Skill Icon, 최대 레벨과 강화 비용을 수정할 수 있습니다. " +
                    "Skill Icon을 비우면 Default Skill Icon 스타일이 자동 적용됩니다.",
                    MessageType.Info);
                EditorGUILayout.Space(8);
                if (GUILayout.Button("Project 창에서 찾기", GUILayout.Height(28)))
                {
                    Selection.activeObject = selectedCharacter;
                    EditorGUIUtility.PingObject(selectedCharacter);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void CreateCharacter()
        {
            Directory.CreateDirectory(CharacterDatabaseBootstrap.CharacterFolder);
            var character = ScriptableObject.CreateInstance<CharacterData>();
            character.name = "New Character";
            var serialized = new SerializedObject(character);
            serialized.FindProperty("characterId").stringValue = Guid.NewGuid().ToString("N");
            serialized.FindProperty("displayName").stringValue = "새 캐릭터";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            string path = AssetDatabase.GenerateUniqueAssetPath(
                CharacterDatabaseBootstrap.CharacterFolder + "/Character.asset");
            AssetDatabase.CreateAsset(character, path);
            Undo.RecordObject(database, "Create character");
            database.Add(character);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            SelectCharacter(character);
            Selection.activeObject = character;
            EditorGUIUtility.PingObject(character);
        }

        void SelectCharacter(CharacterData character)
        {
            selectedCharacter = character;
            if (characterInspector != null)
            {
                DestroyImmediate(characterInspector);
                characterInspector = null;
            }
            Repaint();
        }
    }
}
