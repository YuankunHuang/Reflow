using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reflow
{
    /// <summary>
    /// ScrollRect that settles the content's Reflow layout before a normalized position is applied (so it is
    /// computed from this frame's content size) and scrolls to elements, animated or not. Scrolling to an element
    /// follows it: when rows on the way are spawned and turn out to have another size than estimated, the target
    /// moves with the element instead of landing on a stale position.
    /// A plain ScrollRect works with Reflow layouts too; this adds the read barrier and the helpers.
    /// </summary>
    [AddComponentMenu("Layout/Reflow/Scroll Rect")]
    public class ReflowScrollRect : ScrollRect
    {
        private const int SETTLE_FRAMES = 3;

        private ReflowNode _contentNode;
        private RectTransform _cachedContent;
        private bool _isTweening;
        private float _tweenStart;
        private float _tweenDuration;
        private Vector2 _tweenFrom;
        private Vector2 _tweenTo;

        // Element being scrolled to, re-targeted every frame until it settles.
        private ReflowLayout _trackLayout;
        private int _trackIndex;
        private ReflowScrollAlignment _trackAlignment;
        private bool _trackReveal;
        private float _trackMargin;
        private int _settleFrameCount;

        public bool IsScrollingTo => _isTweening || _settleFrameCount > 0;

        /// <summary> Runs the content layout now if it is pending. </summary>
        public void EnsureContentLayout()
        {
            if (content != _cachedContent)
            {
                _cachedContent = content;
                _contentNode = content != null ? content.GetComponent<ReflowNode>() : null;
            }
            if (_contentNode != null)
                _contentNode.EnsureLayout();
        }

        /// <summary> Scrolls so element <paramref name="pIndex"/> of <paramref name="pLayout"/> sits at the viewport's start / center / end. </summary>
        public bool ScrollToElement(ReflowLayout pLayout, int pIndex, ReflowScrollAlignment pAlignment = ReflowScrollAlignment.Start, float pDuration = 0f)
        {
            return Track(pLayout, pIndex, pAlignment, false, 0f, pDuration);
        }

        /// <summary> Scrolls as little as needed to show element <paramref name="pIndex"/> fully. </summary>
        public bool RevealElement(ReflowLayout pLayout, int pIndex, float pMargin = 0f, float pDuration = 0f)
        {
            return Track(pLayout, pIndex, ReflowScrollAlignment.Start, true, pMargin, pDuration);
        }

        /// <summary> Moves the content to <paramref name="pPosition"/> (anchoredPosition), stopping inertia. </summary>
        public void ScrollToContentPosition(Vector2 pPosition, float pDuration = 0f)
        {
            _trackLayout = null;
            StartScroll(pPosition, pDuration);
        }

        public void StopScrollTo()
        {
            _isTweening = false;
            _trackLayout = null;
            _settleFrameCount = 0;
        }

        /// <summary> The layout moved the content (scroll anchoring); keep drags and animations continuous. </summary>
        internal void OnContentShifted(Vector2 pDelta)
        {
            m_ContentStartPosition += pDelta;
            _tweenFrom += pDelta;
            _tweenTo += pDelta;
        }

        protected override void SetNormalizedPosition(float pValue, int pAxis)
        {
            EnsureContentLayout();
            base.SetNormalizedPosition(pValue, pAxis);
        }

        protected override void LateUpdate()
        {
            // Before the base update, so it reports the move (and the layout updates visibility) this frame.
            if (content != null)
            {
                if (_isTweening)
                {
                    if (_trackLayout != null && TryGetTrackedTarget(out Vector2 target))
                        _tweenTo = target;
                    float t = Mathf.Clamp01((Time.unscaledTime - _tweenStart) / _tweenDuration);
                    float inverse = 1f - t;
                    content.anchoredPosition = Vector2.LerpUnclamped(_tweenFrom, _tweenTo, 1f - inverse * inverse * inverse);
                    velocity = Vector2.zero;
                    if (t >= 1f)
                    {
                        _isTweening = false;
                        _settleFrameCount = _trackLayout != null ? SETTLE_FRAMES : 0;
                    }
                }
                else if (_settleFrameCount > 0)
                {
                    _settleFrameCount--;
                    if (TryGetTrackedTarget(out Vector2 target) && (content.anchoredPosition - target).sqrMagnitude > 0.01f)
                    {
                        content.anchoredPosition = target;
                        velocity = Vector2.zero;
                    }
                    if (_settleFrameCount == 0)
                        _trackLayout = null;
                }
            }
            base.LateUpdate();
        }

        public override void OnBeginDrag(PointerEventData pEventData)
        {
            StopScrollTo();
            base.OnBeginDrag(pEventData);
        }

        public override void OnScroll(PointerEventData pData)
        {
            StopScrollTo();
            base.OnScroll(pData);
        }

        protected override void OnDisable()
        {
            StopScrollTo();
            base.OnDisable();
        }

        private bool Track(ReflowLayout pLayout, int pIndex, ReflowScrollAlignment pAlignment, bool pReveal, float pMargin, float pDuration)
        {
            if (pLayout == null || content == null || pIndex < 0 || pIndex >= pLayout.ElementCount)
                return false;
            _trackLayout = pLayout;
            _trackIndex = pIndex;
            _trackAlignment = pAlignment;
            _trackReveal = pReveal;
            _trackMargin = pMargin;
            _settleFrameCount = 0;
            if (!TryGetTrackedTarget(out Vector2 target))
            {
                _trackLayout = null;
                return false;
            }
            StartScroll(target, pDuration);
            return true;
        }

        private bool TryGetTrackedTarget(out Vector2 pTarget)
        {
            pTarget = default;
            ReflowLayout layout = _trackLayout;
            if (layout == null || _trackIndex >= layout.ElementCount)
                return false;
            EnsureContentLayout();
            layout.EnsureLayout();
            if (!layout.TryGetEntrySlot(_trackIndex, out _))
                return false;
            Rect rect = layout.GetElementRect(_trackIndex);
            pTarget = _trackReveal
                ? ReflowScrollUtil.GetRevealContentPosition(this, layout.RectTransform, rect, _trackMargin)
                : ReflowScrollUtil.GetAlignedContentPosition(this, layout.RectTransform, rect, _trackAlignment);
            return true;
        }

        private void StartScroll(Vector2 pTarget, float pDuration)
        {
            StopMovement();
            if (pDuration > 0f)
            {
                _isTweening = true;
                _tweenStart = Time.unscaledTime;
                _tweenDuration = pDuration;
                _tweenFrom = content.anchoredPosition;
                _tweenTo = pTarget;
                return;
            }

            // Jump. Spawning at the target may correct estimated sizes and move a tracked element: follow it.
            _isTweening = false;
            for (int i = 0; i <= SETTLE_FRAMES; i++)
            {
                content.anchoredPosition = pTarget;
                RefreshContentVisibility();
                if (_trackLayout == null || !TryGetTrackedTarget(out Vector2 next) || (next - pTarget).sqrMagnitude <= 0.01f)
                    break;
                pTarget = next;
            }
            _trackLayout = null;
        }

        /// <summary> Layouts under the content spawn what the jump brought into view right away, so it can be read back. </summary>
        private void RefreshContentVisibility()
        {
            EnsureContentLayout();
            if (_contentNode is ReflowLayout layout)
                layout.RefreshVisibility();
            if (_trackLayout != null && !ReferenceEquals(_trackLayout, _contentNode))
                _trackLayout.RefreshVisibility();
        }
    }
}
