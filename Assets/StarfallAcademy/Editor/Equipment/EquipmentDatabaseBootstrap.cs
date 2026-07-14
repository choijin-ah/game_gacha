using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public static class EquipmentDatabaseBootstrap
    {
        public const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/EquipmentDatabase.asset";
        public const string EquipmentFolder = "Assets/StarfallAcademy/Data/Equipment/Definitions";
        public const string SetFolder = "Assets/StarfallAcademy/Data/Equipment/Sets";
        public const string DropTableFolder = "Assets/StarfallAcademy/Data/Equipment/DropTables";

        public static EquipmentDatabase LoadOrCreate()
        {
            EquipmentDatabase database = AssetDatabase.LoadAssetAtPath<EquipmentDatabase>(DatabasePath);
            if (database != null) return database;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            Directory.CreateDirectory(EquipmentFolder);
            Directory.CreateDirectory(SetFolder);
            Directory.CreateDirectory(DropTableFolder);
            AssetDatabase.Refresh();
            database = ScriptableObject.CreateInstance<EquipmentDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }
    }
}
