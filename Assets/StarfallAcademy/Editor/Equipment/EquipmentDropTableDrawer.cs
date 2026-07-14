using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomEditor(typeof(EquipmentDropTable))]
    public sealed class EquipmentDropTableDrawer : UnityEditor.Editor
    {
        int seed = 100;
        string preview;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();
            EquipmentDropTable table = (EquipmentDropTable)target;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Drop Preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Total weight", table.TotalWeight.ToString("0.###"));
            seed = EditorGUILayout.IntField("Seed", seed);
            if (GUILayout.Button("Simulate 10,000 rolls"))
            {
                var random = new System.Random(seed);
                var counts = new Dictionary<string, int>();
                for (int i = 0; i < 10000; i++)
                {
                    EquipmentDefinition result = table.Roll(random);
                    string id = result != null ? result.Id : "<none>";
                    counts.TryGetValue(id, out int count);
                    counts[id] = count + 1;
                }
                preview = string.Join("\n", counts.OrderByDescending(pair => pair.Value)
                    .Select(pair => pair.Key + "  " + (pair.Value / 100f).ToString("0.00") + "%"));
            }
            if (!string.IsNullOrEmpty(preview)) EditorGUILayout.HelpBox(preview, MessageType.Info);
        }
    }
}
