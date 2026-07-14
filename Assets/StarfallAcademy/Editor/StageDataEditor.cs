using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomEditor(typeof(StageData))]
    public sealed class StageDataEditor : UnityEditor.Editor
    {
        static bool basic = true;
        static bool audio = true;
        static bool enemies = true;
        static bool boss = true;
        static bool requirements = true;
        static bool rewards = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            Section("기본 정보", ref basic, "stageId", "chapter", "displayName", "description", "category");
            Section("오디오", ref audio, "battleBgm");
            Section("적 구성", ref enemies, "enemyName", "enemyCount", "enemyLevel", "enemyMaxHp",
                "enemyAttack", "enemySpeed", "bossStage", "enemyArchetype", "enemyWeaknesses",
                "enemyDefense", "enemyMaxBreak", "enemyDelayResistance", "enemyEffectResistance",
                "initialSkillPoints", "enemyLineup", "showsDangerWarning");
            Section("보스 2페이즈", ref boss, "bossPhaseTwoEnabled", "bossPhaseTwoThreshold",
                "bossPhaseTwoAttackMultiplier", "bossPhaseTwoSpeedBonus", "bossPhaseTwoSummonCount");
            Section("입장 조건", ref requirements, "recommendedPower", "staminaCost", "threeStarTurnLimit",
                "sweepEnabled");
            Section("고정 보상 · 장비 드롭", ref rewards, "accountExperienceReward",
                "firstClearPremiumCurrency", "rewardCredits", "rewardSkillMaterials",
                "firstClearRewardPackage", "repeatClearRewardPackage", "equipmentDropTable");
            serializedObject.ApplyModifiedProperties();

            StageData stage = (StageData)target;
            RewardPackage first = stage.FirstClearRewardPackage;
            RewardPackage repeat = stage.RepeatClearRewardPackage;
            EditorGUILayout.HelpBox("First clear: " + (first != null && !first.IsEmpty
                    ? first.Summary : "legacy fixed reward") + "\nRepeat: "
                + (repeat != null && !repeat.IsEmpty ? repeat.Summary : "legacy fixed reward")
                + "\nEquipment: " + (stage.EquipmentDropTable != null
                    ? stage.EquipmentDropTable.Id : "none"), MessageType.Info);
        }

        void Section(string title, ref bool expanded, params string[] names)
        {
            EditorGUILayout.Space(3);
            expanded = EditorGUILayout.Foldout(expanded, title, true, EditorStyles.foldoutHeader);
            if (!expanded) return;
            EditorGUI.indentLevel++;
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty property = serializedObject.FindProperty(names[i]);
                if (property != null) EditorGUILayout.PropertyField(property, true);
            }
            EditorGUI.indentLevel--;
        }
    }
}
