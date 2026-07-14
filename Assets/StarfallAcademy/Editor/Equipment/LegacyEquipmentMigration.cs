using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public static class LegacyEquipmentMigration
    {
        const string MigrationVersionKey = "StarfallAcademy.Migration.Equipment.Version";
        const int CurrentVersion = 1;

        [MenuItem("Starfall/Migrate/Legacy Equipment Data")]
        public static void Run()
        {
            if (PlayerPrefs.GetInt(MigrationVersionKey, 0) >= CurrentVersion)
            {
                EditorUtility.DisplayDialog("Equipment migration", "Legacy equipment was already migrated.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Migrate legacy equipment",
                "Back up the current equipment inventory and convert character slot levels?",
                "Back up and migrate", "Cancel")) return;

            EquipmentDatabase equipmentDatabase = EquipmentDatabaseBootstrap.LoadOrCreate();
            CharacterDatabase characters = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
                CharacterDatabaseBootstrap.DatabasePath);
            if (characters == null)
            {
                EditorUtility.DisplayDialog("Equipment migration", "CharacterDatabase is missing.", "OK");
                return;
            }

            EquipmentInventoryService inventory = EquipmentInventoryService.Default;
            WriteBackup(inventory.ExportJson(true));
            int migrated = 0;
            for (int i = 0; i < characters.Characters.Count; i++)
            {
                CharacterData character = characters.Characters[i];
                if (character == null) continue;
                foreach (EquipmentSlot slot in EquipmentService.Slots)
                {
                    int level = EquipmentService.GetLevel(character, slot);
                    if (level <= 0) continue;
                    EquipmentDefinition definition = EnsureLegacyDefinition(equipmentDatabase, slot);
                    string instanceId = "legacy_" + character.Id + "_" + slot;
                    EquipmentInstance instance = inventory.Add(definition.Id, level, instanceId);
                    if (instance != null && inventory.Equip(instance.instanceId, character,
                        equipmentDatabase, out _)) migrated++;
                }
            }
            PlayerPrefs.SetInt(MigrationVersionKey, CurrentVersion);
            PlayerPrefs.Save();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Equipment migration", migrated
                + " equipped item(s) were migrated. A JSON backup was saved under Library/StarfallAcademy/Backups.", "OK");
        }

        static EquipmentDefinition EnsureLegacyDefinition(EquipmentDatabase database, EquipmentSlot slot)
        {
            string id = "legacy_" + slot.ToString().ToLowerInvariant();
            EquipmentDefinition existing = database.FindEquipment(id);
            if (existing != null) return existing;
            Directory.CreateDirectory(EquipmentDatabaseBootstrap.EquipmentFolder);
            var definition = ScriptableObject.CreateInstance<EquipmentDefinition>();
            definition.name = "Legacy " + slot;
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("equipmentId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = "기본 " + EquipmentService.GetSlotDisplayName(slot);
            serialized.FindProperty("slot").enumValueIndex = (int)slot;
            serialized.FindProperty("maximumLevel").intValue = EquipmentService.MaxEquipmentLevel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                EquipmentDatabaseBootstrap.EquipmentFolder + "/Legacy " + slot + ".asset");
            AssetDatabase.CreateAsset(definition, path);
            database.Add(definition);
            EditorUtility.SetDirty(database);
            return definition;
        }

        static void WriteBackup(string json)
        {
            string folder = "Library/StarfallAcademy/Backups";
            Directory.CreateDirectory(folder);
            string path = folder + "/equipment_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json";
            File.WriteAllText(path, json ?? "{}");
        }
    }
}
