using System.Collections.Generic;
using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Plays an appear animation for elements the first time they spawn, staggered one after another
    /// (or by diagonal for grids). Items are spawned and laid out right away; only the visuals are delayed.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ReflowLayout))]
    [AddComponentMenu("Layout/Reflow/Spawn Animator")]
    public sealed class ReflowSpawnAnimator : MonoBehaviour
    {
        public enum Effect
        {
            Fade = 0,
            FadeSlide = 1,
            Scale = 2
        }

        public enum Order
        {
            /// <summary> In element order. </summary>
            Sequential = 0,
            /// <summary> By row + column, so a grid fills in diagonal waves. </summary>
            Diagonal = 1
        }

        private static readonly Comparer<ReflowItemRecord> ENTRY_ORDER_COMPARER =
            Comparer<ReflowItemRecord>.Create((pA, pB) => pA.entryIndex.CompareTo(pB.entryIndex));

        [SerializeField] private Effect _effect = Effect.FadeSlide;
        [SerializeField] private Order _order = Order.Sequential;
        [SerializeField] private float _duration = 0.3f;
        [Tooltip("Delay between one item and the next.")]
        [SerializeField] private float _stagger = 0.05f;
        [Tooltip("Longest delay any item waits, so long lists do not trickle in forever.")]
        [SerializeField] private float _maxDelay = 0.6f;
        [Tooltip("FadeSlide: where items start, relative to their place (anchored position units).")]
        [SerializeField] private Vector2 _slideOffset = new Vector2(0f, -40f);
        [Tooltip("Scale: starting scale.")]
        [SerializeField] private float _startScale = 0.6f;

        private ReflowLayout _layout;
        private bool _suppressNext;
        private readonly List<ReflowItemRecord> _sortList = new List<ReflowItemRecord>();

        public Effect SpawnEffect
        {
            get => _effect;
            set => _effect = value;
        }

        public Order SpawnOrder
        {
            get => _order;
            set => _order = value;
        }

        public float Duration
        {
            get => _duration;
            set => _duration = value;
        }

        public float Stagger
        {
            get => _stagger;
            set => _stagger = value;
        }

        /// <summary> Whether an appear animation is still running (or waiting for its delay). </summary>
        public bool HasPendingSpawns => Layout != null && Layout.IsAnimating;

        private ReflowLayout Layout
        {
            get
            {
                if (_layout == null)
                    TryGetComponent(out _layout);
                return _layout;
            }
        }

        /// <summary> The next batch of new items appears without animation (e.g. when restoring a list). </summary>
        public void SuppressNextSpawnAnimation()
        {
            _suppressNext = true;
        }

        /// <summary> Elements animate again on their next spawn. </summary>
        public void ResetSpawnHistory()
        {
            if (Layout != null)
                Layout.ResetSpawnHistory();
        }

        /// <summary> Jumps every running appear animation to its end. </summary>
        public void CompleteSpawnAnimations()
        {
            if (Layout != null)
                ReflowAnimationDriver.CompleteAll(Layout);
        }

        internal void Play(List<ReflowItemRecord> pRecordList)
        {
            if (_suppressNext)
            {
                _suppressNext = false;
                return;
            }

            _sortList.Clear();
            for (int i = 0; i < pRecordList.Count; i++)
            {
                ReflowItemRecord record = pRecordList[i];
                if (record.entryIndex >= 0 && !record.isInPool)
                    _sortList.Add(record);
            }
            if (_sortList.Count == 0)
                return;
            _sortList.Sort(ENTRY_ORDER_COMPARER);

            int baseKey = int.MaxValue;
            if (_order == Order.Diagonal)
            {
                for (int i = 0; i < _sortList.Count; i++)
                    baseKey = Mathf.Min(baseKey, GetDiagonalKey(_sortList[i]));
            }

            for (int i = 0; i < _sortList.Count; i++)
            {
                ReflowItemRecord record = _sortList[i];
                int step = _order == Order.Diagonal ? GetDiagonalKey(record) - baseKey : i;
                float delay = Mathf.Min(step * _stagger, _maxDelay);
                switch (_effect)
                {
                    case Effect.Fade:
                        ReflowAnimationDriver.StartAppear(record, Vector2.zero, 0f, 1f, delay, _duration);
                        break;
                    case Effect.Scale:
                        ReflowAnimationDriver.StartAppear(record, Vector2.zero, 0f, _startScale, delay, _duration);
                        break;
                    default:
                        ReflowAnimationDriver.StartAppear(record, _slideOffset, 0f, 1f, delay, _duration);
                        break;
                }
            }
            _sortList.Clear();
        }

        private int GetDiagonalKey(ReflowItemRecord pRecord)
        {
            Vector2Int coordinate = Layout.GetGridCoordinate(pRecord.entryIndex);
            return coordinate.x + coordinate.y;
        }

        private void OnEnable()
        {
            if (Layout != null)
                Layout._spawnAnimator = this;
        }

        private void OnDisable()
        {
            if (_layout != null && _layout._spawnAnimator == this)
            {
                _layout._spawnAnimator = null;
                ReflowAnimationDriver.CompleteAll(_layout);
            }
        }
    }
}
