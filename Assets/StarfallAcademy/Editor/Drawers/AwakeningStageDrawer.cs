using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomPropertyDrawer(typeof(AwakeningStageDefinition))]
    public sealed class AwakeningStageDrawer : PropertyDrawer
    {
        const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;
            float height = line + Spacing;
            height += ChildHeight(property, "requiredFragments");
            height += ChildHeight(property, "statModifiers");
            height += ChildHeight(property, "skillEffectChanges");
            height += ChildHeight(property, "description");
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                row.y += line + Spacing;
                DrawChild(ref row, property, "requiredFragments", "Required Fragments");
                DrawChild(ref row, property, "statModifiers", "Stat Modifiers");
                DrawChild(ref row, property, "skillEffectChanges", "Skill Effect Changes");
                DrawChild(ref row, property, "description", "Description");
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        static float ChildHeight(SerializedProperty property, string name)
        {
            SerializedProperty child = property.FindPropertyRelative(name);
            return (child == null ? 0f : EditorGUI.GetPropertyHeight(child, true)) + Spacing;
        }

        static void DrawChild(ref Rect row, SerializedProperty property, string name, string label)
        {
            SerializedProperty child = property.FindPropertyRelative(name);
            if (child == null) return;
            float height = EditorGUI.GetPropertyHeight(child, true);
            EditorGUI.PropertyField(new Rect(row.x, row.y, row.width, height), child,
                new GUIContent(label), true);
            row.y += height + Spacing;
        }
    }
}
