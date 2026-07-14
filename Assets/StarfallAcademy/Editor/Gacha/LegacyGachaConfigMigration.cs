using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class LegacyGachaConfigMigration
    {
        internal const string LegacyConfigPath =
            "Assets/StarfallAcademy/Resources/Data/GachaConfig.asset";
        internal const string MigratedBannerPath =
            "Assets/StarfallAcademy/Data/Gacha/Banners/LegacyGachaBanner.asset";
        const string MigratedBannerId = "legacy_standard_pickup";

        [MenuItem("Starfall/Migrate/Legacy Gacha Configuration")]
        public static void MigrateMenu()
        {
            GachaBannerDatabase database = GachaBannerDatabaseBootstrap.LoadOrCreate(false);
            TryMigrate(database, true, out string message);
            Debug.Log("[Starfall Gacha] " + message, database);
        }

        internal static bool TryMigrate(GachaBannerDatabase database, bool showDialog,
            out string message)
        {
            if (database == null)
            {
                message = "GachaBannerDatabase is missing.";
                Show(showDialog, message);
                return false;
            }

            GachaConfig legacy = AssetDatabase.LoadAssetAtPath<GachaConfig>(LegacyConfigPath);
            if (legacy == null)
            {
                message = "No legacy GachaConfig asset was found.";
                Show(showDialog, message);
                return false;
            }

            for (int i = 0; i < database.Banners.Count; i++)
            {
                GachaBannerData existing = database.Banners[i];
                if (existing != null && (existing.LegacySource == legacy
                    || existing.Id == MigratedBannerId))
                {
                    message = "Legacy GachaConfig was already migrated.";
                    Show(showDialog, message);
                    return false;
                }
            }

            GachaBannerDatabaseBootstrap.EnsureFolders();
            GachaBannerData banner =
                AssetDatabase.LoadAssetAtPath<GachaBannerData>(MigratedBannerPath);
            bool created = false;
            if (banner == null)
            {
                banner = ScriptableObject.CreateInstance<GachaBannerData>();
                banner.name = "Legacy Gacha Banner";
                CopyLegacyValues(legacy, banner);
                AssetDatabase.CreateAsset(banner, MigratedBannerPath);
                Undo.RegisterCreatedObjectUndo(banner, "Migrate legacy gacha configuration");
                created = true;
            }

            Undo.RecordObject(database, "Register migrated gacha banner");
            bool added = database.Add(banner);
            if (added) EditorUtility.SetDirty(database);
            if (created) EditorUtility.SetDirty(banner);
            AssetDatabase.SaveAssets();
            message = created
                ? "Migrated the legacy GachaConfig into a multi-banner asset."
                : "Reconnected the existing migrated banner asset to the database.";
            Show(showDialog, message);
            return created || added;
        }

        static void CopyLegacyValues(GachaConfig source, GachaBannerData destination)
        {
            var sourceObject = new SerializedObject(source);
            var destinationObject = new SerializedObject(destination);
            sourceObject.Update();
            destinationObject.Update();

            CopyString(sourceObject, destinationObject, "bannerTitle");
            CopyString(sourceObject, destinationObject, "bannerSubtitle");
            CopyString(sourceObject, destinationObject, "pityGroupId");
            CopyFloat(sourceObject, destinationObject, "topRarityRatePercent");
            CopyFloat(sourceObject, destinationObject, "featuredSharePercent");
            CopyFloat(sourceObject, destinationObject, "fourStarRatePercent");
            CopyInt(sourceObject, destinationObject, "hardPity");
            CopyInt(sourceObject, destinationObject, "softPityStart");
            CopyFloat(sourceObject, destinationObject, "softPityBonusPerPullPercent");
            CopyBool(sourceObject, destinationObject, "guaranteeFeaturedAfterMiss");
            CopyBool(sourceObject, destinationObject, "guaranteeFourStarOnTenPull");
            CopyInt(sourceObject, destinationObject, "singlePullCost");
            CopyInt(sourceObject, destinationObject, "tenPullCost");

            SerializedProperty sourcePickups = sourceObject.FindProperty("pickupCharacters");
            SerializedProperty destinationPickups =
                destinationObject.FindProperty("pickupCharacters");
            if (sourcePickups != null && destinationPickups != null)
            {
                destinationPickups.arraySize = sourcePickups.arraySize;
                for (int i = 0; i < sourcePickups.arraySize; i++)
                    destinationPickups.GetArrayElementAtIndex(i).objectReferenceValue =
                        sourcePickups.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            SetString(destinationObject, "bannerId", MigratedBannerId);
            SerializedProperty type = destinationObject.FindProperty("bannerType");
            if (type != null) type.enumValueIndex = (int)GachaBannerType.Pickup;
            SerializedProperty legacySource = destinationObject.FindProperty("legacySource");
            if (legacySource != null) legacySource.objectReferenceValue = source;
            destinationObject.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CopyString(SerializedObject source, SerializedObject destination,
            string name)
        {
            SerializedProperty from = source.FindProperty(name);
            SerializedProperty to = destination.FindProperty(name);
            if (from != null && to != null) to.stringValue = from.stringValue;
        }

        static void CopyInt(SerializedObject source, SerializedObject destination,
            string name)
        {
            SerializedProperty from = source.FindProperty(name);
            SerializedProperty to = destination.FindProperty(name);
            if (from != null && to != null) to.intValue = from.intValue;
        }

        static void CopyFloat(SerializedObject source, SerializedObject destination,
            string name)
        {
            SerializedProperty from = source.FindProperty(name);
            SerializedProperty to = destination.FindProperty(name);
            if (from != null && to != null) to.floatValue = from.floatValue;
        }

        static void CopyBool(SerializedObject source, SerializedObject destination,
            string name)
        {
            SerializedProperty from = source.FindProperty(name);
            SerializedProperty to = destination.FindProperty(name);
            if (from != null && to != null) to.boolValue = from.boolValue;
        }

        static void SetString(SerializedObject target, string name, string value)
        {
            SerializedProperty property = target.FindProperty(name);
            if (property != null) property.stringValue = value;
        }

        static void Show(bool enabled, string message)
        {
            if (enabled) EditorUtility.DisplayDialog("Legacy Gacha Migration", message, "OK");
        }
    }
}
