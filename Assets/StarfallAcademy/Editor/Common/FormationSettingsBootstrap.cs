using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public static class FormationSettingsBootstrap
    {
        const string Path = "Assets/StarfallAcademy/Resources/Data/FormationSettings.asset";

        public static FormationSettings LoadOrCreate()
        {
            FormationSettings settings = AssetDatabase.LoadAssetAtPath<FormationSettings>(Path);
            if (settings != null) return settings;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            settings = ScriptableObject.CreateInstance<FormationSettings>();
            AssetDatabase.CreateAsset(settings, Path);
            AssetDatabase.SaveAssets();
            return settings;
        }

        [MenuItem("Starfall/Data/Formation Settings")]
        static void SelectSettings()
        {
            FormationSettings settings = LoadOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }
    }
}
