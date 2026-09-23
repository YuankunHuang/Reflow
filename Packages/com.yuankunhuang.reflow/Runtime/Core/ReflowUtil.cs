using UnityEngine;

namespace Reflow
{
    internal static class ReflowUtil
    {
        /// <summary> "Not fixed": the node picks the size on this axis. </summary>
        internal const float FREE = -1f;
        internal const float EPSILON = 0.001f;

        internal static readonly Vector2 FREE_SIZE = new Vector2(FREE, FREE);
        internal static readonly Vector2 TOP_LEFT = new Vector2(0f, 1f);

        internal static float Get(Vector2 pVector, int pAxis)
        {
            return pAxis == 0 ? pVector.x : pVector.y;
        }

        internal static void Set(ref Vector2 pVector, int pAxis, float pValue)
        {
            if (pAxis == 0)
                pVector.x = pValue;
            else
                pVector.y = pValue;
        }

        /// <summary> Builds a vector from a value on <paramref name="pAxis"/> and one on the other axis. </summary>
        internal static Vector2 Compose(int pAxis, float pAlong, float pAcross)
        {
            return pAxis == 0 ? new Vector2(pAlong, pAcross) : new Vector2(pAcross, pAlong);
        }

        /// <summary> 0 / 0.5 / 1 for start / center / end of <paramref name="pAxis"/> (y grows downward). </summary>
        internal static float AlignFactor(TextAnchor pAnchor, int pAxis)
        {
            int value = (int)pAnchor;
            return pAxis == 0 ? (value % 3) * 0.5f : (value / 3) * 0.5f;
        }

        internal static bool Approximately(float pA, float pB)
        {
            return Mathf.Abs(pA - pB) <= EPSILON;
        }

        internal static bool Approximately(Vector2 pA, Vector2 pB)
        {
            return Mathf.Abs(pA.x - pB.x) <= EPSILON && Mathf.Abs(pA.y - pB.y) <= EPSILON;
        }

        /// <summary> The tighter of two optional caps (&lt;= 0 = none). </summary>
        internal static float CombineCaps(float pCapA, float pCapB)
        {
            if (pCapA <= EPSILON)
                return pCapB;
            if (pCapB <= EPSILON)
                return pCapA;
            return Mathf.Min(pCapA, pCapB);
        }

        /// <summary>
        /// Natural size to use for a child the layout may have resized. A rect that still holds what the layout wrote
        /// last time carries no news, so the remembered natural size stays; anything else was changed by the child's
        /// content or by hand and becomes the new natural size. Without this the written size would be read back as
        /// natural and the original size could never come back.
        /// </summary>
        internal static Vector2 ResolveNatural(Vector2 pRectSize, ref Vector2 pNatural, ref bool pHasNatural, Vector2 pLastWritten, bool pHasWritten)
        {
            if (!pHasNatural || !pHasWritten)
            {
                pNatural = pRectSize;
                pHasNatural = true;
                return pNatural;
            }
            if (!Approximately(pRectSize.x, pLastWritten.x))
                pNatural.x = pRectSize.x;
            if (!Approximately(pRectSize.y, pLastWritten.y))
                pNatural.y = pRectSize.y;
            return pNatural;
        }

        /// <summary>
        /// Moves anchors to the parent's top-left corner (the uGUI layout group convention) keeping the current size.
        /// </summary>
        internal static void AnchorTopLeft(RectTransform pRect)
        {
            if (pRect.anchorMin == TOP_LEFT && pRect.anchorMax == TOP_LEFT)
                return;
            Vector2 size = pRect.rect.size;
            pRect.anchorMin = TOP_LEFT;
            pRect.anchorMax = TOP_LEFT;
            pRect.sizeDelta = size;
        }

        /// <summary> anchoredPosition that puts the child's top-left corner at <paramref name="pTopLeft"/> (x right, y down). </summary>
        internal static Vector2 GetAnchoredPosition(RectTransform pRect, Vector2 pTopLeft, Vector2 pVisualSize)
        {
            Vector2 pivot = pRect.pivot;
            return new Vector2(pTopLeft.x + pivot.x * pVisualSize.x, -(pTopLeft.y + (1f - pivot.y) * pVisualSize.y));
        }

        internal static void SetSize(RectTransform pRect, int pAxis, float pSize)
        {
            if (Approximately(Get(pRect.rect.size, pAxis), pSize))
                return;
            pRect.SetSizeWithCurrentAnchors(pAxis == 0 ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical, pSize);
            ReflowStats.RectWriteCount++;
        }
    }
}
