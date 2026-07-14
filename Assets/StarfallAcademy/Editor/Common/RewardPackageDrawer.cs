using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StarfallAcademy.Lobby.Editor
{
    [CustomPropertyDrawer(typeof(RewardPackage))]
    public sealed class RewardPackageDrawer : PropertyDrawer
    {
        const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return line;

            SerializedProperty currency = property.FindPropertyRelative("currencyReward");
            SerializedProperty items = property.FindPropertyRelative("itemRewards");
            float height = line + Spacing;
            height += EditorGUI.GetPropertyHeight(currency, true) + Spacing;
            height += EditorGUI.GetPropertyHeight(items, true) + Spacing;
            height += line * 2f + Spacing * 2f;
            return height;
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

            EditorGUI.indentLevel++;
            SerializedProperty currency = property.FindPropertyRelative("currencyReward");
            SerializedProperty items = property.FindPropertyRelative("itemRewards");

            row.y += line + Spacing;
            float currencyHeight = EditorGUI.GetPropertyHeight(currency, true);
            EditorGUI.PropertyField(new Rect(row.x, row.y, row.width, currencyHeight), currency,
                new GUIContent("Currency"), true);
            ClampCurrency(currency);

            row.y += currencyHeight + Spacing;
            float itemHeight = EditorGUI.GetPropertyHeight(items, true);
            EditorGUI.PropertyField(new Rect(row.x, row.y, row.width, itemHeight), items,
                new GUIContent("Item Rewards"), true);
            ClampItems(items);

            row.y += itemHeight + Spacing;
            string warning = GetWarning(items);
            MessageType type = string.IsNullOrEmpty(warning) ? MessageType.Info : MessageType.Warning;
            EditorGUI.HelpBox(new Rect(row.x, row.y, row.width, line),
                string.IsNullOrEmpty(warning) ? BuildSummary(currency, items) : warning, type);

            row.y += line + Spacing;
            if (GUI.Button(new Rect(row.x, row.y, 135f, line), "Select known item..."))
                ShowItemMenu(items);
            EditorGUI.LabelField(new Rect(row.x + 143f, row.y, row.width - 143f, line),
                "Use this after adding an item row.", EditorStyles.miniLabel);
            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        static void ClampCurrency(SerializedProperty currency)
        {
            if (currency == null) return;
            string[] names = { "credits", "skillMaterials", "accountExperience", "premiumCurrency" };
            for (int i = 0; i < names.Length; i++)
            {
                SerializedProperty value = currency.FindPropertyRelative(names[i]);
                if (value != null && value.intValue < 0) value.intValue = 0;
            }
        }

        static void ClampItems(SerializedProperty items)
        {
            if (items == null || !items.isArray) return;
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty amount = items.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("amount");
                if (amount != null && amount.intValue < 1) amount.intValue = 1;
            }
        }

        static string GetWarning(SerializedProperty items)
        {
            if (items == null || !items.isArray) return "Item reward list is unavailable.";
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty id = items.GetArrayElementAtIndex(i).FindPropertyRelative("itemId");
                string value = id == null ? string.Empty : id.stringValue.Trim();
                if (string.IsNullOrEmpty(value)) return "Item row " + (i + 1) + " has no item ID.";
                if (!ids.Add(value)) return "The item '" + value + "' is registered more than once.";
            }
            return string.Empty;
        }

        static string BuildSummary(SerializedProperty currency, SerializedProperty items)
        {
            var parts = new List<string>();
            AppendCurrency(parts, currency, "credits", "Credits");
            AppendCurrency(parts, currency, "skillMaterials", "Materials");
            AppendCurrency(parts, currency, "accountExperience", "Account EXP");
            AppendCurrency(parts, currency, "premiumCurrency", "Premium");
            if (items != null && items.isArray)
            {
                for (int i = 0; i < items.arraySize; i++)
                {
                    SerializedProperty element = items.GetArrayElementAtIndex(i);
                    string id = element.FindPropertyRelative("itemId")?.stringValue;
                    int amount = element.FindPropertyRelative("amount")?.intValue ?? 0;
                    if (!string.IsNullOrWhiteSpace(id) && amount > 0) parts.Add(id + " x" + amount);
                }
            }
            return parts.Count == 0 ? "Reward preview: empty" : "Reward preview: " + string.Join(", ", parts);
        }

        static void AppendCurrency(ICollection<string> parts, SerializedProperty currency,
            string propertyName, string label)
        {
            int value = currency?.FindPropertyRelative(propertyName)?.intValue ?? 0;
            if (value > 0) parts.Add(label + " " + value.ToString("N0"));
        }

        static void ShowItemMenu(SerializedProperty items)
        {
            if (items == null || !items.isArray || items.arraySize == 0) return;
            SerializedProperty target = items.GetArrayElementAtIndex(items.arraySize - 1)
                .FindPropertyRelative("itemId");
            if (target == null) return;

            var menu = new GenericMenu();
            string[] builtIns =
            {
                "ticket:recruitment", "material:awakening", "material:enhancement"
            };
            for (int i = 0; i < builtIns.Length; i++)
            {
                string id = builtIns[i];
                menu.AddItem(new GUIContent("Common/" + id), target.stringValue == id,
                    () => SetItemId(target, id));
            }

            string[] characterGuids = AssetDatabase.FindAssets("t:CharacterData");
            for (int i = 0; i < characterGuids.Length; i++)
            {
                CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(
                    AssetDatabase.GUIDToAssetPath(characterGuids[i]));
                if (character == null) continue;
                string id = ItemInventoryService.CharacterFragmentId(character.Id);
                string label = "Character fragments/" + character.DisplayName;
                menu.AddItem(new GUIContent(label), target.stringValue == id,
                    () => SetItemId(target, id));
            }
            menu.ShowAsContext();
        }

        static void SetItemId(SerializedProperty property, string id)
        {
            property.serializedObject.Update();
            property.stringValue = id;
            property.serializedObject.ApplyModifiedProperties();
        }
    }
}
