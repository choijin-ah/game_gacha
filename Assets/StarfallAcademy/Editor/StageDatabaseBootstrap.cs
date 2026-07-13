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
}
