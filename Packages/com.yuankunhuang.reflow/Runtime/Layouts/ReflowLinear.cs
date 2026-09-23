using UnityEngine;

namespace Reflow
{
    /// <summary> Children in a row (<see cref="ReflowHorizontal"/>) or a column (<see cref="ReflowVertical"/>). </summary>
    public abstract class ReflowLinear : ReflowLayout
    {
        [SerializeField] protected float _spacing;
        [SerializeField] protected TextAnchor _childAlignment = TextAnchor.UpperLeft;
        [Tooltip("Along the layout direction. Expand shares the free space by flexible weight (needs a size that is not following content).")]
        [SerializeField] protected ReflowChildSizeMode _mainAxisMode = ReflowChildSizeMode.Natural;
        [Tooltip("Across the layout direction. Expand fills the container.")]
        [SerializeField] protected ReflowChildSizeMode _crossAxisMode = ReflowChildSizeMode.Natural;
        [Tooltip("Children keep their own size and are scaled to the size the layout gives them.")]
        [SerializeField] protected bool _scaleToFit;
        [Tooltip("Last child first. Element indices do not change.")]
        [SerializeField] protected bool _reverseArrangement;

        private ReflowAxisSolver.ChildInput[] _mainInputArray = new ReflowAxisSolver.ChildInput[16];
        private ReflowAxisSolver.ChildInput[] _crossInputArray = new ReflowAxisSolver.ChildInput[16];
        private float[] _mainSizeArray = new float[16];
        private float[] _crossSizeArray = new float[16];
        private float[] _resolvedCrossArray = new float[16];

        /// <summary> 0 = horizontal row, 1 = vertical column. </summary>
        protected abstract int MainAxis { get; }

        public float Spacing
        {
            get => _spacing;
            set { _spacing = value; MarkDirty(); }
        }

        public TextAnchor ChildAlignment
        {
            get => _childAlignment;
            set { _childAlignment = value; MarkDirty(); }
        }

        public ReflowChildSizeMode MainAxisMode
        {
            get => _mainAxisMode;
            set { _mainAxisMode = value; SetDirty(true); }
        }

        public ReflowChildSizeMode CrossAxisMode
        {
            get => _crossAxisMode;
            set { _crossAxisMode = value; SetDirty(true); }
        }

        public bool ScaleToFit
        {
            get => _scaleToFit;
            set { _scaleToFit = value; SetDirty(true); }
        }

        public bool ReverseArrangement
        {
            get => _reverseArrangement;
            set { _reverseArrangement = value; MarkDirty(); }
        }

        internal override int PrimaryAxis => MainAxis;

        internal override bool IsPrimaryAscending => !_reverseArrangement;

        internal override bool UsesScaleToFit => _scaleToFit;

        internal override Vector2 Solve(Vector2 pFixedSize)
        {
            int count = _slotCount;
            EnsureScratch(count);

            int main = MainAxis;
            int cross = 1 - main;
            float padMain = main == 0 ? _padding.horizontal : _padding.vertical;
            float padCross = main == 0 ? _padding.vertical : _padding.horizontal;
            float padMainStart = main == 0 ? _padding.left : _padding.top;
            float padCrossStart = main == 0 ? _padding.top : _padding.left;
            float spacingTotal = count > 1 ? (count - 1) * _spacing : 0f;
            float fixedMain = ReflowUtil.Get(pFixedSize, main);
            float fixedCross = ReflowUtil.Get(pFixedSize, cross);
            bool followMain = FollowsContent(main);
            bool followCross = FollowsContent(cross);
            bool expandCross = _crossAxisMode == ReflowChildSizeMode.Expand;

            // Cross size known up front: Expand children get it while measuring (so their content wraps right).
            float innerCross = fixedCross >= 0f ? Mathf.Max(0f, fixedCross - padCross) : -1f;
            bool useResolvedCross = false;
            float mainSize = 0f;
            float crossSize = 0f;
            float availMain = 0f;
            float availCross = 0f;

            for (int round = 0; round < 2; round++)
            {
                // 1. Measure every child with the sizes the layout already knows it will give.
                for (int i = 0; i < count; i++)
                {
                    ref ReflowSlot slot = ref _slotArray[i];
                    ReflowElement element = slot.element;
                    bool controlsMain = element == null || element.LayoutControls(main);
                    bool controlsCross = element == null || element.LayoutControls(cross);

                    Vector2 exact = ReflowUtil.FREE_SIZE;
                    if (controlsMain && _mainAxisMode == ReflowChildSizeMode.Fixed)
                        ReflowUtil.Set(ref exact, main, GetFixedChildSize(ref slot, main));
                    if (controlsCross)
                    {
                        if (useResolvedCross)
                            ReflowUtil.Set(ref exact, cross, _resolvedCrossArray[i]);
                        else if (_crossAxisMode == ReflowChildSizeMode.Fixed)
                            ReflowUtil.Set(ref exact, cross, GetFixedChildSize(ref slot, cross));
                        else if (expandCross && innerCross >= 0f)
                            ReflowUtil.Set(ref exact, cross, element != null ? element.Clamp(cross, innerCross) : innerCross);
                    }

                    Vector2 desired = MeasureSlot(ref slot, exact);
                    _mainInputArray[i] = ReflowAxisSolver.CreateInput(ReflowUtil.Get(desired, main), element, main, controlsMain);
                    _crossInputArray[i] = ReflowAxisSolver.CreateInput(ReflowUtil.Get(desired, cross), element, cross, controlsCross);
                }

                // 2. Main axis: container size, then the children's share of it.
                float contentMain = padMain + spacingTotal;
                for (int i = 0; i < count; i++)
                    contentMain += _mainInputArray[i].EffectiveNatural;
                float followedMain = followMain ? ApplyPolicy(main, contentMain) : contentMain;
                mainSize = fixedMain >= 0f ? fixedMain : followedMain;
                SetContentSize(main, followMain ? followedMain : mainSize);
                availMain = Mathf.Max(0f, mainSize - padMain);

                ReflowAxisSolver.MainAxisMode mode = ReflowAxisSolver.MainAxisMode.Natural;
                if (_mainAxisMode == ReflowChildSizeMode.Expand && fixedMain >= 0f)
                    mode = ReflowAxisSolver.MainAxisMode.Expand;
                else if (followMain && contentMain > mainSize + ReflowAxisSolver.OVERFLOW_EPSILON)
                    mode = ReflowAxisSolver.MainAxisMode.Capped;
                ReflowAxisSolver.Distribute(count, mode, availMain, spacingTotal, _mainInputArray, _mainSizeArray);

                // 3. A child given another main size may wrap differently: measure its cross size again.
                for (int i = 0; i < count; i++)
                {
                    ref ReflowSlot slot = ref _slotArray[i];
                    ReflowNode node = slot.node;
                    if (node == null || slot.isManual || !node.FollowsContent(cross) || !_mainInputArray[i].layoutControls)
                        continue;
                    float given = _mainSizeArray[i];
                    if (ReflowUtil.Approximately(given, ReflowUtil.Get(slot.desired, main)))
                        continue;
                    Vector2 exact = slot.exact;
                    ReflowUtil.Set(ref exact, main, given);
                    _crossInputArray[i].natural = ReflowUtil.Get(MeasureSlot(ref slot, exact), cross);
                }

                // 4. Cross axis.
                float contentCross = 0f;
                for (int i = 0; i < count; i++)
                    contentCross = Mathf.Max(contentCross, _crossInputArray[i].EffectiveNatural);
                contentCross += padCross;
                float followedCross = followCross ? ApplyPolicy(cross, contentCross) : contentCross;
                crossSize = fixedCross >= 0f ? fixedCross : followedCross;
                SetContentSize(cross, followCross ? followedCross : crossSize);
                availCross = Mathf.Max(0f, crossSize - padCross);
                bool crossCapped = followCross && contentCross > crossSize + ReflowAxisSolver.OVERFLOW_EPSILON;
                for (int i = 0; i < count; i++)
                    _crossSizeArray[i] = ReflowAxisSolver.ResolveCross(_crossInputArray[i], availCross, expandCross, crossCapped);

                // 5. The cross size was unknown while measuring. If children that size their main axis from content
                //    got a different cross size, measure once more with it.
                if (round > 0 || innerCross >= 0f || !(expandCross || crossCapped) || !CrossChangesMain(count, main, cross))
                    break;
                for (int i = 0; i < count; i++)
                    _resolvedCrossArray[i] = _crossSizeArray[i];
                useResolvedCross = true;
            }

            // 6. Positions.
            float totalMain = ReflowAxisSolver.Sum(count, _spacing, _mainSizeArray);
            float cursor = padMainStart + (availMain - totalMain) * ReflowUtil.AlignFactor(_childAlignment, main);
            float crossAlign = ReflowUtil.AlignFactor(_childAlignment, cross);
            for (int k = 0; k < count; k++)
            {
                int i = _reverseArrangement ? count - 1 - k : k;
                if (k > 0)
                    cursor += _spacing;
                ref ReflowSlot slot = ref _slotArray[i];
                float childMain = _mainSizeArray[i];
                float childCross = _crossSizeArray[i];
                slot.size = ReflowUtil.Compose(main, childMain, childCross);
                slot.position = ReflowUtil.Compose(main, cursor, padCrossStart + (availCross - childCross) * crossAlign);
                slot.cellPosition = ReflowUtil.Compose(main, cursor, padCrossStart);
                slot.cellSize = ReflowUtil.Compose(main, childMain, availCross);
                slot.row = k;
                slot.column = 0;
                cursor += childMain;
            }

            return ReflowUtil.Compose(main, mainSize, crossSize);
        }

        private bool CrossChangesMain(int pCount, int pMain, int pCross)
        {
            for (int i = 0; i < pCount; i++)
            {
                ref ReflowSlot slot = ref _slotArray[i];
                if (slot.node == null || slot.isManual || !slot.node.FollowsContent(pMain) || !_crossInputArray[i].layoutControls)
                    continue;
                if (!ReflowUtil.Approximately(_crossSizeArray[i], ReflowUtil.Get(slot.desired, pCross)))
                    return true;
            }
            return false;
        }

        private void EnsureScratch(int pCount)
        {
            if (_mainInputArray.Length >= pCount)
                return;
            int capacity = Mathf.NextPowerOfTwo(pCount);
            _mainInputArray = new ReflowAxisSolver.ChildInput[capacity];
            _crossInputArray = new ReflowAxisSolver.ChildInput[capacity];
            _mainSizeArray = new float[capacity];
            _crossSizeArray = new float[capacity];
            _resolvedCrossArray = new float[capacity];
        }
    }
}
