using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Reflow
{
    /// <summary>
    /// Per-child options read by the parent Reflow layout: skip the child, give it a manual size, keep the layout from
    /// resizing an axis, flexible weights and min/max bounds. Changing a value relayouts the parent.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Layout/Reflow/Element")]
    public sealed class ReflowElement : UIBehaviour
    {
        public enum SizeSource
        {
            /// <summary> The child's own rect (or content, for fitters and layouts). </summary>
            Rect = 0,
            /// <summary> <see cref="ManualSize"/>. </summary>
            Manual = 1
        }

        [SerializeField] private bool _ignoreLayout;
        [SerializeField] private SizeSource _sizeSource = SizeSource.Rect;
        [SerializeField] private Vector2 _manualSize = new Vector2(100f, 100f);
        [Tooltip("Off: the layout positions this child but never changes its width.")]
        [SerializeField] private bool _layoutControlsWidth = true;
        [Tooltip("Off: the layout positions this child but never changes its height.")]
        [SerializeField] private bool _layoutControlsHeight = true;
        [Tooltip("Share of free space when the layout expands or is capped. 0 = keep natural size.")]
        [SerializeField] private float _flexibleWidth = 1f;
        [SerializeField] private float _flexibleHeight = 1f;
        [Tooltip("0 = none. Kept even if the total overflows the container.")]
        [SerializeField] private float _minWidth;
        [Tooltip("0 = none.")]
        [SerializeField] private float _maxWidth;
        [Tooltip("0 = none. Kept even if the total overflows the container.")]
        [SerializeField] private float _minHeight;
        [Tooltip("0 = none.")]
        [SerializeField] private float _maxHeight;

        private ReflowLayout _parentLayout;
        private bool _isParentResolved;

        public bool IgnoreLayout
        {
            get => _ignoreLayout;
            set
            {
                if (_ignoreLayout == value)
                    return;
                _ignoreLayout = value;
                OnMembershipChanged();
            }
        }

        public SizeSource Source
        {
            get => _sizeSource;
            set => SetField(ref _sizeSource, value);
        }

        public Vector2 ManualSize
        {
            get => _manualSize;
            set => SetField(ref _manualSize, value);
        }

        public bool LayoutControlsWidth
        {
            get => _layoutControlsWidth;
            set => SetField(ref _layoutControlsWidth, value);
        }

        public bool LayoutControlsHeight
        {
            get => _layoutControlsHeight;
            set => SetField(ref _layoutControlsHeight, value);
        }

        public float FlexibleWidth
        {
            get => _flexibleWidth;
            set => SetField(ref _flexibleWidth, value);
        }

        public float FlexibleHeight
        {
            get => _flexibleHeight;
            set => SetField(ref _flexibleHeight, value);
        }

        public float MinWidth
        {
            get => _minWidth;
            set => SetField(ref _minWidth, value);
        }

        public float MaxWidth
        {
            get => _maxWidth;
            set => SetField(ref _maxWidth, value);
        }

        public float MinHeight
        {
            get => _minHeight;
            set => SetField(ref _minHeight, value);
        }

        public float MaxHeight
        {
            get => _maxHeight;
            set => SetField(ref _maxHeight, value);
        }

        internal bool LayoutControls(int pAxis)
        {
            return pAxis == 0 ? _layoutControlsWidth : _layoutControlsHeight;
        }

        internal float GetFlexible(int pAxis)
        {
            return pAxis == 0 ? _flexibleWidth : _flexibleHeight;
        }

        internal float GetMin(int pAxis)
        {
            return pAxis == 0 ? _minWidth : _minHeight;
        }

        internal float GetMax(int pAxis)
        {
            return pAxis == 0 ? _maxWidth : _maxHeight;
        }

        /// <summary> <paramref name="pSize"/> clamped by this element's bounds on one axis (0 = none). </summary>
        internal float Clamp(int pAxis, float pSize)
        {
            float max = GetMax(pAxis);
            float min = GetMin(pAxis);
            if (max > ReflowUtil.EPSILON && pSize > max)
                pSize = max;
            if (min > ReflowUtil.EPSILON && pSize < min)
                pSize = min;
            return pSize;
        }

        private ReflowLayout ParentLayout
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

        private void SetField<T>(ref T pField, T pValue)
        {
            if (EqualityComparer<T>.Default.Equals(pField, pValue))
                return;
            pField = pValue;
            OnValueChanged();
        }

        private void OnValueChanged()
        {
            if (!IsActive())
                return;
            ReflowLayout parent = ParentLayout;
            if (parent != null)
                parent.SetDirty(false);
        }

        /// <summary> Joining or leaving the parent's child set: the parent re-reads its children. </summary>
        private void OnMembershipChanged()
        {
            if (TryGetComponent(out ReflowNode node))
            {
                node.InvalidateElement();
                // Being ignored (or not) decides whether the node is its own layout root.
                if (node.IsActive())
                    node.SetDirty(true);
            }
            ReflowLayout parent = ParentLayout;
            if (parent != null)
                parent.OnChildComponentsChanged();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _isParentResolved = false;
            OnMembershipChanged();
        }

        protected override void OnDisable()
        {
            OnMembershipChanged();
            base.OnDisable();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _isParentResolved = false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _isParentResolved = false;
            OnMembershipChanged();
        }
#endif
    }
}
