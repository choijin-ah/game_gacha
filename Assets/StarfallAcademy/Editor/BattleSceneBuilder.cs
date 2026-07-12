using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StarfallAcademy.Lobby.Editor
{
    [InitializeOnLoad]
    public static class StageSelectSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/StageSelect.unity";
        static StageSelectSceneBuilder() => EditorApplication.delayCall += Ensure;

        [MenuItem("Starfall Academy/Rebuild Stage Select Scene")]
        public static void Create() => BattleSceneBuilderUtility.CreateScene<StageSelectSceneEntry>(
            ScenePath, "Stage Select Scene Entry");

        static void Ensure() => BattleSceneBuilderUtility.EnsureScene<StageSelectSceneEntry>(
            ScenePath, "Stage Select Scene Entry");
    }

    [InitializeOnLoad]
    public static class TurnBattleSceneBuilder
    {
        public const string ScenePath = "Assets/StarfallAcademy/Scenes/TurnBattle.unity";
        static TurnBattleSceneBuilder() => EditorApplication.delayCall += Ensure;

        [MenuItem("Starfall Academy/Rebuild Turn Battle Scene")]
        public static void Create() => BattleSceneBuilderUtility.CreateScene<TurnBattleSceneEntry>(
            ScenePath, "Turn Battle Scene Entry");

        static void Ensure() => BattleSceneBuilderUtility.EnsureScene<TurnBattleSceneEntry>(
            ScenePath, "Turn Battle Scene Entry");
    }

    static class BattleSceneBuilderUtility
    {
        internal static void CreateScene<T>(string path, string entryName) where T : Component
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateObjects<T>(scene, entryName);
            EditorSceneManager.SaveScene(scene, path);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Starfall Academy] Battle scene created: " + path);
        }

        internal static void EnsureScene<T>(string path, string entryName) where T : Component
        {
            if (File.Exists(path))
            {
                SceneBuildSettingsUtility.Update();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += () => EnsureScene<T>(path, entryName);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateObjects<T>(scene, entryName);
            EditorSceneManager.SaveScene(scene, path);
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            SceneBuildSettingsUtility.Update();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void CreateObjects<T>(Scene scene, string entryName) where T : Component
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = LobbyTheme.Hex("08080C");
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var entry = new GameObject(entryName, typeof(T));
            SceneManager.MoveGameObjectToScene(entry, scene);
        }
    }
}
