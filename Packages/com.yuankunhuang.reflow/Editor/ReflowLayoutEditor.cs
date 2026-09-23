using UnityEditor;
using UnityEngine;

namespace Reflow.Editor
{
    /// <summary> Shared inspector for Reflow layouts: subclass settings first, then size, children, virtualization, runtime state. </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflowLayout), true)]
    internal class ReflowLayoutEditor : UnityEditor.Editor
    {
        private bool _showSize = true;
        private bool _showChildren = true;
        private bool _showVirtualization = true;

        private SerializedProperty _paddingProperty;
        private SerializedProperty _widthPolicyProperty;
        private SerializedProperty _heightPolicyProperty;
        private SerializedProperty _includeSceneChildrenProperty;
        private SerializedProperty _defaultChildSizeProperty;
        private SerializedProperty _virtualizationProperty;
        private SerializedProperty _viewportProperty;
        private SerializedProperty _viewportMarginProperty;
        private SerializedProperty _reflowDurationProperty;

        protected virtual void OnEnable()
        {
            _paddingProperty = serializedObject.FindProperty("_padding");
            _widthPolicyProperty = serializedObject.FindProperty("_widthPolicy");
            _heightPolicyProperty = serializedObject.FindProperty("_heightPolicy");
            _includeSceneChildrenProperty = serializedObject.FindProperty("_includeSceneChildren");
            _defaultChildSizeProperty = serializedObject.FindProperty("_defaultChildSize");
            _virtualizationProperty = serializedObject.FindProperty("_virtualization");
            _viewportProperty = serializedObject.FindProperty("_viewport");
            _viewportMarginProperty = serializedObject.FindProperty("_viewportMargin");
            _reflowDurationProperty = serializedObject.FindProperty("_reflowDuration");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_paddingProperty, true);
            DrawLayoutSettings();

            _showSize = EditorGUILayout.BeginFoldoutHeaderGroup(_showSize, "Container Size");
            if (_showSize)
            {
                EditorGUILayout.PropertyField(_widthPolicyProperty, new GUIContent("Width"));
                EditorGUILayout.PropertyField(_heightPolicyProperty, new GUIContent("Height"));
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showChildren = EditorGUILayout.BeginFoldoutHeaderGroup(_showChildren, "Children");
            if (_showChildren)
            {
                EditorGUILayout.PropertyField(_includeSceneChildrenProperty);
                EditorGUILayout.PropertyField(_defaultChildSizeProperty);
                EditorGUILayout.PropertyField(_reflowDurationProperty);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            _showVirtualization = EditorGUILayout.BeginFoldoutHeaderGroup(_showVirtualization, "Virtualization");
            if (_showVirtualization)
            {
                EditorGUILayout.PropertyField(_virtualizationProperty);
                using (new EditorGUI.DisabledScope(_virtualizationProperty.enumValueIndex == (int)ReflowVirtualization.Off))
                {
                    EditorGUILayout.PropertyField(_viewportProperty);
                    EditorGUILayout.PropertyField(_viewportMarginProperty);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            if (targets.Length == 1)
            {
                ReflowLayout layout = (ReflowLayout)target;
                DrawWarnings(layout);
                DrawRuntimeState(layout);
            }

            if (GUILayout.Button("Refresh Layout"))
            {
                foreach (Object item in targets)
                {
                    ReflowLayout layout = (ReflowLayout)item;
                    layout.RefreshAllLayout();
                    layout.EnsureLayout();
                }
                SceneView.RepaintAll();
            }
        }

        /// <summary> Settings of the concrete layout. Default: every serialized field the base does not draw. </summary>
        protected virtual void DrawLayoutSettings()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "_padding", "_widthPolicy", "_heightPolicy", "_includeSceneChildren",
                "_defaultChildSize", "_virtualization", "_viewport", "_viewportMargin", "_reflowDuration");
        }

        protected virtual void DrawWarnings(ReflowLayout pLayout)
        {
            RectTransform rectTransform = pLayout.RectTransform;
            for (int axis = 0; axis < 2; axis++)
            {
                ReflowSizePolicy policy = axis == 0 ? pLayout.WidthPolicy : pLayout.HeightPolicy;
                string axisName = axis == 0 ? "Width" : "Height";
                bool stretched = !Mathf.Approximately(ReflowUtil.Get(rectTransform.anchorMin, axis), ReflowUtil.Get(rectTransform.anchorMax, axis));
                if (policy.follow && stretched && pLayout.ParentLayout == null)
                    EditorGUILayout.HelpBox($"{axisName} follows content but the anchors stretch on this axis; the parent's size and the content will fight. Use non-stretched anchors on this axis.", MessageType.Warning);
                if (policy.follow && policy.min > 0f && policy.max > 0f && policy.min > policy.max)
                    EditorGUILayout.HelpBox($"{axisName}: min is larger than max; min wins.", MessageType.Info);
                RectTransform source = policy.constraintSource;
                if (policy.follow && source != null && (source == rectTransform || source.IsChildOf(rectTransform)))
                    EditorGUILayout.HelpBox($"{axisName}: 'Max From Rect' is this layout or inside it, which would depend on itself. It is ignored.", MessageType.Error);
            }
        }

        private static void DrawRuntimeState(ReflowLayout pLayout)
        {
            if (!Application.isPlaying && pLayout.ElementCount == 0)
                return;
            RectTransform viewport = pLayout.ResolvedViewport;
            string text = $"Elements {pLayout.ElementCount}   Spawned {pLayout.SpawnedCount}   Pooled items {pLayout.PooledItemCount}\n" +
                          $"Virtualized {(pLayout.IsVirtualized ? "yes" : "no")}   Viewport {(viewport != null ? viewport.name : "none")}   Dirty {(pLayout.IsDirty ? "yes" : "no")}";
            EditorGUILayout.HelpBox(text, MessageType.None);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflowLinear), true)]
    internal class ReflowLinearEditor : ReflowLayoutEditor
    {
        private SerializedProperty _spacingProperty;
        private SerializedProperty _childAlignmentProperty;
        private SerializedProperty _mainAxisModeProperty;
        private SerializedProperty _crossAxisModeProperty;
        private SerializedProperty _scaleToFitProperty;
        private SerializedProperty _reverseArrangementProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            _spacingProperty = serializedObject.FindProperty("_spacing");
            _childAlignmentProperty = serializedObject.FindProperty("_childAlignment");
            _mainAxisModeProperty = serializedObject.FindProperty("_mainAxisMode");
            _crossAxisModeProperty = serializedObject.FindProperty("_crossAxisMode");
            _scaleToFitProperty = serializedObject.FindProperty("_scaleToFit");
            _reverseArrangementProperty = serializedObject.FindProperty("_reverseArrangement");
        }

        protected override void DrawLayoutSettings()
        {
            EditorGUILayout.PropertyField(_spacingProperty);
            EditorGUILayout.PropertyField(_childAlignmentProperty);
            EditorGUILayout.PropertyField(_mainAxisModeProperty, new GUIContent("Main Axis Size", _mainAxisModeProperty.tooltip));
            EditorGUILayout.PropertyField(_crossAxisModeProperty, new GUIContent("Cross Axis Size", _crossAxisModeProperty.tooltip));
            EditorGUILayout.PropertyField(_scaleToFitProperty);
            EditorGUILayout.PropertyField(_reverseArrangementProperty);
        }

        protected override void DrawWarnings(ReflowLayout pLayout)
        {
            base.DrawWarnings(pLayout);
            ReflowLinear layout = (ReflowLinear)pLayout;
            int main = layout.PrimaryAxis;
            if (layout.MainAxisMode == ReflowChildSizeMode.Expand && layout.FollowsContent(main) && layout.ParentLayout == null)
                EditorGUILayout.HelpBox("Main axis Expand needs a given size; this layout follows its content on that axis, so there is no free space to share.", MessageType.Info);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(ReflowGrid), true)]
    internal class ReflowGridEditor : ReflowLayoutEditor
    {
        private SerializedProperty _cellSizeProperty;
        private SerializedProperty _spacingProperty;
        private SerializedProperty _startCornerProperty;
        private SerializedProperty _startAxisProperty;
        private SerializedProperty _constraintProperty;
        private SerializedProperty _constraintCountProperty;
        private SerializedProperty _childAlignmentProperty;
        private SerializedProperty _centerSingleRowProperty;
        private SerializedProperty _cellFitProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            _cellSizeProperty = serializedObject.FindProperty("_cellSize");
            _spacingProperty = serializedObject.FindProperty("_spacing");
            _startCornerProperty = serializedObject.FindProperty("_startCorner");
            _startAxisProperty = serializedObject.FindProperty("_startAxis");
            _constraintProperty = serializedObject.FindProperty("_constraint");
            _constraintCountProperty = serializedObject.FindProperty("_constraintCount");
            _childAlignmentProperty = serializedObject.FindProperty("_childAlignment");
            _centerSingleRowProperty = serializedObject.FindProperty("_centerSingleRow");
            _cellFitProperty = serializedObject.FindProperty("_cellFit");
        }

        protected override void DrawLayoutSettings()
        {
            EditorGUILayout.PropertyField(_cellSizeProperty);
            EditorGUILayout.PropertyField(_spacingProperty);
            EditorGUILayout.PropertyField(_cellFitProperty);
            EditorGUILayout.PropertyField(_startCornerProperty);
            EditorGUILayout.PropertyField(_startAxisProperty);
            EditorGUILayout.PropertyField(_constraintProperty);
            if (_constraintProperty.enumValueIndex != (int)ReflowGrid.Constraint.Flexible)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_constraintCountProperty, new GUIContent("Count"));
                if (_constraintCountProperty.intValue < 1)
                    _constraintCountProperty.intValue = 1;
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(_childAlignmentProperty);
            EditorGUILayout.PropertyField(_centerSingleRowProperty);
        }

        protected override void DrawWarnings(ReflowLayout pLayout)
        {
            base.DrawWarnings(pLayout);
            ReflowGrid grid = (ReflowGrid)pLayout;
            if (grid is ReflowExpandableGrid expandable)
            {
                if (grid.Axis == ReflowGrid.StartAxis.Vertical)
                    EditorGUILayout.HelpBox("The expandable block only works with Start Axis = Horizontal (rows). It is ignored now.", MessageType.Warning);
                if (Application.isPlaying)
                    EditorGUILayout.HelpBox(expandable.IsExpanded ? $"Expanded under element {expandable.ExpandedIndex}: {expandable.ExpandedContent.name}" : "Not expanded", MessageType.None);
            }
        }
    }
}
