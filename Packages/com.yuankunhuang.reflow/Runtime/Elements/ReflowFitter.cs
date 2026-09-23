using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Reflow
{
    /// <summary>
    /// Sizes its rect from a TMP text (on this object, or the first active one below it), or from another fitter.
    /// Text is measured with <see cref="TMP_Text.GetPreferredValues(float,float)"/>, so no mesh is built and no
    /// canvas update is forced. Text changes are picked up through TMP's layout-dirty callback and land in the same
    /// frame. Inside an Reflow layout the parent writes the size; on its own the fitter writes it.
    /// Padding is applied as the TMP margin, so the text keeps its own anchors and alignment.
    /// </summary>
    [AddComponentMenu("Layout/Reflow/Fitter")]
    public sealed class ReflowFitter : ReflowNode
    {
        public enum FitMode
        {
            /// <summary> Keep the rect's own size on this axis. </summary>
            Unconstrained = 0,
            /// <summary> TMP's preferred size (line metrics included). Width wraps at the max width. </summary>
            Preferred = 1,
            /// <summary> Tight bounds of the rendered glyphs. Builds the text mesh at the current width. </summary>
            Rendered = 2
        }

        private const float UNBOUNDED_SIZE = 32767f;

        [SerializeField] private FitMode _horizontalFit = FitMode.Unconstrained;
        [SerializeField] private FitMode _verticalFit = FitMode.Unconstrained;
        [SerializeField] private RectOffset _padding = new RectOffset();
        [Tooltip("Per axis, 0 = none. E.g. a button that never gets narrower than its art.")]
        [SerializeField] private Vector2 _minSize;
        [Tooltip("Per axis, 0 = none. Text wraps at the max width and the height is measured again.")]
        [SerializeField] private Vector2 _maxSize;
        [Tooltip("Follow this fitter's size (plus own padding) on the fitted axes instead of measuring text.")]
        [SerializeField] private ReflowFitter _source;
        [Tooltip("Optional. Empty: TMP on this object, else the first active one below it.")]
        [SerializeField] private TMP_Text _text;
        [Tooltip("Rendered mode: only count visible characters (typewriter effects).")]
        [SerializeField] private bool _renderedVisibleOnly = true;

        private TMP_Text _resolvedText;
        private bool _isTextResolved;
        private TMP_Text _callbackText;
        private UnityAction _onTextDirtyAction;
        private bool _isApplyingMargin;
        private bool _hasAppliedMargin;

        private bool _isPreferredValid;
        private Vector2 _preferredSize;
        private float _wrapWidth = -1f;
        private float _wrapHeight;
        private bool _isRenderedValid;
        private Vector2 _renderedSize;
        private Vector2 _lastSourceSize;

        public FitMode HorizontalFit
        {
            get => _horizontalFit;
            set { _horizontalFit = value; OnSettingsChanged(); }
        }

        public FitMode VerticalFit
        {
            get => _verticalFit;
            set { _verticalFit = value; OnSettingsChanged(); }
        }

        public RectOffset Padding
        {
            get => _padding;
            set { _padding = value ?? new RectOffset(); OnSettingsChanged(); }
        }

        public Vector2 MinSize
        {
            get => _minSize;
            set { _minSize = value; MarkDirty(); }
        }

        public Vector2 MaxSize
        {
            get => _maxSize;
            set { _maxSize = value; MarkDirty(); }
        }

        public ReflowFitter Source
        {
            get => _source;
            set { _source = value; OnSettingsChanged(); }
        }

        public TMP_Text Text
        {
            get => _text;
            set { _text = value; _isTextResolved = false; OnSettingsChanged(); }
        }

        /// <summary> Re-measures the text. Only needed for changes TMP does not report (it reports text, font and size changes). </summary>
        public void Refresh()
        {
            InvalidateTextCache();
            MarkDirty();
        }

        public override bool FollowsContent(int pAxis)
        {
            return (pAxis == 0 ? _horizontalFit : _verticalFit) != FitMode.Unconstrained;
        }

        protected override Vector2 MeasureContent(Vector2 pFixedSize)
        {
            Vector2 rectSize = RectTransform.rect.size;

            if (_source != null && _source != this)
            {
                _source.EnsureLayout();
                Vector2 sourceSize = _source.RectTransform.rect.size;
                _lastSourceSize = sourceSize;
                return new Vector2(
                    pFixedSize.x >= 0f ? pFixedSize.x : FollowsContent(0) ? Clamp(0, sourceSize.x + _padding.horizontal) : rectSize.x,
                    pFixedSize.y >= 0f ? pFixedSize.y : FollowsContent(1) ? Clamp(1, sourceSize.y + _padding.vertical) : rectSize.y);
            }

            TMP_Text text = ResolveText();
            if (text == null)
                return new Vector2(pFixedSize.x >= 0f ? pFixedSize.x : rectSize.x, pFixedSize.y >= 0f ? pFixedSize.y : rectSize.y);

            ApplyMargin(text);

            float width;
            if (pFixedSize.x >= 0f)
                width = pFixedSize.x;
            else if (_horizontalFit == FitMode.Preferred)
                width = Clamp(0, GetPreferred(text).x);
            else if (_horizontalFit == FitMode.Rendered)
                width = Clamp(0, GetRendered(text).x);
            else
                width = rectSize.x;

            float height;
            if (pFixedSize.y >= 0f)
                height = pFixedSize.y;
            else if (_verticalFit == FitMode.Preferred)
                height = Clamp(1, GetPreferredHeight(text, width));
            else if (_verticalFit == FitMode.Rendered)
                height = Clamp(1, GetRendered(text).y);
            else
                height = rectSize.y;

            return new Vector2(width, height);
        }

        protected override void ArrangeContent(Vector2 pSize)
        {
        }

        internal override void PollSources()
        {
            if (_source == null || !IsActive())
                return;
            if (!ReflowUtil.Approximately(_source.RectTransform.rect.size, _lastSourceSize))
                SetDirty(false);
        }

        private float Clamp(int pAxis, float pSize)
        {
            float max = ReflowUtil.Get(_maxSize, pAxis);
            float min = ReflowUtil.Get(_minSize, pAxis);
            if (max > ReflowUtil.EPSILON && pSize > max)
                pSize = max;
            if (min > ReflowUtil.EPSILON && pSize < min)
                pSize = min;
            return pSize;
        }

        /// <summary> Unwrapped preferred size, padding included (TMP adds its margins). </summary>
        private Vector2 GetPreferred(TMP_Text pText)
        {
            if (!_isPreferredValid)
            {
                _preferredSize = pText.GetPreferredValues(UNBOUNDED_SIZE, UNBOUNDED_SIZE);
                _isPreferredValid = true;
                _wrapWidth = -1f;
            }
            return _preferredSize;
        }

        /// <summary> Preferred height when the fitter is <paramref name="pWidth"/> wide. </summary>
        private float GetPreferredHeight(TMP_Text pText, float pWidth)
        {
            Vector2 preferred = GetPreferred(pText);
            if (pWidth >= preferred.x - ReflowUtil.EPSILON)
                return preferred.y;
            if (ReflowUtil.Approximately(pWidth, _wrapWidth))
                return _wrapHeight;

            // TMP wraps at the given width without margins, then adds the margins to the result.
            float innerWidth = Mathf.Max(0f, pWidth - _padding.horizontal);
            _wrapHeight = pText.GetPreferredValues(innerWidth, UNBOUNDED_SIZE).y;
            _wrapWidth = pWidth;
            return _wrapHeight;
        }

        private Vector2 GetRendered(TMP_Text pText)
        {
            if (!_isRenderedValid)
            {
                pText.ForceMeshUpdate();
                Vector2 rendered = pText.GetRenderedValues(_renderedVisibleOnly);
                if (!float.IsFinite(rendered.x) || !float.IsFinite(rendered.y))
                    rendered = Vector2.zero;
                _renderedSize = new Vector2(rendered.x + _padding.horizontal, rendered.y + _padding.vertical);
                _isRenderedValid = true;
            }
            return _renderedSize;
        }

        private void ApplyMargin(TMP_Text pText)
        {
            bool hasPadding = _padding.left != 0 || _padding.right != 0 || _padding.top != 0 || _padding.bottom != 0;
            if (!hasPadding && !_hasAppliedMargin)
                return;

            Vector4 margin = new Vector4(_padding.left, _padding.top, _padding.right, _padding.bottom);
            if (pText.margin == margin)
                return;
            _isApplyingMargin = true;
            try
            {
                pText.margin = margin;
            }
            finally
            {
                _isApplyingMargin = false;
            }
            _hasAppliedMargin = hasPadding;
            InvalidateTextCache();
        }

        private TMP_Text ResolveText()
        {
            if (!_isTextResolved)
            {
                _resolvedText = _text;
                if (_resolvedText == null && !TryGetComponent(out _resolvedText))
                    _resolvedText = GetComponentInChildren<TMP_Text>();
                _isTextResolved = true;
                BindTextCallback(_resolvedText);
            }
            return _resolvedText;
        }

        private void BindTextCallback(TMP_Text pText)
        {
            if (_callbackText == pText)
                return;
            if (_onTextDirtyAction == null)
                _onTextDirtyAction = OnTextLayoutDirty;
            if (_callbackText != null)
                _callbackText.UnregisterDirtyLayoutCallback(_onTextDirtyAction);
            _callbackText = pText;
            if (_callbackText != null)
                _callbackText.RegisterDirtyLayoutCallback(_onTextDirtyAction);
            InvalidateTextCache();
        }

        private void OnTextLayoutDirty()
        {
            _isRenderedValid = false;
            // TMP reports its own resizes too; a layout writing this rect is not a content change.
            if (_isApplyingMargin || ReflowScheduler.IsWriting)
                return;
            InvalidateTextCache();
            SetDirty(false);
        }

        private void InvalidateTextCache()
        {
            _isPreferredValid = false;
            _isRenderedValid = false;
            _wrapWidth = -1f;
        }

        private void OnSettingsChanged()
        {
            InvalidateTextCache();
            UpdateSourceWatch();
            SetDirty(true);
        }

        private void UpdateSourceWatch()
        {
            ReflowScheduler.SetSourceWatcher(this, _source != null && isActiveAndEnabled);
        }

        protected override void OnEnable()
        {
            _isTextResolved = false;
            ResolveText();
            UpdateSourceWatch();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            BindTextCallback(null);
            _isTextResolved = false;
            ReflowScheduler.SetSourceWatcher(this, false);
            base.OnDisable();
        }

        private void OnTransformChildrenChanged()
        {
            if (_text != null)
                return;
            _isTextResolved = false;
            MarkDirty();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            _isTextResolved = false;
            InvalidateTextCache();
            if (isActiveAndEnabled)
            {
                ResolveText();
                UpdateSourceWatch();
            }
            base.OnValidate();
        }
#endif
    }
}
