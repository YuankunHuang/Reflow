using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Reflow
{
    public abstract partial class ReflowLayout
    {
        private const string VIEWPORT_NAME = "Viewport";
        private static readonly ProfilerMarker VISIBILITY_MARKER = new ProfilerMarker("Reflow.Visibility");
        private static readonly Vector3[] CORNER_ARRAY = new Vector3[4];

        private bool _isVirtualizedNow;
        private bool _isVisibilityDirty;
        private RectTransform _resolvedViewport;
        private bool _isViewportResolved;
        private bool _hasResolvedViewport;
        private bool _hasWarnedNoViewport;
        private ScrollRect _boundScrollRect;
        private UnityAction<Vector2> _onScrollAction;
        private int _visibleFirst;
        private int _visibleLast = -1;
        private int _visibleSolveVersion = -1;
        private int _solveCounter;
        private ReflowScrollAnchor _measureAnchor = ReflowScrollAnchor.None;
        private readonly List<int> _newSlotList = new List<int>();

        /// <summary> Whether managed elements are virtualized right now (a viewport was found). </summary>
        public bool IsVirtualized => _isVirtualizedNow;

        /// <summary> The viewport used for virtualization and scrolling, or null. </summary>
        public RectTransform ResolvedViewport => ResolveViewport();

        /// <summary> The ScrollRect this layout listens to, or null. </summary>
        public ScrollRect ScrollRect
        {
            get
            {
                ResolveViewport();
                return _boundScrollRect;
            }
        }

        /// <summary>
        /// Updates which elements are spawned after the content was moved by code. Scrolling through a ScrollRect
        /// does this automatically.
        /// </summary>
        public void RefreshVisibility()
        {
            EnsureLayout();
            UpdateVisibilityOnly();
        }

        /// <summary> Item showing element <paramref name="pIndex"/>, or null when it is not spawned. Runs pending layout first. </summary>
        public IReflowItem GetActiveElement(int pIndex)
        {
            EnsureLayout();
            if (pIndex < 0 || pIndex >= _entryCount)
                return null;
            return _entryArray[pIndex].record?.item;
        }

        /// <summary> Like <see cref="GetActiveElement"/> but without running pending layout. </summary>
        internal IReflowItem GetActiveElementNoLayout(int pIndex)
        {
            return pIndex >= 0 && pIndex < _entryCount ? _entryArray[pIndex].record?.item : null;
        }

        /// <summary> Element rect in this layout's local space (like <see cref="RectTransform.rect"/>). Runs pending layout first. </summary>
        public Rect GetElementRect(int pIndex)
        {
            EnsureLayout();
            if (!TryGetEntrySlot(pIndex, out int slotIndex))
                return default;
            ref ReflowSlot slot = ref _slotArray[slotIndex];
            Rect rect = RectTransform.rect;
            return new Rect(rect.xMin + slot.position.x, rect.yMax - slot.position.y - slot.size.y, slot.size.x, slot.size.y);
        }

        /// <summary>
        /// Distance from the layout's start to where scrolling puts element <paramref name="pIndex"/> at the viewport's
        /// start / center / end, along <see cref="PrimaryAxis"/>. Not clamped. For a vertical content with a top pivot
        /// this is the content's anchoredPosition.y.
        /// </summary>
        public float GetElementScrollOffset(int pIndex, ReflowScrollAlignment pAlignment = ReflowScrollAlignment.Start)
        {
            EnsureLayout();
            if (!TryGetEntrySlot(pIndex, out int slotIndex))
                return 0f;
            ref ReflowSlot slot = ref _slotArray[slotIndex];
            int axis = PrimaryAxis;
            float start = ReflowUtil.Get(slot.cellPosition, axis);
            float size = ReflowUtil.Get(slot.cellSize, axis);
            RectTransform viewport = ResolveViewport();
            float viewSize = viewport != null ? ReflowUtil.Get(viewport.rect.size, axis) : 0f;
            switch (pAlignment)
            {
                case ReflowScrollAlignment.Center:
                    return start + size * 0.5f - viewSize * 0.5f;
                case ReflowScrollAlignment.End:
                    return start + size - viewSize;
                default:
                    return start;
            }
        }

        /// <summary> First element (in visual order) that is at least partly inside the viewport, or -1. </summary>
        public int GetFirstVisibleIndex()
        {
            EnsureLayout();
            return TryGetViewRect(false, out Rect view) ? FindFirstVisibleEntrySlot(view, out _) : -1;
        }

        /// <summary> Remembers where the first visible element sits in the viewport. Pair with <see cref="RestoreScrollAnchor"/>. </summary>
        public ReflowScrollAnchor CaptureScrollAnchor()
        {
            EnsureLayout();
            if (!TryGetViewRect(false, out Rect view))
                return ReflowScrollAnchor.None;
            int index = FindFirstVisibleEntrySlot(view, out int slotIndex);
            if (index < 0)
                return ReflowScrollAnchor.None;
            int axis = PrimaryAxis;
            float viewStart = axis == 0 ? view.xMin : view.yMin;
            return new ReflowScrollAnchor { index = index, offset = ReflowUtil.Get(_slotArray[slotIndex].cellPosition, axis) - viewStart };
        }

        /// <summary>
        /// Scrolls so the anchored element sits where it was when captured, e.g. after items above it changed size.
        /// Stops ScrollRect inertia. Returns false when the element is gone or there is no viewport.
        /// </summary>
        public bool RestoreScrollAnchor(ReflowScrollAnchor pAnchor)
        {
            if (!pAnchor.IsValid)
                return false;
            EnsureLayout();
            if (!TryGetEntrySlot(pAnchor.index, out int slotIndex) || !TryGetViewRect(false, out Rect view))
                return false;

            int axis = PrimaryAxis;
            float viewStart = axis == 0 ? view.xMin : view.yMin;
            float delta = ReflowUtil.Get(_slotArray[slotIndex].cellPosition, axis) - viewStart - pAnchor.offset;
            if (Mathf.Abs(delta) > ReflowUtil.EPSILON)
                ShiftContent(axis, delta);
            if (_boundScrollRect != null)
                _boundScrollRect.StopMovement();
            UpdateVisibilityOnly();
            return true;
        }

        /// <summary> Moves the scrolled content so what is at <paramref name="pDelta"/> (container space) comes back into place. </summary>
        private void ShiftContent(int pAxis, float pDelta)
        {
            RectTransform content = _boundScrollRect != null && _boundScrollRect.content != null ? _boundScrollRect.content : RectTransform;
            Vector3 localShift = pAxis == 1 ? new Vector3(0f, pDelta, 0f) : new Vector3(-pDelta, 0f, 0f);
            Vector3 worldShift = RectTransform.TransformVector(localShift);
            Transform contentParent = content.parent;
            Vector2 parentShift = contentParent != null ? (Vector2)contentParent.InverseTransformVector(worldShift) : (Vector2)worldShift;
            content.anchoredPosition += parentShift;
            if (_boundScrollRect is ReflowScrollRect layoutScrollRect)
                layoutScrollRect.OnContentShifted(parentShift);
        }

        /// <summary>
        /// Items that were already on screen stay put when freshly spawned items turn out to have another size than
        /// estimated (scrolling up into unmeasured rows, jumping far). Like CSS scroll anchoring, but only for
        /// measurement corrections; data changes keep the scroll position (use Capture/RestoreScrollAnchor for those).
        /// </summary>
        private void ApplyMeasureAnchor()
        {
            ReflowScrollAnchor anchor = _measureAnchor;
            _measureAnchor = ReflowScrollAnchor.None;
            if (!TryGetEntrySlot(anchor.index, out int slotIndex) || !TryGetViewRect(true, out Rect view))
                return;
            int axis = PrimaryAxis;
            float viewStart = axis == 0 ? view.xMin : view.yMin;
            float delta = ReflowUtil.Get(_slotArray[slotIndex].cellPosition, axis) - viewStart - anchor.offset;
            if (Mathf.Abs(delta) > ReflowUtil.EPSILON)
                ShiftContent(axis, delta);
        }

        /// <summary> First visible element (visual order) that was spawned before this update, and its offset from the view start. </summary>
        private ReflowScrollAnchor FindStableAnchor(Rect pView, int pFirst, int pLast)
        {
            int axis = PrimaryAxis;
            float viewStart = axis == 0 ? pView.xMin : pView.yMin;
            bool ascending = IsPrimaryAscending;
            for (int k = 0; k <= pLast - pFirst; k++)
            {
                ref ReflowSlot slot = ref _slotArray[ascending ? pFirst + k : pLast - k];
                if (slot.entryIndex >= 0 && _entryArray[slot.entryIndex].record != null && OverlapsSecondary(ref slot, pView))
                    return new ReflowScrollAnchor { index = slot.entryIndex, offset = ReflowUtil.Get(slot.cellPosition, axis) - viewStart };
            }
            return ReflowScrollAnchor.None;
        }

        internal bool TryGetEntrySlot(int pIndex, out int pSlotIndex)
        {
            pSlotIndex = -1;
            if (pIndex < 0 || pIndex >= _entryCount)
                return false;
            int slotIndex = _entryArray[pIndex].slotIndex;
            if (slotIndex < 0 || slotIndex >= _slotCount || _slotArray[slotIndex].entryIndex != pIndex)
                return false;
            pSlotIndex = slotIndex;
            return true;
        }

        private int FindFirstVisibleEntrySlot(Rect pView, out int pSlotIndex)
        {
            GetVisibleSlotRange(pView, out int first, out int last);
            bool ascending = IsPrimaryAscending;
            for (int k = 0; k <= last - first; k++)
            {
                int s = ascending ? first + k : last - k;
                ref ReflowSlot slot = ref _slotArray[s];
                if (slot.entryIndex >= 0 && OverlapsSecondary(ref slot, pView))
                {
                    pSlotIndex = s;
                    return slot.entryIndex;
                }
            }
            pSlotIndex = -1;
            return -1;
        }

        #region Viewport

        private bool ResolveVirtualized()
        {
            if (_virtualization == ReflowVirtualization.Off)
                return false;
            if (ResolveViewport() != null)
                return true;
            if (_virtualization == ReflowVirtualization.On && !_hasWarnedNoViewport && _entryCount > 0)
            {
                _hasWarnedNoViewport = true;
                Debug.LogWarning("[Reflow] Virtualization is On but no viewport was found; spawning every element.", this);
            }
            return false;
        }

        private RectTransform ResolveViewport()
        {
            // Cached, unless the viewport that was found has been destroyed since.
            if (_isViewportResolved && (!_hasResolvedViewport || _resolvedViewport != null))
                return _resolvedViewport;

            UnbindScrollRect();
            _isViewportResolved = true;
            ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect != null && scrollRect.transform != transform)
                BindScrollRect(scrollRect);

            if (_viewport != null)
                _resolvedViewport = _viewport;
            else if (scrollRect != null)
                _resolvedViewport = scrollRect.viewport != null ? scrollRect.viewport : (RectTransform)scrollRect.transform;
            else
                _resolvedViewport = FindViewportByName();
            _hasResolvedViewport = _resolvedViewport != null;
            return _resolvedViewport;
        }

        private RectTransform FindViewportByName()
        {
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.name == VIEWPORT_NAME)
                    return parent as RectTransform;
            }
            return null;
        }

        private void InvalidateViewport()
        {
            _isViewportResolved = false;
            _hasResolvedViewport = false;
            _resolvedViewport = null;
            UnbindScrollRect();
        }

        private void BindScrollRect(ScrollRect pScrollRect)
        {
            if (_onScrollAction == null)
                _onScrollAction = OnScrolled;
            _boundScrollRect = pScrollRect;
            _boundScrollRect.onValueChanged.AddListener(_onScrollAction);
        }

        private void UnbindScrollRect()
        {
            if (_boundScrollRect != null)
                _boundScrollRect.onValueChanged.RemoveListener(_onScrollAction);
            _boundScrollRect = null;
        }

        private void OnScrolled(Vector2 pPosition)
        {
            RequestVisibilityUpdate();
        }

        private void RequestVisibilityUpdate()
        {
            if (!_isVirtualizedNow || !IsActive())
                return;
            _isVisibilityDirty = true;
            ReflowScheduler.Enqueue(this);
        }

        /// <summary> Viewport rect in container space (x right, y down from the top-left corner). </summary>
        private bool TryGetViewRect(bool pWithMargin, out Rect pView)
        {
            RectTransform viewport = ResolveViewport();
            if (viewport == null)
            {
                pView = default;
                return false;
            }

            RectTransform self = RectTransform;
            viewport.GetWorldCorners(CORNER_ARRAY);
            Vector3 a = self.InverseTransformPoint(CORNER_ARRAY[0]);
            Vector3 b = self.InverseTransformPoint(CORNER_ARRAY[2]);
            Rect rect = self.rect;
            float margin = pWithMargin ? _viewportMargin : 0f;
            pView = Rect.MinMaxRect(
                Mathf.Min(a.x, b.x) - rect.xMin - margin,
                rect.yMax - Mathf.Max(a.y, b.y) - margin,
                Mathf.Max(a.x, b.x) - rect.xMin + margin,
                rect.yMax - Mathf.Min(a.y, b.y) + margin);
            return true;
        }

        #endregion

        #region Visibility

        /// <summary>
        /// Slots [first, last] whose cells overlap the view along <see cref="PrimaryAxis"/>. Cells advance
        /// monotonically with the slot index, so this is two binary searches: O(log n).
        /// </summary>
        private void GetVisibleSlotRange(Rect pView, out int pFirst, out int pLast)
        {
            int count = _slotCount;
            int axis = PrimaryAxis;
            bool ascending = IsPrimaryAscending;
            float viewMin = axis == 0 ? pView.xMin : pView.yMin;
            float viewMax = axis == 0 ? pView.xMax : pView.yMax;

            // v = position in visual order; starts and ends never decrease with v.
            int low = 0;
            int high = count;
            while (low < high)
            {
                int mid = (low + high) >> 1;
                ref ReflowSlot slot = ref _slotArray[ascending ? mid : count - 1 - mid];
                if (ReflowUtil.Get(slot.cellPosition, axis) + ReflowUtil.Get(slot.cellSize, axis) > viewMin)
                    high = mid;
                else
                    low = mid + 1;
            }
            int firstVisual = low;

            high = count;
            while (low < high)
            {
                int mid = (low + high) >> 1;
                ref ReflowSlot slot = ref _slotArray[ascending ? mid : count - 1 - mid];
                if (ReflowUtil.Get(slot.cellPosition, axis) >= viewMax)
                    high = mid;
                else
                    low = mid + 1;
            }
            int lastVisual = low - 1;

            if (lastVisual < firstVisual)
            {
                pFirst = 0;
                pLast = -1;
                return;
            }
            pFirst = ascending ? firstVisual : count - 1 - lastVisual;
            pLast = ascending ? lastVisual : count - 1 - firstVisual;
        }

        private bool OverlapsSecondary(ref ReflowSlot pSlot, Rect pView)
        {
            int axis = 1 - PrimaryAxis;
            float start = ReflowUtil.Get(pSlot.cellPosition, axis);
            float end = start + ReflowUtil.Get(pSlot.cellSize, axis);
            return axis == 0 ? end > pView.xMin && start < pView.xMax : end > pView.yMin && start < pView.yMax;
        }

        private bool IsSlotVisible(int pSlotIndex, int pFirst, int pLast, Rect pView)
        {
            return pSlotIndex >= pFirst && pSlotIndex <= pLast && OverlapsSecondary(ref _slotArray[pSlotIndex], pView);
        }

        /// <summary>
        /// Spawns elements that came into view and despawns those that left. Returns true when a newly spawned item
        /// (or a child dirtied by a spawn callback) measured differently from what the last solve assumed.
        /// </summary>
        private bool UpdateVisibility(bool pAfterSolve)
        {
            _newSlotList.Clear();
            if (pAfterSolve)
                _solveCounter++;
            if (!_isVirtualizedNow || _entryCount == 0)
            {
                _hasSuppressedChildDirty = false;
                return false;
            }

            VISIBILITY_MARKER.Begin();
            bool changed = false;
            _suppressChildDirtyDepth++;
            try
            {
                int first = 0;
                int last = -1;
                bool hasView = TryGetViewRect(true, out Rect view);
                if (hasView)
                    GetVisibleSlotRange(view, out first, out last);
                ReflowScrollAnchor stableAnchor = hasView ? FindStableAnchor(view, first, last) : ReflowScrollAnchor.None;

                // Despawn what left the view.
                if (pAfterSolve || _visibleSolveVersion != _solveCounter)
                {
                    for (int i = _spawnedRecordList.Count - 1; i >= 0; i--)
                    {
                        int entryIndex = _spawnedRecordList[i].entryIndex;
                        int slotIndex = _entryArray[entryIndex].slotIndex;
                        if (slotIndex < 0 || !hasView || !IsSlotVisible(slotIndex, first, last, view))
                            Despawn(entryIndex);
                    }
                }
                else
                {
                    for (int s = _visibleFirst; s <= _visibleLast && s < _slotCount; s++)
                    {
                        int entryIndex = _slotArray[s].entryIndex;
                        if (entryIndex >= 0 && _entryArray[entryIndex].record != null && !IsSlotVisible(s, first, last, view))
                            Despawn(entryIndex);
                    }
                }

                // Spawn what entered it.
                for (int s = first; s <= last; s++)
                {
                    ref ReflowSlot slot = ref _slotArray[s];
                    if (slot.entryIndex < 0 || _entryArray[slot.entryIndex].record != null || !OverlapsSecondary(ref slot, view))
                        continue;
                    Spawn(slot.entryIndex);
                    _newSlotList.Add(s);
                }

                // Measure what was spawned (and children a spawn callback dirtied) against the solve's assumption.
                if (_newSlotList.Count > 0 || _hasSuppressedChildDirty)
                {
                    _hasSuppressedChildDirty = false;
                    for (int i = 0; i < _sceneSlotCount; i++)
                    {
                        ref ReflowSlot slot = ref _slotArray[i];
                        if (slot.node != null && slot.node._isDirty && !ReflowUtil.Approximately(slot.desired, MeasureSlot(ref slot, slot.exact)))
                            changed = true;
                    }
                    for (int i = 0; i < _spawnedRecordList.Count; i++)
                    {
                        ReflowItemRecord record = _spawnedRecordList[i];
                        ref ReflowEntry entry = ref _entryArray[record.entryIndex];
                        if (entry.slotIndex < 0)
                            continue;
                        ref ReflowSlot slot = ref _slotArray[entry.slotIndex];
                        if (slot.rectTransform != record.rectTransform)
                            FillEntrySlot(ref slot, ref entry);
                        Vector2 assumed = slot.desired;
                        if (!ReflowUtil.Approximately(MeasureSlot(ref slot, slot.exact), assumed))
                            changed = true;
                    }
                }

                // Sizes are about to be corrected: keep what was already on screen where it is.
                if (changed && stableAnchor.IsValid && !_measureAnchor.IsValid)
                    _measureAnchor = stableAnchor;

                _visibleFirst = first;
                _visibleLast = last;
                _visibleSolveVersion = _solveCounter;
            }
            finally
            {
                _suppressChildDirtyDepth--;
                VISIBILITY_MARKER.End();
            }
            return changed;
        }

        /// <summary> Scroll path: spawn / despawn only; a full layout follows only if a new item measured differently. </summary>
        private void UpdateVisibilityOnly()
        {
            _isVisibilityDirty = false;
            if (!_isVirtualizedNow || !_isSolveValid || _isDirty || IsLayingOut)
                return;

            bool changed = UpdateVisibility(false);
            if (_newSlotList.Count > 0)
            {
                ReflowScheduler.BeginWrite();
                try
                {
                    UpdateChildTracker();
                    for (int i = 0; i < _newSlotList.Count; i++)
                        WriteSlot(ref _slotArray[_newSlotList[i]]);
                }
                finally
                {
                    ReflowScheduler.EndWrite();
                }

                bool scaleToFit = UsesScaleToFit;
                for (int i = 0; i < _newSlotList.Count; i++)
                {
                    ref ReflowSlot slot = ref _slotArray[_newSlotList[i]];
                    if (slot.node != null && slot.node.isActiveAndEnabled)
                        slot.node.Arrange(scaleToFit ? slot.natural : slot.size);
                }
            }
            FinishAnimations();
            FlushDeactivations();
            if (changed)
                SetDirty(false);
        }

        #endregion
    }
}
