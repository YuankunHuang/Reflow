using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Grid that can open a full-width block (e.g. a detail panel) under the row of one element; the rows below
    /// move down. Row-by-row filling only (<see cref="ReflowGrid.StartAxis.Horizontal"/>).
    /// </summary>
    [AddComponentMenu("Layout/Reflow/Expandable Grid")]
    public sealed class ReflowExpandableGrid : ReflowGrid
    {
        private int _expandedIndex = -1;
        private RectTransform _expandedContent;
        private ReflowNode _expandedNode;
        private Vector2 _contentNatural;
        private bool _hasContentNatural;
        private Vector2 _contentLastWritten;
        private bool _hasContentWritten;

        public int ExpandedIndex => _expandedIndex;

        public RectTransform ExpandedContent => _expandedContent;

        public bool IsExpanded => _expandedIndex >= 0 && _expandedContent != null;

        /// <summary>
        /// Shows <paramref name="pContent"/> under the row of element <paramref name="pElementIndex"/>. The content is
        /// parented to this layout and activated; its height is its own (or its content's, for Reflow layouts and fitters)
        /// at the full inner width.
        /// </summary>
        /// <param name="pKeepScrollAnchor"> Keep the first visible element in place, e.g. when a block above it closes. </param>
        public void SetExpandedContent(int pElementIndex, RectTransform pContent, bool pKeepScrollAnchor = true)
        {
            if (pContent == null)
            {
                ClearExpandedContent(pKeepScrollAnchor);
                return;
            }
            if (pElementIndex < 0 || pElementIndex >= ElementCount)
                throw new System.ArgumentOutOfRangeException(nameof(pElementIndex));

            ReflowScrollAnchor anchor = pKeepScrollAnchor ? CaptureScrollAnchor() : ReflowScrollAnchor.None;
            if (_expandedContent != null && _expandedContent != pContent)
                _expandedContent.gameObject.SetActive(false);
            if (_expandedContent != pContent)
            {
                _hasContentNatural = false;
                _hasContentWritten = false;
            }

            _expandedIndex = pElementIndex;
            _expandedContent = pContent;
            pContent.TryGetComponent(out _expandedNode);
            if (pContent.parent != transform)
                pContent.SetParent(transform, false);
            if (!pContent.gameObject.activeSelf)
                pContent.gameObject.SetActive(true);
            OnChildComponentsChanged();
            RestoreScrollAnchor(anchor);
        }

        /// <summary> Closes the block. The content is deactivated, not destroyed. </summary>
        public void ClearExpandedContent(bool pKeepScrollAnchor = true)
        {
            if (_expandedContent == null && _expandedIndex < 0)
                return;
            ReflowScrollAnchor anchor = pKeepScrollAnchor ? CaptureScrollAnchor() : ReflowScrollAnchor.None;
            RectTransform content = _expandedContent;
            _expandedContent = null;
            _expandedNode = null;
            _expandedIndex = -1;
            if (content != null)
                content.gameObject.SetActive(false);
            OnChildComponentsChanged();
            RestoreScrollAnchor(anchor);
        }

        /// <summary> Scrolls the ScrollRect above so the expanded row and block are visible. </summary>
        public bool ScrollExpandedIntoView(float pMargin = 0f, float pDuration = 0f)
        {
            if (!IsExpanded)
                return false;
            EnsureLayout();
            UnityEngine.UI.ScrollRect scrollRect = ScrollRect;
            if (scrollRect == null || scrollRect.content == null || !_hasExpansion)
                return false;

            Rect rowRect = GetElementRect(_expandedIndex);
            Rect rect = RectTransform.rect;
            Rect blockRect = new Rect(rect.xMin + _expansionRect.x, rect.yMax - _expansionRect.y - _expansionRect.height, _expansionRect.width, _expansionRect.height);
            Rect target = Rect.MinMaxRect(
                Mathf.Min(rowRect.xMin, blockRect.xMin), Mathf.Min(rowRect.yMin, blockRect.yMin),
                Mathf.Max(rowRect.xMax, blockRect.xMax), Mathf.Max(rowRect.yMax, blockRect.yMax));
            Vector2 position = ReflowScrollUtil.GetRevealContentPosition(scrollRect, RectTransform, target, pMargin);
            ReflowScrollUtil.ScrollTo(scrollRect, position, pDuration);
            return true;
        }

        internal override bool ExcludeSceneChild(RectTransform pChild)
        {
            return pChild == _expandedContent;
        }

        internal override bool IsExtraChild(RectTransform pChild)
        {
            return pChild == _expandedContent;
        }

        internal override bool TryGetExpansion(float pInnerWidth, out int pEntryIndex, out float pHeight)
        {
            pEntryIndex = _expandedIndex;
            pHeight = 0f;
            if (!IsExpanded || _expandedIndex >= ElementCount || !_expandedContent.gameObject.activeSelf || IsElementHidden(_expandedIndex))
                return false;
            // The content may have got (or lost) a layout since it was set.
            if (_expandedNode == null)
                _expandedContent.TryGetComponent(out _expandedNode);

            Vector2 natural = ReflowUtil.ResolveNatural(_expandedContent.rect.size, ref _contentNatural, ref _hasContentNatural, _contentLastWritten, _hasContentWritten);
            if (_expandedNode != null && _expandedNode.isActiveAndEnabled)
                pHeight = _expandedNode.Measure(new Vector2(pInnerWidth, _expandedNode.FollowsContent(1) ? ReflowUtil.FREE : natural.y)).y;
            else
                pHeight = natural.y;
            return true;
        }

        internal override void OnArranged()
        {
            if (!_hasExpansion || _expandedContent == null)
                return;

            Vector2 size = _expansionRect.size;
            ReflowScheduler.BeginWrite();
            try
            {
                ReflowUtil.AnchorTopLeft(_expandedContent);
                if (_expandedContent.sizeDelta != size)
                    _expandedContent.sizeDelta = size;
                Vector2 position = ReflowUtil.GetAnchoredPosition(_expandedContent, _expansionRect.position, size);
                if (_expandedContent.anchoredPosition != position)
                    _expandedContent.anchoredPosition = position;
            }
            finally
            {
                ReflowScheduler.EndWrite();
            }
            _contentLastWritten = size;
            _hasContentWritten = true;
            if (_expandedNode != null && _expandedNode.isActiveAndEnabled)
                _expandedNode.Arrange(size);
        }

        internal override void OnElementsChangedInternal(ReflowElementChange pChange)
        {
            if (_expandedIndex < 0)
                return;
            int mapped = pChange.MapIndex(_expandedIndex);
            if (mapped >= 0)
            {
                _expandedIndex = mapped;
                return;
            }
            RectTransform content = _expandedContent;
            _expandedContent = null;
            _expandedNode = null;
            _expandedIndex = -1;
            if (content != null)
                content.gameObject.SetActive(false);
        }
    }
}
