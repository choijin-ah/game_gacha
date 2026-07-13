using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class CharacterArchiveSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/CharacterArchive.unity";

        static CharacterArchiveSceneBuilder()
        {
            EditorApplication.delayCall += EnsureSceneExists;
        }

        [MenuItem("Starfall/Rebuild/Character Archive Scene")]
        public static void Create()
        {
            if (!SceneBuilderSafety.CanRunManualBuild()) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSceneObjects();
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Character archive scene created: " + ScenePath);
        }

        static void EnsureSceneExists()
        {
            if (File.Exists(ScenePath))
            {
                SceneBuildSettingsUtility.Update();
                return;
            }
            if (!SceneBuilderSafety.TryBegin(ScenePath, EnsureSceneExists)) return;

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
            Debug.Log("[Starfall] Character archive scene created automatically: " + ScenePath);
        }

        static void CreateSceneObjects(Scene? targetScene = null)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LobbyTheme.Hex("09090D");
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var entry = new GameObject("Character Archive Scene Entry", typeof(CharacterArchiveSceneEntry));
            if (!targetScene.HasValue) return;
            SceneManager.MoveGameObjectToScene(cameraObject, targetScene.Value);
            SceneManager.MoveGameObjectToScene(entry, targetScene.Value);
        }
    }
}
