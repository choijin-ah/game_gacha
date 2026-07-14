using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    internal static class SaveDataVersionMigration
    {
        [MenuItem("Starfall/Migrate/Save Data Version")]
        static void Run()
        {
            int before = PlayerSaveVersion.StoredVersion;
            if (!EditorUtility.DisplayDialog("Save Data Version",
                "플레이어 저장 데이터 버전을 v" + before + "에서 지원 버전 v"
                + PlayerSaveVersion.LatestVersion + "으로 검사·마이그레이션할까요?\n\n"
                + "기능별 데이터는 멱등 변환되며 PlayerPrefs.DeleteAll을 사용하지 않습니다.",
                "검사 및 마이그레이션", "취소")) return;

            bool succeeded = PlayerSaveVersion.TryMigrate(out string report);
            Debug.Log("[Starfall Save] " + report);
            EditorUtility.DisplayDialog("Save Data Version",
                report, succeeded ? "확인" : "오류 확인");
        }

        [MenuItem("Starfall/Migrate/Save Data Version", true)]
        static bool ValidateRun() => !EditorApplication.isCompiling;
    }
}
