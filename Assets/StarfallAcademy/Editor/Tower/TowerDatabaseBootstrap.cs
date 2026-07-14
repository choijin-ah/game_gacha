using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class TowerDatabaseBootstrap
    {
        internal const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/TowerDatabase.asset";
        internal const string DataFolder = "Assets/StarfallAcademy/Data/Tower";

        static TowerDatabaseBootstrap() =>
            EditorApplication.delayCall += () => EnsureDatabase();

        [MenuItem("Starfall/Data/Challenge Tower Database")]
        public static void Open() => ChallengeTowerDatabaseWindow.Open(EnsureDatabase());

        internal static TowerDatabase EnsureDatabase()
        {
            TowerDatabase database = AssetDatabase.LoadAssetAtPath<TowerDatabase>(DatabasePath);
            if (database != null) return database;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            database = ScriptableObject.CreateInstance<TowerDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }
    }
}
