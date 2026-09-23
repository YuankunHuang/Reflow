using UnityEngine;
using UnityEngine.UI;

namespace Reflow
{
    /// <summary> Content positions that bring a rect into a ScrollRect's viewport, clamped to the scroll range. </summary>
    public static class ReflowScrollUtil
    {
        /// <summary> Content anchoredPosition that puts <paramref name="pLocalRect"/> (in <paramref name="pSpace"/>'s local space) at the viewport's start / center / end. </summary>
        public static Vector2 GetAlignedContentPosition(ScrollRect pScrollRect, RectTransform pSpace, Rect pLocalRect, ReflowScrollAlignment pAlignment)
        {
            GetRects(pScrollRect, pSpace, pLocalRect, out Rect target, out Rect content, out Rect view);
            Vector2 delta = Vector2.zero;
            if (pScrollRect.horizontal)
            {
                switch (pAlignment)
                {
                    case ReflowScrollAlignment.Center: delta.x = view.center.x - target.center.x; break;
                    case ReflowScrollAlignment.End: delta.x = view.xMax - target.xMax; break;
                    default: delta.x = view.xMin - target.xMin; break;
                }
            }
            if (pScrollRect.vertical)
            {
                switch (pAlignment)
                {
                    case ReflowScrollAlignment.Center: delta.y = view.center.y - target.center.y; break;
                    case ReflowScrollAlignment.End: delta.y = view.yMin - target.yMin; break;
                    default: delta.y = view.yMax - target.yMax; break;
                }
            }
            return ToContentPosition(pScrollRect, Clamp(delta, content, view));
        }

        /// <summary> Smallest scroll that makes <paramref name="pLocalRect"/> fully visible (start aligned when it is larger than the viewport). </summary>
        public static Vector2 GetRevealContentPosition(ScrollRect pScrollRect, RectTransform pSpace, Rect pLocalRect, float pMargin)
        {
            GetRects(pScrollRect, pSpace, pLocalRect, out Rect target, out Rect content, out Rect view);
            Vector2 delta = Vector2.zero;
            if (pScrollRect.horizontal)
            {
                if (target.width + pMargin * 2f > view.width || target.xMin - pMargin < view.xMin)
                    delta.x = view.xMin - (target.xMin - pMargin);
                else if (target.xMax + pMargin > view.xMax)
                    delta.x = view.xMax - (target.xMax + pMargin);
            }
            if (pScrollRect.vertical)
            {
                if (target.height + pMargin * 2f > view.height || target.yMax + pMargin > view.yMax)
                    delta.y = view.yMax - (target.yMax + pMargin);
                else if (target.yMin - pMargin < view.yMin)
                    delta.y = view.yMin - (target.yMin - pMargin);
            }
            return ToContentPosition(pScrollRect, Clamp(delta, content, view));
        }

        /// <summary> Moves the content (animated through <see cref="ReflowScrollRect"/> when it is one) and stops inertia. </summary>
        public static void ScrollTo(ScrollRect pScrollRect, Vector2 pContentPosition, float pDuration = 0f)
        {
            if (pScrollRect is ReflowScrollRect layoutScrollRect)
            {
                layoutScrollRect.ScrollToContentPosition(pContentPosition, pDuration);
                return;
            }
            pScrollRect.StopMovement();
            pScrollRect.content.anchoredPosition = pContentPosition;
        }

        private static void GetRects(ScrollRect pScrollRect, RectTransform pSpace, Rect pLocalRect, out Rect pTarget, out Rect pContent, out Rect pView)
        {
            RectTransform viewport = pScrollRect.viewport != null ? pScrollRect.viewport : (RectTransform)pScrollRect.transform;
            RectTransform content = pScrollRect.content;
            pTarget = TransformRect(pSpace, viewport, pLocalRect);
            pContent = TransformRect(content, viewport, content.rect);
            pView = viewport.rect;
        }

        /// <summary> Clamps a content move (viewport space) so the content still covers the viewport. </summary>
        private static Vector2 Clamp(Vector2 pDelta, Rect pContent, Rect pView)
        {
            if (pContent.width >= pView.width)
                pDelta.x = Mathf.Clamp(pDelta.x, pView.xMax - pContent.xMax, pView.xMin - pContent.xMin);
            else
                pDelta.x = pView.xMin - pContent.xMin;

            if (pContent.height >= pView.height)
                pDelta.y = Mathf.Clamp(pDelta.y, pView.yMax - pContent.yMax, pView.yMin - pContent.yMin);
            else
                pDelta.y = pView.yMax - pContent.yMax;
            return pDelta;
        }

        private static Vector2 ToContentPosition(ScrollRect pScrollRect, Vector2 pViewportDelta)
        {
            RectTransform viewport = pScrollRect.viewport != null ? pScrollRect.viewport : (RectTransform)pScrollRect.transform;
            RectTransform content = pScrollRect.content;
            if (!pScrollRect.horizontal)
                pViewportDelta.x = 0f;
            if (!pScrollRect.vertical)
                pViewportDelta.y = 0f;
            Vector3 worldDelta = viewport.TransformVector(pViewportDelta);
            Transform parent = content.parent;
            Vector2 parentDelta = parent != null ? (Vector2)parent.InverseTransformVector(worldDelta) : (Vector2)worldDelta;
            return content.anchoredPosition + parentDelta;
        }

        private static Rect TransformRect(RectTransform pFrom, RectTransform pTo, Rect pRect)
        {
            if (pFrom == pTo)
                return pRect;
            Vector3 a = pTo.InverseTransformPoint(pFrom.TransformPoint(new Vector3(pRect.xMin, pRect.yMin)));
            Vector3 b = pTo.InverseTransformPoint(pFrom.TransformPoint(new Vector3(pRect.xMax, pRect.yMax)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
    }
}
