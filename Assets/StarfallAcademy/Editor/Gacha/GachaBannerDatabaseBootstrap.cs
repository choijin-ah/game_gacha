using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class GachaBannerDatabaseBootstrap
    {
        internal const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/GachaBannerDatabase.asset";
        internal const string BannerFolder = "Assets/StarfallAcademy/Data/Gacha/Banners";
        internal const string CharacterDatabasePath =
            "Assets/StarfallAcademy/Resources/Data/CharacterDatabase.asset";

        internal static GachaBannerDatabase LoadOrCreate(bool migrateLegacy = true)
        {
            GachaBannerDatabase database =
                AssetDatabase.LoadAssetAtPath<GachaBannerDatabase>(DatabasePath);
            if (database == null)
            {
                EnsureFolders();
                database = ScriptableObject.CreateInstance<GachaBannerDatabase>();
                database.name = "GachaBannerDatabase";
                AssetDatabase.CreateAsset(database, DatabasePath);
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }

            if (migrateLegacy && (database.Banners == null || database.Banners.Count == 0))
                LegacyGachaConfigMigration.TryMigrate(database, false, out _);
            return database;
        }

        internal static void EnsureFolders()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            Directory.CreateDirectory(BannerFolder);
            AssetDatabase.Refresh();
        }

        internal static CharacterDatabase LoadCharacterDatabase() =>
            AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharacterDatabasePath);
    }
}
