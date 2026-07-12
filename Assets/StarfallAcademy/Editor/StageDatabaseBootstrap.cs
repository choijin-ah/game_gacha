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

        [MenuItem("Starfall/Stage Database")]
        public static void Open()
        {
            StageDatabase database = EnsureDefaults();
            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);
        }

        static StageDatabase EnsureDefaults()
        {
            StageDatabase database = AssetDatabase.LoadAssetAtPath<StageDatabase>(DatabasePath);
            if (database == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
                database = ScriptableObject.CreateInstance<StageDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }
            Directory.CreateDirectory(StageFolder);
            string[] names = { "안개 낀 진입로", "폐쇄된 지하역", "붉은 시계탑", "검은 성당", "월식의 관측소" };
            string[] enemies = { "그림자 잔재", "도시 망령", "시계 인형", "검은 사제", "월식의 파수꾼" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = StageFolder + "/Stage_" + (i + 1).ToString("00") + ".asset";
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
                if (stage == null)
                {
                    stage = ScriptableObject.CreateInstance<StageData>();
                    stage.name = "Stage " + (i + 1).ToString("00");
                    SerializedObject serialized = new SerializedObject(stage);
                    serialized.FindProperty("stageId").stringValue = "stage_" + (i + 1).ToString("00");
                    serialized.FindProperty("chapter").stringValue = i < 3 ? "CHAPTER 1" : "CHAPTER 2";
                    serialized.FindProperty("displayName").stringValue = names[i];
                    serialized.FindProperty("description").stringValue =
                        "도시의 이상 현상을 조사하고 " + enemies[i] + "을(를) 제압하세요.";
                    serialized.FindProperty("enemyName").stringValue = enemies[i];
                    serialized.FindProperty("enemyCount").intValue = i == 4 ? 1 : Mathf.Min(4, 2 + i);
                    serialized.FindProperty("enemyLevel").intValue = 5 + i * 5;
                    serialized.FindProperty("enemyMaxHp").intValue = 1000 + i * 700;
                    serialized.FindProperty("enemyAttack").intValue = 105 + i * 55;
                    serialized.FindProperty("enemySpeed").intValue = 48 + i * 4;
                    serialized.FindProperty("bossStage").boolValue = i == 4;
                    serialized.FindProperty("recommendedPower").intValue = 2500 + i * 2500;
                    serialized.FindProperty("rewardCredits").intValue = 5000 + i * 3500;
                    serialized.FindProperty("rewardSkillMaterials").intValue = 10 + i * 5;
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
}
