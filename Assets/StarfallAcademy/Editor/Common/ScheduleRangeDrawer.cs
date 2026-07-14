using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomPropertyDrawer(typeof(ScheduleRange))]
    public sealed class ScheduleRangeDrawer : PropertyDrawer
    {
        const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            return property.isExpanded ? line * 5f + Spacing * 4f : line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            SerializedProperty start = property.FindPropertyRelative("startUtc");
            SerializedProperty end = property.FindPropertyRelative("endUtc");
            EditorGUI.indentLevel++;
            row.y += line + Spacing;
            EditorGUI.PropertyField(row, start, new GUIContent("Start UTC"));
            row.y += line + Spacing;
            EditorGUI.PropertyField(row, end, new GUIContent("End UTC"));
            row.y += line + Spacing;

            DateTime? startUtc;
            DateTime? endUtc;
            bool startValid = TryParse(start?.stringValue, out startUtc);
            bool endValid = TryParse(end?.stringValue, out endUtc);
            bool valid = startValid && endValid
                && (!startUtc.HasValue || !endUtc.HasValue || endUtc.Value > startUtc.Value);
            string duration = startUtc.HasValue && endUtc.HasValue && valid
                ? FormatDuration(endUtc.Value - startUtc.Value) : "Open-ended";
            EditorGUI.LabelField(row, "Duration", duration);
            row.y += line + Spacing;

            ScheduleState state = ResolveState(valid, startUtc, endUtc, ContentTime.UtcNow);
            MessageType type = !valid ? MessageType.Error
                : state == ScheduleState.Active ? MessageType.Info : MessageType.None;
            EditorGUI.HelpBox(row, valid ? "Status: " + StateLabel(state) :
                "Invalid range: use ISO-8601 UTC and ensure the end is after the start.", type);
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        static bool TryParse(string value, out DateTime? parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(value)) return true;
            if (!DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime result)) return false;
            parsed = ScheduleRange.NormalizeUtc(result);
            return true;
        }

        static ScheduleState ResolveState(bool valid, DateTime? start, DateTime? end,
            DateTime now)
        {
            if (!valid) return ScheduleState.Invalid;
            if (start.HasValue && now < start.Value) return ScheduleState.Upcoming;
            if (end.HasValue && now >= end.Value) return ScheduleState.Ended;
            return ScheduleState.Active;
        }

        static string StateLabel(ScheduleState state)
        {
            switch (state)
            {
                case ScheduleState.Upcoming: return "Upcoming";
                case ScheduleState.Active: return "Active";
                case ScheduleState.Ended: return "Ended";
                default: return "Invalid";
            }
        }

        static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalDays >= 1d)
                return duration.TotalDays.ToString("0.##", CultureInfo.InvariantCulture) + " days";
            if (duration.TotalHours >= 1d)
                return duration.TotalHours.ToString("0.##", CultureInfo.InvariantCulture) + " hours";
            return duration.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture) + " minutes";
        }
    }
}
