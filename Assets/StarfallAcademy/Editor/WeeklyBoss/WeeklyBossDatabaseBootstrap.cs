using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class WeeklyBossDatabaseBootstrap
    {
        internal const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/WeeklyBossDatabase.asset";
        internal const string DataFolder = "Assets/StarfallAcademy/Data/WeeklyBoss";

        static WeeklyBossDatabaseBootstrap() =>
            EditorApplication.delayCall += () => EnsureDatabase();

        [MenuItem("Starfall/Data/Weekly Boss Database")]
        public static void Open() => WeeklyBossDatabaseWindow.Open(EnsureDatabase());

        internal static WeeklyBossDatabase EnsureDatabase()
        {
            WeeklyBossDatabase database =
                AssetDatabase.LoadAssetAtPath<WeeklyBossDatabase>(DatabasePath);
            if (database != null) return database;
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            database = ScriptableObject.CreateInstance<WeeklyBossDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }
    }
}
