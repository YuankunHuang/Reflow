using UnityEditor;
using UnityEngine;

namespace Reflow.Editor
{
    /// <summary> Scene view overlay for a selected layout: cells, children (spawned or not) and the viewport. </summary>
    internal static class ReflowGizmos
    {
        private const int MAX_DRAWN_SLOTS = 2000;
        private static readonly Color CELL_COLOR = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color SPAWNED_COLOR = new Color(0.3f, 0.9f, 0.4f, 0.8f);
        private static readonly Color VIRTUAL_COLOR = new Color(0.3f, 0.6f, 1f, 0.5f);
        private static readonly Color VIEWPORT_COLOR = new Color(1f, 0.8f, 0.2f, 0.9f);
        private static readonly Vector3[] CORNER_ARRAY = new Vector3[4];

        [DrawGizmo(GizmoType.Selected)]
        private static void DrawLayout(ReflowLayout pLayout, GizmoType pType)
        {
            RectTransform rectTransform = pLayout.RectTransform;
            Rect rect = rectTransform.rect;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = rectTransform.localToWorldMatrix;

            int count = Mathf.Min(pLayout._slotCount, MAX_DRAWN_SLOTS);
            for (int i = 0; i < count; i++)
            {
                ref ReflowSlot slot = ref pLayout._slotArray[i];
                Gizmos.color = CELL_COLOR;
                DrawRect(rect, slot.cellPosition, slot.cellSize);
                Gizmos.color = slot.rectTransform != null ? SPAWNED_COLOR : VIRTUAL_COLOR;
                DrawRect(rect, slot.position, slot.size);
            }
            Gizmos.matrix = previousMatrix;

            RectTransform viewport = pLayout.ResolvedViewport;
            if (viewport == null)
                return;
            viewport.GetWorldCorners(CORNER_ARRAY);
            Gizmos.color = VIEWPORT_COLOR;
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(CORNER_ARRAY[i], CORNER_ARRAY[(i + 1) % 4]);
        }

        /// <summary> Draws a rect given in container space (x right, y down from the top-left corner). </summary>
        private static void DrawRect(Rect pContainer, Vector2 pTopLeft, Vector2 pSize)
        {
            Vector3 center = new Vector3(pContainer.xMin + pTopLeft.x + pSize.x * 0.5f, pContainer.yMax - pTopLeft.y - pSize.y * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(pSize.x, pSize.y, 0f));
        }
    }
}
