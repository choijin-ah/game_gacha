using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class GachaSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/Gacha.unity";

        static GachaSceneBuilder()
        {
            EditorApplication.delayCall += EnsureSceneExists;
        }

        [MenuItem("Starfall/Rebuild Gacha Scene")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneObjects(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Gacha scene created: " + ScenePath);
        }

        static void EnsureSceneExists()
        {
            if (File.Exists(ScenePath))
            {
                SceneBuildSettingsUtility.Update();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += EnsureSceneExists;
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateSceneObjects(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Gacha scene created automatically: " + ScenePath);
        }

        static void CreateSceneObjects(Scene scene)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LobbyTheme.Hex("081126");
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var entry = new GameObject("Gacha Scene Entry", typeof(GachaSceneEntry));
            SceneManager.MoveGameObjectToScene(entry, scene);
        }
    }
}
