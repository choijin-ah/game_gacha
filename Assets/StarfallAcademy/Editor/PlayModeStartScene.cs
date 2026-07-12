using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        static PlayModeStartScene()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Starfall Academy/Use Lobby As Play Start")]
        public static void Configure()
        {
            SceneBuildSettingsUtility.Update();
            SceneAsset lobby = AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbySceneBuilder.ScenePath);
            if (lobby == null) return;

            EditorSceneManager.playModeStartScene = lobby;
            Debug.Log("[Starfall Academy] Play Mode start scene: Lobby");
        }
    }
}
