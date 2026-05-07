// --------------------------------------------------------------
// Copyright 2024 CyberAgent, Inc.
// --------------------------------------------------------------

using Nova.Editor.Core.Scripts;

namespace Nova.Editor.Core
{
    using UnityEditor;
    using UnityEngine;

    [CustomPropertyDrawer(typeof(EnumDisplayNameAttribute))]
    public class EnumDisplayNameDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EnumDisplayNameAttribute displayNameAttribute = (EnumDisplayNameAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Enum)
            {
                EditorGUI.BeginProperty(position, label, property);

                int index = property.enumValueIndex;
                string[] displayNames = displayNameAttribute.DisplayNames;
                string[] enumNames = property.enumDisplayNames;

                if (displayNames.Length != enumNames.Length)
                {
                    Debug.LogError("EnumDisplayNameAttribute: Display names count does not match enum names count.");
                    EditorGUI.PropertyField(position, property, label);
                    return;
                }

                index = EditorGUI.Popup(position, label.text, index, displayNames);
                property.enumValueIndex = index;

                EditorGUI.EndProperty();
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use EnumDisplayName with Enum.");
            }
        }
    }

}