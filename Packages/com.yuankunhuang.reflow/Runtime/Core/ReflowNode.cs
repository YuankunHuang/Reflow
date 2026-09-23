using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reflow
{
    /// <summary>
    /// Base of everything that takes part in Reflow layout: containers (<see cref="ReflowLayout"/>) and
    /// content-sized leaves (<see cref="ReflowFitter"/>). One per GameObject.
    ///
    /// Protocol: a parent calls <see cref="Measure"/> (sizes go up), decides the final size and calls
    /// <see cref="Arrange"/> (positions go down). A node whose size does not depend on its content is a relayout
    /// boundary: <see cref="MarkDirty"/> walks up only until the first boundary and queues it in
    /// <see cref="ReflowScheduler"/>, so changing one label relayouts one branch, once per frame, still in that
    /// frame. Anything that needs the result calls <see cref="EnsureLayout"/> first.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class ReflowNode : UIBehaviour, ILayoutElement
    {
        private const int ROOT_PASS_LIMIT = 4;
        private static readonly ProfilerMarker ROOT_MARKER = new ProfilerMarker("Reflow.Root");

        internal bool _isDirty = true;
        internal bool _isQueued;
        internal int _sortDepth;
        /// <summary> Bumped by every <see cref="SetDirty"/>; lets containers tell whether a cached solve is still current. </summary>
        internal int _dirtyVersion;

        private int _layoutDepth;
        private RectTransform _rectTransform;
        private ReflowLayout _parentLayout;
        private bool _isParentResolved;
        private ReflowElement _element;
        private bool _isElementResolved;

        private bool _isMeasureValid;
        private Vector2 _measuredFixedSize;
        private Vector2 _desiredSize;
        private bool _hasArranged;
        private Vector2 _arrangedSize;

        private DrivenRectTransformTracker _selfTracker;
        private DrivenTransformProperties _selfDrivenProperties;
        private bool _notifiesUgui;
        private bool _isUguiResolved;

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = (RectTransform)transform;
                return _rectTransform;
            }
        }

        /// <summary> Size from the last measure. </summary>
        public Vector2 DesiredSize => _desiredSize;

        /// <summary> Whether a layout is pending for this node. </summary>
        public bool IsDirty => _isDirty;

        internal bool IsLayingOut => _layoutDepth > 0;

        /// <summary> The fixed size of the last measure (FREE on axes sized from content). </summary>
        internal Vector2 MeasuredFixedSize => _measuredFixedSize;

        internal virtual bool HasQueuedWork => _isDirty;

        /// <summary> Layout on the parent GameObject, or null. </summary>
        internal ReflowLayout ParentLayout
        {
            get
            {
                if (!_isParentResolved)
                {
                    Transform parent = transform.parent;
                    _parentLayout = parent != null ? parent.GetComponent<ReflowLayout>() : null;
                    _isParentResolved = true;
                }
                return _parentLayout;
            }
        }

        /// <summary> <see cref="ReflowElement"/> on this GameObject, or null. </summary>
        internal ReflowElement Element
        {
            get
            {
                if (!_isElementResolved)
                {
                    TryGetComponent(out _element);
                    _isElementResolved = true;
                }
                return _element;
            }
        }

        /// <summary> Whether this node's size on <paramref name="pAxis"/> (0 = x, 1 = y) comes from its content. </summary>
        public abstract bool FollowsContent(int pAxis);

        /// <summary>
        /// Size this node wants. <paramref name="pFixedSize"/> holds, per axis, the size the node will get
        /// (&gt;= 0), or <see cref="ReflowUtil.FREE"/> when the node picks it from its content.
        /// </summary>
        protected abstract Vector2 MeasureContent(Vector2 pFixedSize);

        /// <summary> Lays out the content inside the final size. </summary>
        protected abstract void ArrangeContent(Vector2 pSize);

        /// <summary>
        /// Requests a layout. Cheap and idempotent: any number of calls in a frame cost one layout, which still lands
        /// before this frame renders (or on <see cref="EnsureLayout"/>).
        /// </summary>
        public void MarkDirty()
        {
            SetDirty(false);
        }

        /// <summary> Runs any pending layout that affects this node now, so its rects can be read. </summary>
        public void EnsureLayout()
        {
            ReflowScheduler.FlushFor(this);
        }

        /// <param name="pForce"> Walk up again even if already dirty (structure changes, re-enable). </param>
        internal void SetDirty(bool pForce)
        {
            _isMeasureValid = false;
            _dirtyVersion++;
            if (_isDirty && !pForce)
                return;
            _isDirty = true;
            if (!IsActive())
                return;

            ReflowLayout parent = GetDependentParent();
            if (parent == null)
            {
                ReflowScheduler.Enqueue(this);
                return;
            }
            // A parent that is spawning or measuring this node measures it again itself.
            if (parent._suppressChildDirtyDepth > 0)
            {
                parent._hasSuppressedChildDirty = true;
                return;
            }
            parent.SetDirty(pForce);
        }

        /// <summary> The layout whose result depends on this node's size, or null when this node is a relayout boundary. </summary>
        internal ReflowLayout GetDependentParent()
        {
            if (!FollowsContent(0) && !FollowsContent(1))
                return null;
            ReflowLayout parent = ParentLayout;
            if (parent == null || !parent.IsActive() || !parent.IsLayoutChild(this))
                return null;
            return parent;
        }

        internal Vector2 Measure(Vector2 pFixedSize)
        {
            if (_isMeasureValid && pFixedSize == _measuredFixedSize)
                return _desiredSize;

            ReflowStats.MeasureCount++;
            _layoutDepth++;
            try
            {
                _desiredSize = MeasureContent(pFixedSize);
            }
            finally
            {
                _layoutDepth--;
            }
            _measuredFixedSize = pFixedSize;
            _isMeasureValid = true;
            return _desiredSize;
        }

        internal void Arrange(Vector2 pSize)
        {
            if (!_isDirty && _hasArranged && pSize == _arrangedSize)
                return;

            ReflowStats.ArrangeCount++;
            // Cleared first so the content can dirty the node again (e.g. freshly spawned items measured differently).
            _isDirty = false;
            _layoutDepth++;
            try
            {
                ArrangeContent(pSize);
            }
            finally
            {
                _layoutDepth--;
            }
            _arrangedSize = pSize;
            _hasArranged = true;
        }

        internal virtual void ProcessQueued()
        {
            if (_isDirty)
                LayoutAsRoot();
        }

        /// <summary> Called at every flush for nodes registered with <see cref="ReflowScheduler.SetSourceWatcher"/>. </summary>
        internal virtual void PollSources()
        {
        }

        /// <summary> Lays this node out as the top of a layout pass: sizes from the rect, except axes that follow content. </summary>
        internal void LayoutAsRoot()
        {
            if (!IsActive())
                return;

            ReflowLayout parent = GetDependentParent();
            if (parent != null)
            {
                // Became a follower since it was queued; the parent owns its size now.
                parent.SetDirty(true);
                return;
            }

            ReflowStats.RootLayoutCount++;
            ROOT_MARKER.Begin();
            try
            {
                Vector2 previousDesired = _desiredSize;
                bool followX = FollowsContent(0);
                bool followY = FollowsContent(1);
                for (int pass = 0; pass < ROOT_PASS_LIMIT && _isDirty; pass++)
                {
                    Vector2 current = RectTransform.rect.size;
                    Vector2 fixedSize = new Vector2(followX ? ReflowUtil.FREE : current.x, followY ? ReflowUtil.FREE : current.y);
                    Vector2 size = Measure(fixedSize);
                    if (followX || followY)
                        WriteOwnSize(size, followX, followY);
                    Arrange(size);
                }

                if (!ReflowUtil.Approximately(previousDesired, _desiredSize))
                    NotifyUgui();
            }
            finally
            {
                ROOT_MARKER.End();
            }
        }

        private void WriteOwnSize(Vector2 pSize, bool pFollowX, bool pFollowY)
        {
            RectTransform rectTransform = RectTransform;
            ReflowScheduler.BeginWrite();
            try
            {
                if (pFollowX)
                    ReflowUtil.SetSize(rectTransform, 0, pSize.x);
                if (pFollowY)
                    ReflowUtil.SetSize(rectTransform, 1, pSize.y);
            }
            finally
            {
                ReflowScheduler.EndWrite();
            }

#if UNITY_EDITOR
            DrivenTransformProperties properties = DrivenTransformProperties.None;
            if (pFollowX)
                properties |= DrivenTransformProperties.SizeDeltaX;
            if (pFollowY)
                properties |= DrivenTransformProperties.SizeDeltaY;
            if (properties != _selfDrivenProperties)
            {
                _selfTracker.Clear();
                if (properties != DrivenTransformProperties.None)
                    _selfTracker.Add(this, rectTransform, properties);
                _selfDrivenProperties = properties;
            }
#endif
        }

        private void ClearSelfTracker()
        {
            _selfTracker.Clear();
            _selfDrivenProperties = DrivenTransformProperties.None;
        }

        /// <summary> Lets a uGUI layout group above (or a ContentSizeFitter here) pick up a new preferred size. </summary>
        private void NotifyUgui()
        {
            if (!_isUguiResolved)
            {
                Transform parent = transform.parent;
                _notifiesUgui = (parent != null && parent.TryGetComponent(out ILayoutGroup _)) || TryGetComponent(out ILayoutSelfController _);
                _isUguiResolved = true;
            }
            if (_notifiesUgui && !CanvasUpdateRegistry.IsRebuildingLayout())
                LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
        }

        internal void InvalidateElement()
        {
            _isElementResolved = false;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _isParentResolved = false;
            _isElementResolved = false;
            _isUguiResolved = false;
            ReflowScheduler.EnsureInstalled();
            SetDirty(true);
            NotifyParentMembership();
        }

        protected override void OnDisable()
        {
            ClearSelfTracker();
            _isMeasureValid = false;
            _hasArranged = false;
            NotifyParentMembership();
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _isParentResolved = false;
            _isUguiResolved = false;
            ClearSelfTracker();
            if (IsActive())
                SetDirty(true);
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            if (!IsActive() || ReflowScheduler.IsWriting || IsLayingOut)
                return;
            if (_hasArranged && ReflowUtil.Approximately(RectTransform.rect.size, _arrangedSize))
                return;
            SetDirty(false);
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetDirty(false);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _isElementResolved = false;
            if (IsActive())
                SetDirty(true);
        }
#endif

        /// <summary>
        /// Joining or leaving the parent's child set changes the parent's layout, and the parent re-reads which children
        /// are layouts. Pooled items toggle under suppression, so spawning does not pay for this.
        /// </summary>
        private void NotifyParentMembership()
        {
            ReflowLayout parent = ParentLayout;
            if (parent != null && parent._suppressChildDirtyDepth == 0 && parent.IsActive())
                parent.OnChildComponentsChanged();
        }

        #region ILayoutElement (lets Reflow nodes sit inside uGUI layout groups)

        public virtual void CalculateLayoutInputHorizontal()
        {
            EnsureLayout();
        }

        public virtual void CalculateLayoutInputVertical()
        {
        }

        public virtual float minWidth => -1f;

        public virtual float preferredWidth => _isMeasureValid ? _desiredSize.x : -1f;

        public virtual float flexibleWidth => -1f;

        public virtual float minHeight => -1f;

        public virtual float preferredHeight => _isMeasureValid ? _desiredSize.y : -1f;

        public virtual float flexibleHeight => -1f;

        public virtual int layoutPriority => 1;

        #endregion
    }
}
