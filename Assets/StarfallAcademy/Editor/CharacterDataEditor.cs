using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomEditor(typeof(CharacterData))]
    public sealed class CharacterDataEditor : UnityEditor.Editor
    {
        static bool identityExpanded = true;
        static bool battleExpanded = true;
        static bool actionExpanded = true;
        static bool audioExpanded;
        static bool growthExpanded = true;
        static bool awakeningExpanded = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawScript();
            DrawSection("기본 정보", ref identityExpanded,
                "characterId", "displayName", "affiliation", "description",
                "portrait", "gachaArt", "accentColor", "role", "attackType", "rarity");
            DrawSection("전투 능력치", ref battleExpanded,
                "combatPower", "battleElement", "maxHpOverride", "attackOverride",
                "defenseOverride", "speedOverride", "critChance", "critDamage",
                "maxEnergyOverride", "aggroWeight");
            DrawSection("일반 공격 · 스킬 · 궁극기", ref actionExpanded,
                "skillName", "skillIcon", "defaultSkillIcon", "basicAction", "skillAction",
                "ultimateAction");
            DrawSection("전투 오디오", ref audioExpanded,
                "basicAttackSfx", "basicAttackVoices", "skillSfx", "skillVoices",
                "ultimateSfx", "ultimateVoices", "actionSfxVolume", "voiceVolume");
            DrawSection("성장", ref growthExpanded,
                "level", "maxLevel", "levelUpBaseCreditCost", "levelUpCreditCostGrowth",
                "combatPowerPerLevel", "skillMaxLevel", "skillBaseMaterialCost",
                "skillMaterialCostGrowth", "combatPowerPerSkillLevel");
            DrawAwakening();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawScript()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        }

        void DrawSection(string title, ref bool expanded, params string[] properties)
        {
            EditorGUILayout.Space(3);
            expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
            if (!expanded) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i < properties.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(properties[i]);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            EditorGUI.indentLevel--;
        }

        void DrawAwakening()
        {
            EditorGUILayout.Space(3);
            awakeningExpanded = EditorGUILayout.Foldout(awakeningExpanded, "각성", true,
                EditorStyles.foldoutHeader);
            if (!awakeningExpanded) return;

            EditorGUI.indentLevel++;
            SerializedProperty duplicateReward = serializedObject.FindProperty("duplicateFragmentReward");
            SerializedProperty stages = serializedObject.FindProperty("awakeningStages");
            EditorGUILayout.PropertyField(duplicateReward, new GUIContent("중복 획득 조각"));
            EditorGUILayout.PropertyField(stages, new GUIContent("각성 단계"), true);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("단계 추가"))
            {
                Undo.RecordObject(target, "Add awakening stage");
                stages.InsertArrayElementAtIndex(stages.arraySize);
                ResetNewStage(stages.GetArrayElementAtIndex(stages.arraySize - 1), stages.arraySize);
            }
            using (new EditorGUI.DisabledScope(stages.arraySize == 0))
            {
                if (GUILayout.Button("이전 단계 복제"))
                {
                    Undo.RecordObject(target, "Duplicate awakening stage");
                    stages.InsertArrayElementAtIndex(stages.arraySize);
                }
                if (GUILayout.Button("마지막 삭제"))
                {
                    Undo.RecordObject(target, "Remove awakening stage");
                    stages.DeleteArrayElementAtIndex(stages.arraySize - 1);
                }
            }
            EditorGUILayout.EndHorizontal();
            DrawAwakeningPreview();
            EditorGUI.indentLevel--;
        }

        void DrawAwakeningPreview()
        {
            serializedObject.ApplyModifiedProperties();
            CharacterData character = (CharacterData)target;
            int cumulativeFragments = 0;
            int estimatedPower = character.CombatPower;
            var lines = new List<string>();
            for (int i = 0; i < character.AwakeningStages.Count; i++)
            {
                AwakeningStageDefinition stage = character.AwakeningStages[i];
                if (stage == null) continue;
                cumulativeFragments += stage.RequiredFragments;
                for (int j = 0; j < stage.StatModifiers.Count; j++)
                {
                    AwakeningStatModifier modifier = stage.StatModifiers[j];
                    if (modifier != null && modifier.Stat == AwakeningStatType.CombatPowerFlat)
                        estimatedPower += Mathf.Max(0, Mathf.RoundToInt(modifier.Value));
                }
                lines.Add("Stage " + (i + 1) + " · cumulative fragments " + cumulativeFragments
                    + " · estimated power " + estimatedPower.ToString("N0"));
            }
            string validation = ValidateCharacter(character).FirstOrDefault();
            if (!string.IsNullOrEmpty(validation))
                EditorGUILayout.HelpBox(validation, MessageType.Warning);
            EditorGUILayout.HelpBox(lines.Count == 0 ? "No awakening stages configured."
                : string.Join("\n", lines), MessageType.Info);
            serializedObject.Update();
        }

        static void ResetNewStage(SerializedProperty stage, int index)
        {
            if (stage == null) return;
            SerializedProperty cost = stage.FindPropertyRelative("requiredFragments");
            if (cost != null) cost.intValue = Mathf.Max(10, index * 10);
            SerializedProperty stats = stage.FindPropertyRelative("statModifiers");
            if (stats != null) stats.arraySize = 0;
            SerializedProperty skills = stage.FindPropertyRelative("skillEffectChanges");
            if (skills != null) skills.arraySize = 0;
            SerializedProperty description = stage.FindPropertyRelative("description");
            if (description != null) description.stringValue = string.Empty;
        }

        static IEnumerable<string> ValidateCharacter(CharacterData character)
        {
            if (character == null) yield break;
            if (character.DuplicateFragmentReward <= 0)
                yield return "Duplicate fragment reward must be greater than zero.";
            int previous = 0;
            const int MaximumStages = 10;
            if (character.AwakeningStages.Count > MaximumStages)
                yield return "Awakening stages exceed the supported maximum of " + MaximumStages + ".";
            for (int i = 0; i < character.AwakeningStages.Count; i++)
            {
                AwakeningStageDefinition stage = character.AwakeningStages[i];
                if (stage == null)
                {
                    yield return "Awakening stage " + (i + 1) + " is empty.";
                    continue;
                }
                if (stage.RequiredFragments <= 0)
                    yield return "Stage " + (i + 1) + " fragment cost must be positive.";
                if (stage.RequiredFragments < previous)
                    yield return "Stage fragment costs should be in ascending order.";
                previous = stage.RequiredFragments;
                var stats = new HashSet<AwakeningStatType>();
                for (int j = 0; j < stage.StatModifiers.Count; j++)
                {
                    AwakeningStatModifier modifier = stage.StatModifiers[j];
                    if (modifier == null)
                    {
                        yield return "Stage " + (i + 1) + " contains an empty stat modifier.";
                        continue;
                    }
                    if (!Enum.IsDefined(typeof(AwakeningStatType), modifier.Stat))
                        yield return "Stage " + (i + 1) + " contains an invalid stat modifier.";
                    else if (!stats.Add(modifier.Stat))
                        yield return "Stage " + (i + 1) + " contains duplicate " + modifier.Stat + ".";
                }
                var actions = new HashSet<BattleActionKind>();
                for (int j = 0; j < stage.SkillEffectChanges.Count; j++)
                {
                    AwakeningSkillEffectChange change = stage.SkillEffectChanges[j];
                    if (change == null)
                    {
                        yield return "Stage " + (i + 1) + " contains an empty skill effect change.";
                        continue;
                    }
                    if (!Enum.IsDefined(typeof(BattleActionKind), change.Action))
                        yield return "Stage " + (i + 1) + " references an invalid battle action.";
                    else if (!actions.Add(change.Action))
                        yield return "Stage " + (i + 1) + " contains duplicate " + change.Action + " changes.";
                }
            }
        }

        [InitializeOnLoadMethod]
        static void RegisterValidator()
        {
            ContentValidationRegistry.Register("Character Progression", ValidateAllCharacters);
        }

        static IEnumerable<ContentValidationIssue> ValidateAllCharacters()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterData");
            for (int i = 0; i < guids.Length; i++)
            {
                CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (character == null) continue;
                foreach (string message in ValidateCharacter(character))
                    yield return new ContentValidationIssue(ContentValidationSeverity.Warning,
                        "Character", character.Id, message, character);
            }
        }

        [MenuItem("Starfall/Validate/Character Progression")]
        static void OpenValidation() => ContentValidationWindow.Open("Character Progression");
    }
}
