using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Layout-side state of one pooled item instance: cached components, the rect the layout wrote and the
    /// animation offset on top of it. One per instance, reused across spawns, so spawning allocates nothing.
    /// </summary>
    internal sealed class ReflowItemRecord
    {
        public readonly IReflowItem item;
        public readonly RectTransform rectTransform;
        public readonly GameObject gameObject;
        public readonly object poolKey;
        public readonly ReflowLayout owner;
        /// <summary> Prefab size, restored before every spawn so a reused item never reports the last entry's size. </summary>
        public readonly Vector2 baseSize;
        public readonly Vector3 baseScale;
        public readonly ReflowElement element;
        public readonly ReflowNode node;

        public int entryIndex = -1;
        public int spawnedListIndex = -1;
        public bool isInPool;

        // What the layout wrote; animations add on top.
        public Vector2 targetPosition;
        public Vector3 targetScale;

        // Animation state, driven by ReflowAnimationDriver.
        public int animationIndex = -1;
        public Vector2 offset;
        public float scaleFactor = 1f;
        public CanvasGroup canvasGroup;
        public float baseAlpha = 1f;
        public bool animatesAlpha;
        public bool animatesScale;
        public Vector2 offsetFrom;
        public float alphaFrom = 1f;
        public float scaleFrom = 1f;
        public float animationStart;
        public float animationDelay;
        public float animationDuration;

        // RefreshWithAnimation: visual position captured before the relayout.
        public bool hasPendingReflow;
        public Vector2 reflowFrom;

        public ReflowItemRecord(IReflowItem pItem, object pPoolKey, ReflowLayout pOwner)
        {
            item = pItem;
            rectTransform = pItem.RectTransform;
            gameObject = rectTransform.gameObject;
            poolKey = pPoolKey;
            owner = pOwner;

            baseSize = rectTransform.rect.size;
            ReflowUtil.AnchorTopLeft(rectTransform);
            baseScale = rectTransform.localScale;
            targetScale = baseScale;
            gameObject.TryGetComponent(out element);
            gameObject.TryGetComponent(out node);
        }

        public bool IsAnimating => animationIndex >= 0;

        public CanvasGroup EnsureCanvasGroup()
        {
            if (canvasGroup == null && !gameObject.TryGetComponent(out canvasGroup))
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                baseAlpha = 1f;
            }
            else if (!IsAnimating)
            {
                baseAlpha = canvasGroup.alpha;
            }
            return canvasGroup;
        }

        /// <summary> Back to the prefab's size and scale, so the next Show measures from a clean state. </summary>
        public void ResetForSpawn()
        {
            if (rectTransform.sizeDelta != baseSize)
                rectTransform.sizeDelta = baseSize;
            if (rectTransform.localScale != baseScale)
                rectTransform.localScale = baseScale;
            targetScale = baseScale;
            offset = Vector2.zero;
            scaleFactor = 1f;
            hasPendingReflow = false;
        }
    }
}
