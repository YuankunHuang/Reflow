using UnityEditor;
using UnityEngine;

namespace Reflow.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflowElement))]
    internal sealed class ReflowElementEditor : UnityEditor.Editor
    {
        private SerializedProperty _ignoreLayoutProperty;
        private SerializedProperty _sizeSourceProperty;
        private SerializedProperty _manualSizeProperty;
        private SerializedProperty _layoutControlsWidthProperty;
        private SerializedProperty _layoutControlsHeightProperty;
        private SerializedProperty _flexibleWidthProperty;
        private SerializedProperty _flexibleHeightProperty;
        private SerializedProperty _minWidthProperty;
        private SerializedProperty _maxWidthProperty;
        private SerializedProperty _minHeightProperty;
        private SerializedProperty _maxHeightProperty;

        private void OnEnable()
        {
            _ignoreLayoutProperty = serializedObject.FindProperty("_ignoreLayout");
            _sizeSourceProperty = serializedObject.FindProperty("_sizeSource");
            _manualSizeProperty = serializedObject.FindProperty("_manualSize");
            _layoutControlsWidthProperty = serializedObject.FindProperty("_layoutControlsWidth");
            _layoutControlsHeightProperty = serializedObject.FindProperty("_layoutControlsHeight");
            _flexibleWidthProperty = serializedObject.FindProperty("_flexibleWidth");
            _flexibleHeightProperty = serializedObject.FindProperty("_flexibleHeight");
            _minWidthProperty = serializedObject.FindProperty("_minWidth");
            _maxWidthProperty = serializedObject.FindProperty("_maxWidth");
            _minHeightProperty = serializedObject.FindProperty("_minHeight");
            _maxHeightProperty = serializedObject.FindProperty("_maxHeight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_ignoreLayoutProperty);
            if (!_ignoreLayoutProperty.boolValue || _ignoreLayoutProperty.hasMultipleDifferentValues)
            {
                EditorGUILayout.PropertyField(_sizeSourceProperty);
                if (_sizeSourceProperty.enumValueIndex == (int)ReflowElement.SizeSource.Manual)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_manualSizeProperty);
                    EditorGUI.indentLevel--;
                }

                DrawAxis("Width", _layoutControlsWidthProperty, _flexibleWidthProperty, _minWidthProperty, _maxWidthProperty);
                DrawAxis("Height", _layoutControlsHeightProperty, _flexibleHeightProperty, _minHeightProperty, _maxHeightProperty);
            }

            serializedObject.ApplyModifiedProperties();

            ReflowElement element = (ReflowElement)target;
            if (targets.Length == 1 && element.transform.parent != null && element.transform.parent.GetComponent<ReflowLayout>() == null)
                EditorGUILayout.HelpBox("The parent has no Reflow layout, so these settings have no effect here.", MessageType.Info);
        }

        private static void DrawAxis(string pName, SerializedProperty pControls, SerializedProperty pFlexible, SerializedProperty pMin, SerializedProperty pMax)
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField(pName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(pControls, new GUIContent("Layout Controls", pControls.tooltip));
            using (new EditorGUI.DisabledScope(!pControls.boolValue && !pControls.hasMultipleDifferentValues))
            {
                EditorGUILayout.PropertyField(pFlexible, new GUIContent("Flexible", pFlexible.tooltip));
                EditorGUILayout.PropertyField(pMin, new GUIContent("Min", pMin.tooltip));
                EditorGUILayout.PropertyField(pMax, new GUIContent("Max", pMax.tooltip));
            }
            if (pMin.floatValue > 0f && pMax.floatValue > 0f && pMin.floatValue > pMax.floatValue)
                EditorGUILayout.HelpBox("Min is larger than max; min wins.", MessageType.Info);
            EditorGUI.indentLevel--;
        }
    }
}
