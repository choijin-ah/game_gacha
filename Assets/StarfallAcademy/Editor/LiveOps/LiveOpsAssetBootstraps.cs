using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class AttendanceCampaignDatabaseBootstrap
    {
        internal const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/AttendanceCampaignDatabase.asset";
        internal const string CampaignFolder =
            "Assets/StarfallAcademy/Data/LiveOps/Attendance";

        internal static AttendanceCampaignDatabase LoadExisting() =>
            AssetDatabase.LoadAssetAtPath<AttendanceCampaignDatabase>(DatabasePath);

        internal static AttendanceCampaignDatabase LoadOrCreate()
        {
            AttendanceCampaignDatabase database = LoadExisting();
            if (database != null) return database;
            EnsureFolders(DatabasePath, CampaignFolder);
            // Create the referenced content first. If creation is interrupted, a later retry can
            // still build the database instead of mistaking a partial empty database for an
            // intentionally empty user asset.
            AttendanceCampaignData campaign = CreateCampaign(false,
                "attendance_welcome_7day");
            database = ScriptableObject.CreateInstance<AttendanceCampaignDatabase>();
            database.Add(campaign);
            AssetDatabase.CreateAsset(database, DatabasePath);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        internal static AttendanceCampaignData CreateCampaign(bool registerUndo = true,
            string fixedId = null)
        {
            EnsureFolders(DatabasePath, CampaignFolder);
            AttendanceCampaignData campaign = ScriptableObject.CreateInstance<AttendanceCampaignData>();
            string token = Guid.NewGuid().ToString("N").Substring(0, 8);
            var serialized = new SerializedObject(campaign);
            serialized.FindProperty("campaignId").stringValue = string.IsNullOrWhiteSpace(fixedId)
                ? "attendance_" + token : fixedId.Trim();
            serialized.FindProperty("displayName").stringValue = "7일 출석 캠페인";
            SerializedProperty days = serialized.FindProperty("days");
            days.arraySize = 7;
            for (int i = 0; i < days.arraySize; i++)
            {
                SerializedProperty day = days.GetArrayElementAtIndex(i);
                day.FindPropertyRelative("dayNumber").intValue = i + 1;
                ResetReward(day.FindPropertyRelative("reward"));
                SerializedProperty currency = day.FindPropertyRelative("reward")
                    .FindPropertyRelative("currencyReward");
                currency.FindPropertyRelative("credits").intValue = (i + 1) * 1000;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                CampaignFolder + "/AttendanceCampaign.asset");
            AssetDatabase.CreateAsset(campaign, path);
            if (registerUndo)
                Undo.RegisterCreatedObjectUndo(campaign, "Create attendance campaign");
            return campaign;
        }

        internal static AttendanceCampaignData DuplicateCampaign(AttendanceCampaignData source)
        {
            if (source == null) return null;
            EnsureFolders(DatabasePath, CampaignFolder);
            AttendanceCampaignData copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " Copy";
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("campaignId").stringValue = source.CampaignId
                + "_copy_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            serialized.FindProperty("displayName").stringValue = source.DisplayName + " 복사본";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                CampaignFolder + "/" + source.name + " Copy.asset");
            AssetDatabase.CreateAsset(copy, path);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate attendance campaign");
            return copy;
        }

        static void ResetReward(SerializedProperty reward)
        {
            if (reward == null) return;
            SerializedProperty currency = reward.FindPropertyRelative("currencyReward");
            string[] names = { "credits", "skillMaterials", "accountExperience", "premiumCurrency" };
            for (int i = 0; i < names.Length; i++)
                currency.FindPropertyRelative(names[i]).intValue = 0;
            reward.FindPropertyRelative("itemRewards").arraySize = 0;
        }

        internal static void EnsureFolders(string databasePath, string contentFolder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath));
            Directory.CreateDirectory(contentFolder);
            AssetDatabase.Refresh();
        }
    }

    internal static class MailTemplateDatabaseBootstrap
    {
        internal const string DatabasePath =
            "Assets/StarfallAcademy/Resources/Data/MailTemplateDatabase.asset";
        internal const string TemplateFolder =
            "Assets/StarfallAcademy/Data/LiveOps/Mail";

        internal static MailTemplateDatabase LoadExisting() =>
            AssetDatabase.LoadAssetAtPath<MailTemplateDatabase>(DatabasePath);

        internal static MailTemplateDatabase LoadOrCreate()
        {
            MailTemplateDatabase database = LoadExisting();
            if (database != null) return database;
            AttendanceCampaignDatabaseBootstrap.EnsureFolders(DatabasePath, TemplateFolder);
            MailTemplateData template = CreateTemplate(false, "mail_welcome");
            database = ScriptableObject.CreateInstance<MailTemplateDatabase>();
            database.Add(template);
            AssetDatabase.CreateAsset(database, DatabasePath);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        internal static MailTemplateData CreateTemplate(bool registerUndo = true,
            string fixedId = null)
        {
            AttendanceCampaignDatabaseBootstrap.EnsureFolders(DatabasePath, TemplateFolder);
            MailTemplateData template = ScriptableObject.CreateInstance<MailTemplateData>();
            string token = Guid.NewGuid().ToString("N").Substring(0, 8);
            var serialized = new SerializedObject(template);
            serialized.FindProperty("templateId").stringValue = string.IsNullOrWhiteSpace(fixedId)
                ? "mail_" + token : fixedId.Trim();
            serialized.FindProperty("title").stringValue = "운영팀 선물";
            serialized.FindProperty("body").stringValue = "첨부된 보상을 받아 주세요.";
            serialized.FindProperty("sender").stringValue = "Starfall Academy";
            serialized.FindProperty("defaultExpiryHours").intValue = 168;
            if (!string.IsNullOrWhiteSpace(fixedId))
                serialized.FindProperty("attachments").FindPropertyRelative("currencyReward")
                    .FindPropertyRelative("credits").intValue = 1000;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                TemplateFolder + "/MailTemplate.asset");
            AssetDatabase.CreateAsset(template, path);
            if (registerUndo)
                Undo.RegisterCreatedObjectUndo(template, "Create mail template");
            return template;
        }

        internal static MailTemplateData DuplicateTemplate(MailTemplateData source)
        {
            if (source == null) return null;
            AttendanceCampaignDatabaseBootstrap.EnsureFolders(DatabasePath, TemplateFolder);
            MailTemplateData copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " Copy";
            var serialized = new SerializedObject(copy);
            serialized.FindProperty("templateId").stringValue = source.TemplateId
                + "_copy_" + Guid.NewGuid().ToString("N").Substring(0, 4);
            serialized.FindProperty("title").stringValue = source.Title + " 복사본";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string path = AssetDatabase.GenerateUniqueAssetPath(
                TemplateFolder + "/" + source.name + " Copy.asset");
            AssetDatabase.CreateAsset(copy, path);
            Undo.RegisterCreatedObjectUndo(copy, "Duplicate mail template");
            return copy;
        }
    }

    /// <summary>
    /// Makes a clean checkout immediately playable without requiring the user to discover and
    /// open each LiveOps window first. Existing databases, including intentionally empty ones,
    /// are never modified.
    /// </summary>
    [InitializeOnLoad]
    internal static class LiveOpsDefaultContentBootstrap
    {
        static bool queued;

        static LiveOpsDefaultContentBootstrap() => Queue();

        static void Queue()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += EnsureDefaults;
        }

        static void EnsureDefaults()
        {
            queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Queue();
                return;
            }

            try
            {
                AttendanceCampaignDatabaseBootstrap.LoadOrCreate();
                MailTemplateDatabaseBootstrap.LoadOrCreate();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Starfall LiveOps] Could not create default content: "
                    + exception);
            }
        }
    }
}
