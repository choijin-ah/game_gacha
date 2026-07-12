using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    static class GachaConfigBootstrap
    {
        internal const string ConfigPath = "Assets/StarfallAcademy/Resources/Data/GachaConfig.asset";

        static GachaConfigBootstrap()
        {
            EditorApplication.delayCall += () => LoadOrCreate();
        }

        internal static GachaConfig LoadOrCreate()
        {
            GachaConfig config = AssetDatabase.LoadAssetAtPath<GachaConfig>(ConfigPath);
            if (config != null) return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            AssetDatabase.Refresh();
            config = ScriptableObject.CreateInstance<GachaConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }
    }

    public sealed class GachaConfigWindow : EditorWindow
    {
        GachaConfig config;
        CharacterDatabase characterDatabase;
        CharacterData pendingPickup;
        Vector2 scroll;

        [MenuItem("Starfall Academy/Gacha Configuration")]
        public static void Open()
        {
            var window = GetWindow<GachaConfigWindow>("Gacha Configuration");
            window.minSize = new Vector2(720, 680);
            window.Show();
        }

        void OnEnable()
        {
            config = GachaConfigBootstrap.LoadOrCreate();
            characterDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabaseBootstrap.DatabasePath);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("STARFALL GACHA TOOL", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("픽업 캐릭터, 확률, 천장과 비용을 설정합니다.", EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            if (config == null)
            {
                EditorGUILayout.HelpBox("GachaConfig 에셋을 만들 수 없습니다.", MessageType.Error);
                if (GUILayout.Button("다시 만들기")) config = GachaConfigBootstrap.LoadOrCreate();
                return;
            }

            DrawToolbar();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            SerializedObject serialized = new SerializedObject(config);
            serialized.Update();
            DrawSection("배너", serialized, "bannerTitle", "bannerSubtitle", "pityGroupId");
            DrawSection("확률 (%)", serialized, "topRarityRatePercent", "featuredSharePercent", "fourStarRatePercent");
            DrawSection("천장", serialized, "hardPity", "softPityStart", "softPityBonusPerPullPercent",
                "guaranteeFeaturedAfterMiss", "guaranteeFourStarOnTenPull");
            DrawSection("비용", serialized, "singlePullCost", "tenPullCost");
            serialized.ApplyModifiedProperties();
            DrawPickupCharacters();
            DrawValidation();
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("저장", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button("Project에서 찾기", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("5★ 이상 자동 등록", EditorStyles.toolbarButton, GUILayout.Width(125)))
                AddAllTopRarityCharacters();
            EditorGUILayout.EndHorizontal();
        }

        static void DrawSection(string title, SerializedObject serialized, params string[] propertyNames)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string propertyName in propertyNames)
                EditorGUILayout.PropertyField(serialized.FindProperty(propertyName));
            EditorGUILayout.EndVertical();
        }

        void DrawPickupCharacters()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("픽업 캐릭터", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("런타임 왼쪽 목록에서 플레이어가 이 중 한 명을 선택합니다.", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            pendingPickup = (CharacterData)EditorGUILayout.ObjectField("캐릭터", pendingPickup, typeof(CharacterData), false);
            using (new EditorGUI.DisabledScope(pendingPickup == null))
            {
                if (GUILayout.Button("추가", GUILayout.Width(64)))
                {
                    Undo.RecordObject(config, "Add pickup character");
                    config.AddPickup(pendingPickup);
                    EditorUtility.SetDirty(config);
                    pendingPickup = null;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);

            if (config.PickupCharacters.Count == 0)
                EditorGUILayout.HelpBox("등록된 픽업 캐릭터가 없습니다.", MessageType.Info);
            for (int i = 0; i < config.PickupCharacters.Count; i++)
            {
                CharacterData character = config.PickupCharacters[i];
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                EditorGUILayout.ObjectField(character, typeof(CharacterData), false);
                if (character != null)
                    EditorGUILayout.LabelField(character.Rarity + "★  " + character.DisplayName, GUILayout.Width(160));
                if (GUILayout.Button("제거", GUILayout.Width(56)))
                {
                    Undo.RecordObject(config, "Remove pickup character");
                    config.RemovePickup(character);
                    EditorUtility.SetDirty(config);
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawValidation()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("검증", EditorStyles.boldLabel);
            bool hasTop = false, hasFour = false, hasLower = false, invalidPickup = false;
            if (characterDatabase != null)
            {
                foreach (CharacterData character in characterDatabase.Characters)
                {
                    if (character == null) continue;
                    if (character.Rarity >= 5) hasTop = true;
                    else if (character.Rarity == 4) hasFour = true;
                    else hasLower = true;
                }
            }
            foreach (CharacterData pickup in config.PickupCharacters)
                if (pickup == null || pickup.Rarity < 5) invalidPickup = true;

            if (!hasTop) EditorGUILayout.HelpBox("Character Database에 5★ 이상 캐릭터가 없습니다.", MessageType.Warning);
            if (!hasFour) EditorGUILayout.HelpBox("4★ 캐릭터가 없어 10회 최소 보장을 적용할 수 없습니다.", MessageType.Warning);
            if (!hasLower) EditorGUILayout.HelpBox("3★ 이하 일반 캐릭터가 없습니다.", MessageType.Warning);
            if (invalidPickup) EditorGUILayout.HelpBox("픽업 목록에는 5★ 이상 캐릭터만 등록하세요.", MessageType.Error);
            EditorGUILayout.LabelField("선택 픽업 절대 확률",
                config.EffectiveSelectedPickupRatePercent.ToString("0.###") + "%");
            EditorGUILayout.LabelField("현재 초기 재화", PlayerWallet.DefaultPremiumCurrency.ToString("N0"));
            EditorGUILayout.EndVertical();
        }

        void AddAllTopRarityCharacters()
        {
            if (characterDatabase == null) return;
            Undo.RecordObject(config, "Auto add pickup characters");
            foreach (CharacterData character in characterDatabase.Characters)
                if (character != null && character.Rarity >= 5) config.AddPickup(character);
            EditorUtility.SetDirty(config);
        }
    }
}
