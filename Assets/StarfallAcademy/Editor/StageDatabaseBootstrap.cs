using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class StageDatabaseBootstrap
    {
        const string DatabasePath = "Assets/StarfallAcademy/Resources/Data/StageDatabase.asset";
        const string StageFolder = "Assets/StarfallAcademy/Data/Stages";

        static StageDatabaseBootstrap()
        {
            EditorApplication.delayCall += EnsureOnLoad;
        }

        static void EnsureOnLoad() => EnsureDefaults();

        [MenuItem("Starfall/Data/Stage Database")]
        public static void Open()
        {
            StageDatabase database = EnsureDefaults();
            StageDatabaseWindow.Open(database);
        }

        internal static StageDatabase EnsureDefaults()
        {
            StageDatabase database = AssetDatabase.LoadAssetAtPath<StageDatabase>(DatabasePath);
            if (database == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
                database = ScriptableObject.CreateInstance<StageDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            Directory.CreateDirectory(StageFolder);
            string[] names =
            {
                "안개 낀 진입로", "폐쇄된 지하역", "붉은 시계탑", "검은 성당", "월식의 관측소",
                "균열의 교차로", "포식자의 흔적", "기억의 회랑", "폐기 장비 보관소", "심연의 관측자"
            };
            string[] ids =
            {
                "stage_01", "stage_02", "stage_03", "stage_04", "stage_05",
                "STG_MAIN_03_01", "STG_MAIN_03_02", "STG_GROWTH_EXP_01",
                "STG_EQUIP_01", "STG_MAIN_03_03"
            };
            string[] enemies =
            {
                "그림자 잔재", "도시 망령", "시계 인형", "검은 사제", "월식의 파수꾼",
                "균열 드론", "균열 포식자", "기억의 수호상", "보관소 방어 장치", "심연의 관측자"
            };
            int[] counts = { 2, 3, 4, 4, 1, 5, 2, 3, 4, 1 };
            for (int i = 0; i < names.Length; i++)
            {
                string path = StageFolder + "/Stage_" + (i + 1).ToString("00") + ".asset";
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null)
                {
                    stage = ScriptableObject.CreateInstance<StageData>();
                    stage.name = "Stage " + (i + 1).ToString("00");
                    SerializedObject serialized = new SerializedObject(stage);
                    serialized.FindProperty("stageId").stringValue = ids[i];
                    serialized.FindProperty("chapter").stringValue = i < 3 ? "CHAPTER 1"
                        : i < 5 ? "CHAPTER 2" : i == 7 ? "GROWTH DUNGEON"
                        : i == 8 ? "EQUIPMENT DUNGEON" : "CHAPTER 3";
                    serialized.FindProperty("displayName").stringValue = names[i];
                    serialized.FindProperty("description").stringValue =
                        "도시의 이상 현상을 조사하고 " + enemies[i] + "을(를) 제압하세요.";
                    serialized.FindProperty("enemyName").stringValue = enemies[i];
                    serialized.FindProperty("enemyCount").intValue = counts[i];
                    serialized.FindProperty("enemyLevel").intValue = 5 + i * 5;
                    serialized.FindProperty("enemyMaxHp").intValue = 1000 + i * 700;
                    serialized.FindProperty("enemyAttack").intValue = 105 + i * 55;
                    serialized.FindProperty("enemySpeed").intValue = 48 + i * 4;
                    serialized.FindProperty("bossStage").boolValue = i == 4 || i == 9;
                    serialized.FindProperty("category").enumValueIndex = i == 7 ? (int)StageCategory.Growth
                        : i == 8 ? (int)StageCategory.Equipment : (int)StageCategory.Main;
                    serialized.FindProperty("recommendedPower").intValue = 2500 + i * 2500;
                    serialized.FindProperty("staminaCost").intValue = 10 + i;
                    serialized.FindProperty("accountExperienceReward").intValue = 100 + i * 20;
                    serialized.FindProperty("firstClearPremiumCurrency").intValue = i == 9 ? 50 : 30;
                    serialized.FindProperty("rewardCredits").intValue = 5000 + i * 3500;
                    serialized.FindProperty("rewardSkillMaterials").intValue = 10 + i * 5;
                    serialized.FindProperty("threeStarTurnLimit").intValue = 18 + i;
                    serialized.FindProperty("sweepEnabled").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(stage, path);
                }
                database.Add(stage);
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return database;
        }
    }

    public sealed class StageDatabaseWindow : EditorWindow
    {
        StageDatabase database;
        StageData selectedStage;
        UnityEditor.Editor stageInspector;
        Vector2 listScroll;
        Vector2 inspectorScroll;

        public static void Open(StageDatabase target)
        {
            var window = GetWindow<StageDatabaseWindow>("Stage Database");
            window.minSize = new Vector2(920, 620);
            window.database = target != null ? target : StageDatabaseBootstrap.EnsureDefaults();
            window.Show();
        }

        void OnEnable()
        {
            if (database == null) database = StageDatabaseBootstrap.EnsureDefaults();
        }

        void OnDisable()
        {
            if (stageInspector != null) DestroyImmediate(stageInspector);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL STAGE DATABASE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("스테이지 전투 데이터와 스테이지별 BGM을 수정합니다.", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60)))
                AssetDatabase.SaveAssets();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("데이터베이스 찾기", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                Selection.activeObject = database;
                EditorGUIUtility.PingObject(database);
            }
            EditorGUILayout.EndHorizontal();

            if (database == null)
            {
                EditorGUILayout.HelpBox("스테이지 데이터베이스를 불러오지 못했습니다.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            DrawStageList();
            DrawStageInspector();
            EditorGUILayout.EndHorizontal();
        }

        void DrawStageList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330));
            EditorGUILayout.LabelField("등록 스테이지  " + database.Stages.Count, EditorStyles.boldLabel);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUI.skin.box);
            for (int i = 0; i < database.Stages.Count; i++)
            {
                StageData stage = database.Stages[i];
                if (stage == null) continue;
                Color previous = GUI.backgroundColor;
                if (stage == selectedStage) GUI.backgroundColor = new Color(.45f, .85f, 1f);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                GUI.backgroundColor = previous;
                EditorGUILayout.LabelField((i + 1).ToString("00") + "  " + stage.DisplayName,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(stage.Chapter + "  ·  " + stage.Category,
                    EditorStyles.miniLabel);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(stage.BattleBgm != null
                    ? "BGM  " + stage.BattleBgm.name : "BGM  기본 전투곡", EditorStyles.miniLabel);
                if (GUILayout.Button("편집", GUILayout.Width(54))) SelectStage(stage);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawStageInspector()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField("스테이지 상세", EditorStyles.boldLabel);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, GUI.skin.box);
            if (selectedStage == null)
            {
                EditorGUILayout.Space(30);
                EditorGUILayout.HelpBox("왼쪽에서 스테이지를 선택하세요.\nAudio의 Battle Bgm에서 전투곡을 지정할 수 있습니다.",
                    MessageType.Info);
            }
            else
            {
                if (stageInspector == null || stageInspector.target != selectedStage)
                {
                    if (stageInspector != null) DestroyImmediate(stageInspector);
                    stageInspector = UnityEditor.Editor.CreateEditor(selectedStage);
                }
                stageInspector.OnInspectorGUI();
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("Audio > Battle Bgm을 비우면 씬 BGM 설정의 기본 전투곡을 사용합니다.",
                    MessageType.Info);
                if (GUILayout.Button("Project 창에서 찾기", GUILayout.Height(28)))
                {
                    Selection.activeObject = selectedStage;
                    EditorGUIUtility.PingObject(selectedStage);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void SelectStage(StageData stage)
        {
            selectedStage = stage;
            if (stageInspector != null)
            {
                DestroyImmediate(stageInspector);
                stageInspector = null;
            }
            Repaint();
        }
    }

    [InitializeOnLoad]
    static class GameAudioSettingsBootstrap
    {
        internal const string SettingsPath = "Assets/StarfallAcademy/Resources/Data/GameAudioSettings.asset";

        static GameAudioSettingsBootstrap()
        {
            EditorApplication.delayCall += () => EnsureSettings();
        }

        internal static GameAudioSettings EnsureSettings()
        {
            GameAudioSettings settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(SettingsPath);
            if (settings != null) return settings;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<GameAudioSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }

    public sealed class GameAudioSettingsWindow : EditorWindow
    {
        GameAudioSettings settings;
        SerializedObject settingsObject;
        Vector2 scroll;

        [MenuItem("Starfall/Audio/Scene BGM Settings")]
        public static void Open()
        {
            var window = GetWindow<GameAudioSettingsWindow>("Scene BGM Settings");
            window.minSize = new Vector2(620, 480);
            window.Show();
        }

        void OnEnable() => Reload();

        void Reload()
        {
            settings = GameAudioSettingsBootstrap.EnsureSettings();
            settingsObject = settings != null ? new SerializedObject(settings) : null;
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL SCENE BGM SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("일반 씬 BGM과 전투 기본곡을 설정합니다. 스테이지·캐릭터 오디오는 각 데이터베이스에서 수정합니다.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70))) Reload();
            if (GUILayout.Button("모두 저장", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                AssetDatabase.SaveAssets();
                if (Application.isPlaying) GameAudioDirector.RefreshForCurrentScene();
            }
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("설정 에셋 찾기", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    Selection.activeObject = settings;
                    EditorGUIUtility.PingObject(settings);
                }
            }
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSceneBgm();
            EditorGUILayout.EndScrollView();
        }

        void DrawSceneBgm()
        {
            EditorGUILayout.LabelField("씬별 BGM", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("씬이 바뀌면 지정된 곡으로 자동 크로스페이드됩니다. 비어 있는 씬에서는 BGM을 정지합니다.",
                MessageType.Info);
            if (settingsObject == null) return;

            settingsObject.Update();
            EditorGUILayout.BeginVertical(GUI.skin.box);
            SceneClip("lobbyBgm", "로비");
            SceneClip("formationBgm", "편성");
            SceneClip("gachaBgm", "모집");
            SceneClip("shopBgm", "상점");
            SceneClip("characterArchiveBgm", "캐릭터 도감");
            SceneClip("storyArchiveBgm", "스토리 기록실");
            SceneClip("stageSelectBgm", "스테이지 선택");
            SceneClip("weeklyBossMenuBgm", "주간 보스 메뉴");
            SceneClip("challengeTowerBgm", "도전의 탑");
            SceneClip("mailInboxBgm", "우편함 (선택)");
            SceneClip("defaultBattleBgm", "기본 전투");
            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(settingsObject.FindProperty("musicVolume"), new GUIContent("BGM 기본 볼륨"));
            EditorGUILayout.PropertyField(settingsObject.FindProperty("crossFadeSeconds"), new GUIContent("크로스페이드 시간"));
            EditorGUILayout.EndVertical();
            if (settingsObject.ApplyModifiedProperties() && Application.isPlaying)
                GameAudioDirector.RefreshForCurrentScene();
        }

        void SceneClip(string propertyName, string label)
        {
            SerializedProperty property = settingsObject.FindProperty(propertyName);
            if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label + " BGM"));
        }
    }
}
