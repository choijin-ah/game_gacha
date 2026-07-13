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

        [MenuItem("Starfall/Data/Gacha Configuration")]
        public static void Open()
        {
            var window = GetWindow<GachaConfigWindow>("Gacha Configuration");
            window.minSize = new Vector2(720, 680);
            window.Show();
        }

        [MenuItem("Starfall/Validate/Gacha Configuration")]
        public static void ValidateConfigurationMenu()
        {
            GachaConfig currentConfig = GachaConfigBootstrap.LoadOrCreate();
            CharacterDatabase currentDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
                CharacterDatabaseBootstrap.DatabasePath);
            if (TryValidatePools(currentConfig, currentDatabase, out string message))
                Debug.Log("[Starfall Gacha] " + message);
            else
                Debug.LogError("[Starfall Gacha] " + message);
        }

        [MenuItem("Starfall/Fix/Gacha Rates For Current Pool")]
        public static void FixRatesForCurrentPoolMenu()
        {
            GachaConfig currentConfig = GachaConfigBootstrap.LoadOrCreate();
            CharacterDatabase currentDatabase = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
                CharacterDatabaseBootstrap.DatabasePath);
            FixRatesForCurrentPool(currentConfig, currentDatabase);
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
            if (config.ThreeStarRatePercent <= .0001f) hasLower = true;

            if (!hasTop) EditorGUILayout.HelpBox("Character Database에 5★ 이상 캐릭터가 없습니다.", MessageType.Warning);
            if (!hasFour) EditorGUILayout.HelpBox("4★ 캐릭터가 없어 10회 최소 보장을 적용할 수 없습니다.", MessageType.Warning);
            if (!hasLower) EditorGUILayout.HelpBox("3★ 이하 일반 캐릭터가 없습니다.", MessageType.Warning);
            if (invalidPickup) EditorGUILayout.HelpBox("픽업 목록에는 5★ 이상 캐릭터만 등록하세요.", MessageType.Error);
            EditorGUILayout.LabelField("선택 픽업 절대 확률",
                config.EffectiveSelectedPickupRatePercent.ToString("0.###") + "%");
            EditorGUILayout.LabelField(PlayerWallet.PremiumCurrencyDisplayName + " 초기 지급량",
                PlayerWallet.DefaultPremiumCurrency.ToString("N0"));
            if (!TryValidatePools(config, characterDatabase, out string validationMessage))
                EditorGUILayout.HelpBox(validationMessage, MessageType.Error);
            else
                EditorGUILayout.HelpBox(validationMessage, MessageType.Info);
            if (GUILayout.Button("Fix rates for the current character pool"))
            {
                FixRatesForCurrentPool(config, characterDatabase);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndVertical();
        }

        static bool TryValidatePools(GachaConfig currentConfig,
            CharacterDatabase currentDatabase, out string message)
        {
            if (currentConfig == null || currentDatabase == null)
            {
                message = "GachaConfig or CharacterDatabase is missing.";
                return false;
            }
            if (currentConfig.PickupCharacters == null
                || currentConfig.PickupCharacters.Count == 0)
            {
                message = "At least one pickup character must be configured.";
                return false;
            }
            foreach (CharacterData pickup in currentConfig.PickupCharacters)
            {
                if (pickup == null || pickup.Rarity < 5)
                {
                    message = "Every pickup entry must reference a 5-star or higher character.";
                    return false;
                }

                bool inDatabase = false;
                foreach (CharacterData character in currentDatabase.Characters)
                {
                    if (character != pickup) continue;
                    inDatabase = true;
                    break;
                }
                if (!inDatabase)
                {
                    message = "Pickup character '" + pickup.name
                        + "' is not included in CharacterDatabase.";
                    return false;
                }
            }

            GetPoolAvailability(currentDatabase, out bool hasTop, out bool hasFour,
                out bool hasThree);
            if (!hasTop)
            {
                message = "The top-rarity character pool is empty.";
                return false;
            }
            if ((currentConfig.FourStarRatePercent > .0001f
                || currentConfig.GuaranteeFourStarOnTenPull) && !hasFour)
            {
                message = "The 4-star rate/guarantee is enabled, but the 4-star pool is empty.";
                return false;
            }
            if (currentConfig.ThreeStarRatePercent > .0001f && !hasThree)
            {
                message = "The 3-star rate is enabled, but the 3-star pool is empty.";
                return false;
            }

            message = "Gacha pools and absolute rarity rates are valid: 5-star "
                + currentConfig.TopRarityRatePercent.ToString("0.###") + "%, 4-star "
                + currentConfig.FourStarRatePercent.ToString("0.###") + "%, 3-star "
                + currentConfig.ThreeStarRatePercent.ToString("0.###") + "%";
            return true;
        }

        static void FixRatesForCurrentPool(GachaConfig currentConfig,
            CharacterDatabase currentDatabase)
        {
            if (currentConfig == null || currentDatabase == null)
            {
                Debug.LogError("[Starfall Gacha] Cannot fix rates without both config and database assets.");
                return;
            }

            GetPoolAvailability(currentDatabase, out bool hasTop, out bool hasFour,
                out bool hasThree);
            if (!hasTop || (!hasFour && !hasThree))
            {
                Debug.LogError("[Starfall Gacha] Add the missing rarity content before fixing rates.");
                return;
            }

            Undo.RecordObject(currentConfig, "Fix gacha rates for current character pool");
            var serialized = new SerializedObject(currentConfig);
            serialized.Update();
            SerializedProperty topRate = serialized.FindProperty("topRarityRatePercent");
            SerializedProperty fourRate = serialized.FindProperty("fourStarRatePercent");
            SerializedProperty fourGuarantee = serialized.FindProperty("guaranteeFourStarOnTenPull");

            if (!hasThree && hasFour)
                fourRate.floatValue = Mathf.Max(0f, 100f - topRate.floatValue);
            else if (!hasFour && hasThree)
            {
                fourRate.floatValue = 0f;
                fourGuarantee.boolValue = false;
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(currentConfig);
            AssetDatabase.SaveAssets();
            TryValidatePools(currentConfig, currentDatabase, out string message);
            Debug.Log("[Starfall Gacha] " + message);
        }

        static void GetPoolAvailability(CharacterDatabase currentDatabase,
            out bool hasTop, out bool hasFour, out bool hasThree)
        {
            hasTop = hasFour = hasThree = false;
            if (currentDatabase == null) return;
            foreach (CharacterData character in currentDatabase.Characters)
            {
                if (character == null) continue;
                if (character.Rarity >= 5) hasTop = true;
                else if (character.Rarity == 4) hasFour = true;
                else if (character.Rarity == 3) hasThree = true;
            }
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
