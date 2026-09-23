using UnityEditor;
using UnityEngine;

namespace Reflow.Editor
{
    [CustomPropertyDrawer(typeof(ReflowSizePolicy))]
    internal sealed class ReflowSizePolicyDrawer : PropertyDrawer
    {
        private static readonly GUIContent FOLLOW_LABEL = new GUIContent("Follow Content", "The container takes its content's size on this axis.");
        private static readonly GUIContent MIN_LABEL = new GUIContent("Min", "0 = none. Wins over the cap.");
        private static readonly GUIContent MAX_LABEL = new GUIContent("Max", "0 = none. When the content is larger, flexible children share the space and the rest shrink.");
        private static readonly GUIContent SOURCE_LABEL = new GUIContent("Max From Rect", "Dynamic cap: the current size of this rect. The tighter of this and Max wins.");

        public override float GetPropertyHeight(SerializedProperty pProperty, GUIContent pLabel)
        {
            float line = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return pProperty.FindPropertyRelative("follow").boolValue ? line * 3f : line;
        }

        public override void OnGUI(Rect pPosition, SerializedProperty pProperty, GUIContent pLabel)
        {
            SerializedProperty follow = pProperty.FindPropertyRelative("follow");
            float line = EditorGUIUtility.singleLineHeight;
            float step = line + EditorGUIUtility.standardVerticalSpacing;
            Rect row = new Rect(pPosition.x, pPosition.y, pPosition.width, line);

            EditorGUI.BeginProperty(pPosition, pLabel, pProperty);
            follow.boolValue = EditorGUI.ToggleLeft(row, new GUIContent($"{pLabel.text}: {FOLLOW_LABEL.text}", FOLLOW_LABEL.tooltip), follow.boolValue);
            if (follow.boolValue)
            {
                EditorGUI.indentLevel++;
                row.y += step;
                Rect half = new Rect(row.x, row.y, row.width * 0.5f - 2f, line);
                float labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = 60f;
                EditorGUI.PropertyField(half, pProperty.FindPropertyRelative("min"), MIN_LABEL);
                half.x += row.width * 0.5f + 2f;
                EditorGUI.PropertyField(half, pProperty.FindPropertyRelative("max"), MAX_LABEL);
                EditorGUIUtility.labelWidth = labelWidth;
                row.y += step;
                EditorGUI.PropertyField(row, pProperty.FindPropertyRelative("constraintSource"), SOURCE_LABEL);
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
