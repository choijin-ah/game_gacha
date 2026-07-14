using System;
using UnityEngine;

namespace StarfallAcademy.Lobby
{
    public static class PlayerSaveVersion
    {
        public const string VersionKey = "StarfallAcademy.Save.Version";
        public const string LastMigrationUtcKey = "StarfallAcademy.Save.LastMigrationUtc";
        public const int LatestVersion = 2;

        static readonly object SyncRoot = new object();

        public static int StoredVersion => Mathf.Max(0,
            PlayerPrefs.GetInt(VersionKey, 0));

        public static string LastMigrationUtc =>
            PlayerPrefs.GetString(LastMigrationUtcKey, string.Empty);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoMigrate()
        {
            if (!TryMigrate(out string report))
                Debug.LogError("[Starfall Save] " + report);
        }

        public static bool TryMigrate(out string report)
        {
            lock (SyncRoot)
            {
                int version = StoredVersion;
                if (version > LatestVersion)
                {
                    report = "저장 데이터 버전 " + version + "은 현재 클라이언트 버전 "
                        + LatestVersion + "보다 최신입니다. 다운그레이드하지 않았습니다.";
                    return false;
                }

                if (version == LatestVersion)
                {
                    report = "저장 데이터가 이미 최신 버전 v" + LatestVersion + "입니다.";
                    return true;
                }

                bool formationMigrated = false;
                bool hadVersion = PlayerPrefs.HasKey(VersionKey);
                bool hadMigrationTime = PlayerPrefs.HasKey(LastMigrationUtcKey);
                int previousVersion = PlayerPrefs.GetInt(VersionKey, 0);
                string previousMigrationTime = PlayerPrefs.GetString(LastMigrationUtcKey,
                    string.Empty);
                try
                {
                    while (version < LatestVersion)
                    {
                        switch (version)
                        {
                            case 0:
                                formationMigrated = FormationPresetService.Default
                                    .MigrateLegacyIfNeeded();
                                version = 1;
                                break;
                            case 1:
                                // v2 introduces versioned LiveOps, equipment inventory,
                                // multi-banner gacha history, and special-mode progress.
                                // Each service already treats a missing v2 key as an empty save.
                                version = 2;
                                break;
                            default:
                                throw new InvalidOperationException(
                                    "지원하지 않는 저장 마이그레이션 단계입니다: " + version);
                        }
                        PlayerPrefs.SetInt(VersionKey, version);
                    }
                    string migratedAt = DateTime.UtcNow.ToString("O");
                    PlayerPrefs.SetString(LastMigrationUtcKey, migratedAt);
                    PlayerPrefs.Save();
                    report = "저장 데이터 v" + version + " 준비 완료"
                        + (formationMigrated ? " · 레거시 편성 변환" : string.Empty);
                    return true;
                }
                catch (Exception exception)
                {
                    if (hadVersion) PlayerPrefs.SetInt(VersionKey, previousVersion);
                    else PlayerPrefs.DeleteKey(VersionKey);
                    if (hadMigrationTime)
                        PlayerPrefs.SetString(LastMigrationUtcKey, previousMigrationTime);
                    else PlayerPrefs.DeleteKey(LastMigrationUtcKey);
                    try { PlayerPrefs.Save(); } catch (Exception) { }
                    report = "저장 데이터 마이그레이션 실패: " + exception.Message;
                    return false;
                }
            }
        }

        public static void ResetVersionMarker()
        {
            PlayerPrefs.DeleteKey(VersionKey);
            PlayerPrefs.DeleteKey(LastMigrationUtcKey);
            PlayerPrefs.Save();
        }
    }
}
