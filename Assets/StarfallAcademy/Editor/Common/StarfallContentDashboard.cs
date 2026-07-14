using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class StarfallContentDashboard : EditorWindow
    {
        Vector2 scroll;

        [MenuItem("Starfall/Content Dashboard", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<StarfallContentDashboard>("Content Dashboard");
            window.minSize = new Vector2(620, 520);
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("STARFALL CONTENT DASHBOARD", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create, edit, validate and rebuild project content from one place.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSection("Core Data", new[]
            {
                "Starfall/Data/Character Database", "Starfall/Data/Stage Database",
                "Starfall/Data/Equipment Database", "Starfall/Data/Gacha Banner Database",
                "Starfall/Data/Formation Settings"
            });
            DrawSection("Modes", new[]
            {
                "Starfall/Data/Weekly Boss Database", "Starfall/Data/Challenge Tower Database"
            });
            DrawSection("LiveOps", new[]
            {
                "Starfall/LiveOps/Attendance Calendar", "Starfall/LiveOps/Mail Templates",
                "Starfall/LiveOps/Send Test Mail"
            });
            DrawSection("UI & Scenes", new[]
            {
                "Starfall/UI/Style Guide", "Starfall/Rebuild/Lobby Scene",
                "Starfall/Rebuild/Formation Scene", "Starfall/Rebuild/Character Archive Scene",
                "Starfall/Rebuild/Gacha Scene", "Starfall/Rebuild/Shop Scene",
                "Starfall/Rebuild/Story Archive Scene", "Starfall/Rebuild/Stage Select Scene",
                "Starfall/Rebuild/Turn Battle Scene", "Starfall/Rebuild/Weekly Boss Scene",
                "Starfall/Rebuild/Challenge Tower Scene"
            });
            DrawSection("Debug & Validation", new[]
            {
                "Starfall/Validate/All Content", "Starfall/Debug/Player Data Viewer",
                "Starfall/Debug/Time Simulator", "Starfall/Diagnostics/Meta Core Diagnostics",
                "Starfall/Diagnostics/Battle Core Smoke Test",
                "Starfall/Diagnostics/Weekly Boss Diagnostics",
                "Starfall/Diagnostics/Challenge Tower Diagnostics"
            });
            EditorGUILayout.EndScrollView();
        }

        static void DrawSection(string title, string[] menuPaths)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            for (int i = 0; i < menuPaths.Length; i++)
            {
                string path = menuPaths[i];
                string label = path.Split('/').Last();
                if (GUILayout.Button(label, GUILayout.Height(25)) && !EditorApplication.ExecuteMenuItem(path))
                    Debug.LogWarning("[Starfall] Menu is not available yet: " + path);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }
}
