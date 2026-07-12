using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class FormationSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/Formation.unity";

        static FormationSceneBuilder()
        {
            EditorApplication.delayCall += EnsureSceneExists;
        }

        [MenuItem("Starfall/Rebuild Formation Scene")]
        public static void Create()
        {
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

            new GameObject("Formation Scene Entry", typeof(FormationSceneEntry));
            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Formation scene created: " + ScenePath);
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

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LobbyTheme.Hex("10172D");
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            var entry = new GameObject("Formation Scene Entry", typeof(FormationSceneEntry));
            SceneManager.MoveGameObjectToScene(entry, scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall] Formation scene created automatically: " + ScenePath);
        }
    }

    static class SceneBuildSettingsUtility
    {
        internal static void Update()
        {
            string[] corePaths =
            {
                LobbySceneBuilder.ScenePath,
                FormationSceneBuilder.ScenePath,
                GachaSceneBuilder.ScenePath,
                ShopSceneBuilder.ScenePath,
                CharacterArchiveSceneBuilder.ScenePath,
                StageSelectSceneBuilder.ScenePath,
                TurnBattleSceneBuilder.ScenePath
            };
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in corePaths)
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                bool isCore = false;
                foreach (string corePath in corePaths)
                    if (existing.path == corePath) { isCore = true; break; }
                if (!isCore && File.Exists(existing.path)) scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
