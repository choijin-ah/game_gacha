using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    public sealed class TimeSimulatorWindow : EditorWindow
    {
        string input;

        [MenuItem("Starfall/Debug/Time Simulator")]
        public static void Open()
        {
            var window = GetWindow<TimeSimulatorWindow>("Time Simulator");
            window.minSize = new Vector2(520, 260);
            window.Show();
        }

        void OnEnable() => input = ContentTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("STARFALL UTC TIME SIMULATOR", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Editor and development builds only. All scheduled content, stamina and daily missions use this shared UTC clock.",
                MessageType.Info);
            EditorGUILayout.LabelField("System UTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            EditorGUILayout.LabelField("Runtime UTC", ContentTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
            EditorGUILayout.LabelField("Mode", ContentTime.IsOverridden ? "TEST OVERRIDE" : "SYSTEM TIME");
            EditorGUILayout.Space(6);
            input = EditorGUILayout.TextField("Test UTC (ISO-8601)", input ?? string.Empty);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply")) ApplyInput();
            if (GUILayout.Button("+ 1 Day")) Advance(TimeSpan.FromDays(1));
            if (GUILayout.Button("+ 1 Week")) Advance(TimeSpan.FromDays(7));
            if (GUILayout.Button("Restore System Time"))
            {
                ContentTime.ClearOverride();
                input = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }

        void ApplyInput()
        {
            if (!DateTime.TryParse(input, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime value))
            {
                EditorUtility.DisplayDialog("Time Simulator", "Enter a valid ISO-8601 UTC value.", "OK");
                return;
            }
            ContentTime.TrySetOverride(value);
            input = ContentTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Repaint();
        }

        void Advance(TimeSpan duration)
        {
            ContentTime.TrySetOverride(ContentTime.UtcNow.Add(duration));
            input = ContentTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            Repaint();
        }
    }
}
