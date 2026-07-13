using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby.Editor
{
    public static class LobbySceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/Lobby.unity";

        [MenuItem("Starfall/Rebuild/Lobby Scene")]
        public static void Create()
        {
            if (!SceneBuilderSafety.CanRunManualBuild()) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LobbyTheme.Hex("10172D");
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);

            new GameObject("Lobby Scene Entry", typeof(LobbySceneEntry));
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Lobby scene created: " + ScenePath);
        }
    }
}
