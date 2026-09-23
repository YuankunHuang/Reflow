using System;
using Unity.Profiling;
using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Base of Reflow containers. Lays out two kinds of children, in this order:
    ///   - scene children: active direct children placed in the prefab (unless ignored),
    ///   - managed elements: data added with <see cref="AddElement"/>, shown through pooled items and, inside a
    ///     viewport, virtualized (only visible ones are spawned).
    /// Every change marks the layout dirty; the layout itself runs once per frame at the scheduler's flush points,
    /// or right away on a read (<see cref="ReflowNode.EnsureLayout"/>, <see cref="GetActiveElement"/>, ...).
    /// </summary>
    public abstract partial class ReflowLayout : ReflowNode
    {
        private const int MAX_ARRANGE_ROUNDS = 4;
        private static readonly ProfilerMarker SOLVE_MARKER = new ProfilerMarker("Reflow.Solve");

        [SerializeField] protected RectOffset _padding = new RectOffset();
        [SerializeField] private ReflowSizePolicy _widthPolicy;
        [SerializeField] private ReflowSizePolicy _heightPolicy;
        [Tooltip("Lay out active direct children that are not managed elements (placed in the prefab).")]
        [SerializeField] private bool _includeSceneChildren = true;
        [Tooltip("Size for Fixed child size mode and for elements that give no size hint.")]
        [SerializeField] protected Vector2 _defaultChildSize = new Vector2(100f, 100f);
        [SerializeField] private ReflowVirtualization _virtualization = ReflowVirtualization.Auto;
        [Tooltip("Optional. Empty: the viewport of the ScrollRect above, else an ancestor named \"Viewport\".")]
        [SerializeField] private RectTransform _viewport;
        [Tooltip("Spawn elements this far outside the viewport too.")]
        [SerializeField] private float _viewportMargin;
        [Tooltip("Default duration of RefreshWithAnimation.")]
        [SerializeField] private float _reflowDuration = 0.25f;

        /// <summary> Children of this layout that are measuring or spawning right now report here instead of dirtying it. </summary>
        internal int _suppressChildDirtyDepth;
        internal bool _hasSuppressedChildDirty;

        private bool _isSolveValid;
        private int _solvedVersion;
        private Vector2 _solvedSize;
        private Vector2 _contentSize;
        private Vector2 _lastSourceSize;
        private ReflowNode _widthSourceNode;
        private ReflowNode _heightSourceNode;

        internal ReflowSpawnAnimator _spawnAnimator;

        public RectOffset Padding
        {
            get => _padding;
            set
            {
                _padding = value ?? new RectOffset();
                MarkDirty();
            }
        }

        public ReflowSizePolicy WidthPolicy
        {
            get => _widthPolicy;
            set
            {
                _widthPolicy = value;
                OnPolicyChanged();
            }
        }

        public ReflowSizePolicy HeightPolicy
        {
            get => _heightPolicy;
            set
            {
                _heightPolicy = value;
                OnPolicyChanged();
            }
        }

        /// <summary> Shortcut for <see cref="WidthPolicy"/>.follow. </summary>
        public bool FollowWidth
        {
            get => _widthPolicy.follow;
            set
            {
                _widthPolicy.follow = value;
                OnPolicyChanged();
            }
        }

        /// <summary> Shortcut for <see cref="HeightPolicy"/>.follow. </summary>
        public bool FollowHeight
        {
            get => _heightPolicy.follow;
            set
            {
                _heightPolicy.follow = value;
                OnPolicyChanged();
            }
        }

        public bool IncludeSceneChildren
        {
            get => _includeSceneChildren;
            set
            {
                _includeSceneChildren = value;
                OnChildComponentsChanged();
            }
        }

        public Vector2 DefaultChildSize
        {
            get => _defaultChildSize;
            set
            {
                _defaultChildSize = value;
                MarkDirty();
            }
        }

        public ReflowVirtualization Virtualization
        {
            get => _virtualization;
            set
            {
                _virtualization = value;
                MarkDirty();
            }
        }

        public RectTransform Viewport
        {
            get => _viewport;
            set
            {
                _viewport = value;
                InvalidateViewport();
                MarkDirty();
            }
        }

        public float ViewportMargin
        {
            get => _viewportMargin;
            set
            {
                _viewportMargin = value;
                RequestVisibilityUpdate();
            }
        }

        public float ReflowDuration
        {
            get => _reflowDuration;
            set => _reflowDuration = value;
        }

        /// <summary> Refreshes the layout. Only needed for changes the layout cannot see (a plain child resized or toggled by hand). </summary>
        public void RefreshAllLayout()
        {
            SetDirty(false);
        }

        public override bool FollowsContent(int pAxis)
        {
            return (pAxis == 0 ? _widthPolicy : _heightPolicy).follow;
        }

        /// <summary> Axis along which the slots advance (and scrolling usually runs): 0 = x, 1 = y. </summary>
        internal abstract int PrimaryAxis { get; }

        /// <summary> Whether slot positions grow with the slot index along <see cref="PrimaryAxis"/>. </summary>
        internal abstract bool IsPrimaryAscending { get; }

        /// <summary> Whether children keep their size and are scaled to the slot instead. </summary>
        internal virtual bool UsesScaleToFit => false;

        /// <summary>
        /// Sizes and places the slots built by <see cref="BuildSlots"/>. <paramref name="pFixedSize"/> as in
        /// <see cref="ReflowNode.Measure"/>. Returns the container size and records the content size with
        /// <see cref="SetContentSize"/>.
        /// </summary>
        internal abstract Vector2 Solve(Vector2 pFixedSize);

        /// <summary> Scene children a subclass positions itself (e.g. expanded content). </summary>
        internal virtual bool ExcludeSceneChild(RectTransform pChild)
        {
            return false;
        }

        /// <summary> An excluded child whose size this layout still depends on (it positions it itself). </summary>
        internal virtual bool IsExtraChild(RectTransform pChild)
        {
            return false;
        }

        /// <summary> Called after the slots are written and child layouts arranged. </summary>
        internal virtual void OnArranged()
        {
        }

        /// <summary> Called on insert / remove / reorder / clear, before <see cref="ElementsChanged"/>. </summary>
        internal virtual void OnElementsChangedInternal(ReflowElementChange pChange)
        {
        }

        protected sealed override Vector2 MeasureContent(Vector2 pFixedSize)
        {
            return RunSolve(pFixedSize);
        }

        protected sealed override void ArrangeContent(Vector2 pSize)
        {
            for (int round = 0; round < MAX_ARRANGE_ROUNDS; round++)
            {
                if (!_isSolveValid || _solvedVersion != _dirtyVersion || !ReflowUtil.Approximately(pSize, _solvedSize))
                    RunSolve(pSize);
                if (_measureAnchor.IsValid)
                    ApplyMeasureAnchor();
                if (!UpdateVisibility(true))
                    break;
                // Freshly spawned items measured differently from their estimate: solve again with real sizes.
                _isSolveValid = false;
            }

            WriteSlots();
            ArrangeChildNodes();
            OnArranged();
            FinishAnimations();
            FlushDeactivations();

            // The content changed size while arranging (spawned items, children dirtied by callbacks): whoever sized
            // this layout has to measure it again.
            Vector2 measuredFixed = MeasuredFixedSize;
            if ((measuredFixed.x < 0f && !ReflowUtil.Approximately(_contentSize.x, DesiredSize.x)) ||
                (measuredFixed.y < 0f && !ReflowUtil.Approximately(_contentSize.y, DesiredSize.y)))
                SetDirty(true);
        }

        private Vector2 RunSolve(Vector2 pFixedSize)
        {
            SOLVE_MARKER.Begin();
            ReflowStats.SolveCount++;
            int version = _dirtyVersion;
            try
            {
                BuildSlots();
                _contentSize = pFixedSize;
                Vector2 size = Solve(pFixedSize);
                _solvedSize = size;
                _solvedVersion = version;
                _isSolveValid = true;
                return size;
            }
            finally
            {
                SOLVE_MARKER.End();
            }
        }

        /// <summary> Records what the size would be on <paramref name="pAxis"/> if it followed the content. </summary>
        internal void SetContentSize(int pAxis, float pSize)
        {
            ReflowUtil.Set(ref _contentSize, pAxis, pSize);
        }

        internal override bool HasQueuedWork => _isDirty || _isVisibilityDirty;

        internal override void ProcessQueued()
        {
            if (_isDirty)
            {
                LayoutAsRoot();
                return;
            }
            if (_isVisibilityDirty)
                UpdateVisibilityOnly();
        }

        #region Size policy

        private ref ReflowSizePolicy GetPolicy(int pAxis)
        {
            if (pAxis == 0)
                return ref _widthPolicy;
            return ref _heightPolicy;
        }

        /// <summary> Cap on <paramref name="pAxis"/>: the tighter of max and the constraint source's current size (&lt;= 0 = none). </summary>
        internal float GetCap(int pAxis)
        {
            ref ReflowSizePolicy policy = ref GetPolicy(pAxis);
            float cap = policy.max;
            RectTransform source = GetValidSource(pAxis);
            if (source != null)
            {
                ReflowNode sourceNode = pAxis == 0 ? _widthSourceNode : _heightSourceNode;
                if (sourceNode != null)
                    sourceNode.EnsureLayout();
                float sourceSize = ReflowUtil.Get(source.rect.size, pAxis);
                ReflowUtil.Set(ref _lastSourceSize, pAxis, sourceSize);
                cap = ReflowUtil.CombineCaps(cap, sourceSize);
            }
            return cap;
        }

        /// <summary> Container size on a following axis for content of size <paramref name="pContent"/> (padding included). </summary>
        internal float ApplyPolicy(int pAxis, float pContent)
        {
            float size = pContent;
            float cap = GetCap(pAxis);
            if (cap > ReflowUtil.EPSILON && size > cap)
                size = cap;
            float min = GetPolicy(pAxis).min;
            if (min > ReflowUtil.EPSILON && size < min)
                size = min;
            return size;
        }

        private RectTransform GetValidSource(int pAxis)
        {
            ref ReflowSizePolicy policy = ref GetPolicy(pAxis);
            RectTransform source = policy.constraintSource;
            if (!policy.follow || source == null)
                return null;
            // A source inside this layout would depend on the layout itself.
            if (source == RectTransform || source.IsChildOf(transform))
                return null;
            return source;
        }

        private void OnPolicyChanged()
        {
            UpdateSourceWatch();
            SetDirty(true);
        }

        private void UpdateSourceWatch()
        {
            RectTransform widthSource = GetValidSource(0);
            RectTransform heightSource = GetValidSource(1);
            _widthSourceNode = widthSource != null ? widthSource.GetComponent<ReflowNode>() : null;
            _heightSourceNode = heightSource != null ? heightSource.GetComponent<ReflowNode>() : null;
            ReflowScheduler.SetSourceWatcher(this, isActiveAndEnabled && (widthSource != null || heightSource != null));
        }

        internal override void PollSources()
        {
            if (!IsActive())
                return;
            RectTransform widthSource = GetValidSource(0);
            RectTransform heightSource = GetValidSource(1);
            if ((widthSource != null && !ReflowUtil.Approximately(widthSource.rect.width, _lastSourceSize.x)) ||
                (heightSource != null && !ReflowUtil.Approximately(heightSource.rect.height, _lastSourceSize.y)))
                SetDirty(false);
        }

        #endregion

        #region Unity lifecycle

        protected override void OnEnable()
        {
            _isSceneChildrenDirty = true;
            _isTrackerDirty = true;
            _isSolveValid = false;
            InvalidateViewport();
            UpdateSourceWatch();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            UnbindScrollRect();
            ReflowScheduler.SetSourceWatcher(this, false);
            ReflowAnimationDriver.CompleteAll(this);
            ClearChildTracker();
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            InvalidateViewport();
            base.OnTransformParentChanged();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            InvalidateViewport();
        }

        private void OnTransformChildrenChanged()
        {
            if (_isCreatingItem)
                return;
            OnChildComponentsChanged();
        }

        /// <summary> A direct child joined, left, or changed its layout components. </summary>
        internal void OnChildComponentsChanged()
        {
            _isSceneChildrenDirty = true;
            _isTrackerDirty = true;
            if (IsActive())
                SetDirty(false);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _isSceneChildrenDirty = true;
            _isTrackerDirty = true;
            if (isActiveAndEnabled)
            {
                InvalidateViewport();
                UpdateSourceWatch();
            }
            base.OnValidate();
        }
#endif

        #endregion
    }
}
