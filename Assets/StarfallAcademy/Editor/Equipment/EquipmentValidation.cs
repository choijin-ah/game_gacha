using System;
using System.Collections.Generic;
using UnityEditor;

namespace StarfallAcademy.Lobby.Editor
{
    static class EquipmentValidation
    {
        [InitializeOnLoadMethod]
        static void Register()
        {
            ContentValidationRegistry.Register("Equipment", Validate);
        }

        [MenuItem("Starfall/Validate/Equipment and Drop Tables")]
        static void Open() => ContentValidationWindow.Open("Equipment");

        static IEnumerable<ContentValidationIssue> Validate()
        {
            EquipmentDatabase database = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(
                EquipmentDatabaseBootstrap.DatabasePath);
            if (database == null)
            {
                yield return Error("Database", "EquipmentDatabase is missing.");
                yield break;
            }

            var equipmentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < database.Equipment.Count; i++)
            {
                EquipmentDefinition item = database.Equipment[i];
                if (item == null)
                {
                    yield return Error("Equipment #" + (i + 1), "The equipment reference is empty.", database);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Id)) yield return Error(item.name, "equipmentId is empty.", item);
                else if (!equipmentIds.Add(item.Id)) yield return Error(item.Id, "equipmentId is duplicated.", item);
                if (!Enum.IsDefined(typeof(EquipmentSlot), item.Slot))
                    yield return Error(item.Id, "Equipment slot value is invalid.", item);
                if (item.MaximumLevel <= 0) yield return Error(item.Id, "Maximum level must be positive.", item);
                if (item.EnhancementBaseCost < 0 || item.EnhancementCostPerLevel < 0)
                    yield return Error(item.Id, "Enhancement costs cannot be negative.", item);
                if (item.Set != null && !Contains(database.Sets, item.Set))
                    yield return Error(item.Id, "The referenced set is not registered in the database.", item);
            }

            var setIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < database.Sets.Count; i++)
            {
                EquipmentSetDefinition set = database.Sets[i];
                if (set == null) { yield return Error("Set #" + (i + 1), "The set reference is empty.", database); continue; }
                if (!setIds.Add(set.Id)) yield return Error(set.Id, "setId is duplicated.", set);
            }

            var dropIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < database.DropTables.Count; i++)
            {
                EquipmentDropTable table = database.DropTables[i];
                if (table == null) { yield return Error("Drop table #" + (i + 1), "Reference is empty.", database); continue; }
                if (!dropIds.Add(table.Id)) yield return Error(table.Id, "dropTableId is duplicated.", table);
                if (table.TotalWeight <= 0f) yield return Error(table.Id, "Drop weight total is zero.", table);
                for (int j = 0; j < table.Candidates.Count; j++)
                    if (table.Candidates[j] == null || table.Candidates[j].Equipment == null)
                        yield return Error(table.Id + " / Candidate " + (j + 1), "Equipment reference is empty.", table);
            }

            string[] stageGuids = AssetDatabase.FindAssets("t:StageData");
            for (int i = 0; i < stageGuids.Length; i++)
            {
                StageData stage = AssetDatabase.LoadAssetAtPath<StageData>(
                    AssetDatabase.GUIDToAssetPath(stageGuids[i]));
                if (stage?.EquipmentDropTable != null && !Contains(database.DropTables, stage.EquipmentDropTable))
                    yield return Error(stage.Id, "Stage references an unregistered equipment drop table.", stage);
            }
        }

        static bool Contains<T>(IReadOnlyList<T> values, T target) where T : UnityEngine.Object
        {
            for (int i = 0; i < values.Count; i++) if (values[i] == target) return true;
            return false;
        }

        static ContentValidationIssue Error(string location, string message,
            UnityEngine.Object context = null) => new ContentValidationIssue(
                ContentValidationSeverity.Error, "Equipment", location, message, context);
    }
}
