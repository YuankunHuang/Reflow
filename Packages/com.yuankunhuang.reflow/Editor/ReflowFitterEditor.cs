using TMPro;
using UnityEditor;
using UnityEngine;

namespace Reflow.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflowFitter))]
    internal sealed class ReflowFitterEditor : UnityEditor.Editor
    {
        private SerializedProperty _horizontalFitProperty;
        private SerializedProperty _verticalFitProperty;
        private SerializedProperty _paddingProperty;
        private SerializedProperty _minSizeProperty;
        private SerializedProperty _maxSizeProperty;
        private SerializedProperty _sourceProperty;
        private SerializedProperty _textProperty;
        private SerializedProperty _renderedVisibleOnlyProperty;

        private void OnEnable()
        {
            _horizontalFitProperty = serializedObject.FindProperty("_horizontalFit");
            _verticalFitProperty = serializedObject.FindProperty("_verticalFit");
            _paddingProperty = serializedObject.FindProperty("_padding");
            _minSizeProperty = serializedObject.FindProperty("_minSize");
            _maxSizeProperty = serializedObject.FindProperty("_maxSize");
            _sourceProperty = serializedObject.FindProperty("_source");
            _textProperty = serializedObject.FindProperty("_text");
            _renderedVisibleOnlyProperty = serializedObject.FindProperty("_renderedVisibleOnly");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_horizontalFitProperty);
            EditorGUILayout.PropertyField(_verticalFitProperty);
            EditorGUILayout.PropertyField(_paddingProperty, true);
            EditorGUILayout.PropertyField(_minSizeProperty);
            EditorGUILayout.PropertyField(_maxSizeProperty);
            EditorGUILayout.PropertyField(_sourceProperty);
            using (new EditorGUI.DisabledScope(_sourceProperty.objectReferenceValue != null))
                EditorGUILayout.PropertyField(_textProperty);
            bool usesRendered = _horizontalFitProperty.enumValueIndex == (int)ReflowFitter.FitMode.Rendered ||
                                _verticalFitProperty.enumValueIndex == (int)ReflowFitter.FitMode.Rendered;
            if (usesRendered)
                EditorGUILayout.PropertyField(_renderedVisibleOnlyProperty);

            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
                DrawWarnings((ReflowFitter)target);
        }

        private static void DrawWarnings(ReflowFitter pFitter)
        {
            ReflowFitter source = pFitter.Source;
            if (source == pFitter)
                EditorGUILayout.HelpBox("Source is this fitter itself; it is ignored.", MessageType.Error);
            else if (source == null && pFitter.Text == null && pFitter.GetComponent<TMP_Text>() == null && pFitter.GetComponentInChildren<TMP_Text>() == null)
                EditorGUILayout.HelpBox("No TMP text on this object or below it, and no source: the size stays as it is.", MessageType.Warning);

            if (pFitter.HorizontalFit == ReflowFitter.FitMode.Unconstrained && pFitter.VerticalFit == ReflowFitter.FitMode.Unconstrained)
                EditorGUILayout.HelpBox("Both axes are Unconstrained, so the fitter does nothing.", MessageType.Info);

            RectTransform rectTransform = pFitter.RectTransform;
            for (int axis = 0; axis < 2; axis++)
            {
                bool fits = pFitter.FollowsContent(axis);
                bool stretched = !Mathf.Approximately(ReflowUtil.Get(rectTransform.anchorMin, axis), ReflowUtil.Get(rectTransform.anchorMax, axis));
                if (fits && stretched && pFitter.ParentLayout == null)
                    EditorGUILayout.HelpBox($"{(axis == 0 ? "Width" : "Height")} is fitted but the anchors stretch on this axis. Use non-stretched anchors.", MessageType.Warning);
            }
        }
    }
}
