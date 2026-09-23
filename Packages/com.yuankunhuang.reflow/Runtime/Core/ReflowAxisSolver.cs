using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Sizes children along one axis. Pure math over <see cref="ChildInput"/> values, so it can be reasoned
    /// about and unit tested without components. Positions are the caller's job.
    /// </summary>
    internal static class ReflowAxisSolver
    {
        internal const float SIZE_EPSILON = 0.0001f;
        internal const float OVERFLOW_EPSILON = 0.001f;

        // Layout runs on the main thread only, so shared scratch buffers are safe.
        private static bool[] _flexActiveArray = new bool[16];
        private static float[] _weightArray = new float[16];

        internal enum MainAxisMode
        {
            /// <summary> Children keep their size; overflow is left to masks and scrolling. </summary>
            Natural = 0,
            /// <summary> The container followed its content up to a cap: flexible children share what is left, else everyone shrinks. </summary>
            Capped = 1,
            /// <summary> The container size is given: children are grown (or shrunk) to fill it. </summary>
            Expand = 2
        }

        /// <summary> Per-child input for one axis. </summary>
        internal struct ChildInput
        {
            /// <summary> Size the child asks for. </summary>
            public float natural;
            /// <summary> Whether the layout may change this child's size on this axis. </summary>
            public bool layoutControls;
            /// <summary> Whether an <see cref="ReflowElement"/> is attached. Plain children never flex when capped. </summary>
            public bool hasElement;
            /// <summary> Flexible weight; &lt;= 0 means not flexible. </summary>
            public float flexWeight;
            /// <summary> Lower bound; &lt;= 0 means none. </summary>
            public float minSize;
            /// <summary> Upper bound; &lt;= 0 means none. </summary>
            public float maxSize;

            public float Clamp(float pSize)
            {
                float size = pSize;
                if (maxSize > SIZE_EPSILON && size > maxSize)
                    size = maxSize;
                if (minSize > SIZE_EPSILON && size < minSize)
                    size = minSize;
                return size;
            }

            /// <summary> Starting size. Bounds apply only when the layout writes this axis. </summary>
            public float EffectiveNatural => layoutControls ? Clamp(natural) : natural;
        }

        internal static ChildInput CreateInput(float pNatural, ReflowElement pElement, int pAxis, bool pLayoutControls)
        {
            if (pElement == null)
            {
                return new ChildInput
                {
                    natural = pNatural,
                    layoutControls = pLayoutControls,
                    flexWeight = 1f
                };
            }

            return new ChildInput
            {
                natural = pNatural,
                layoutControls = pLayoutControls,
                hasElement = true,
                flexWeight = pElement.GetFlexible(pAxis),
                minSize = pElement.GetMin(pAxis),
                maxSize = pElement.GetMax(pAxis)
            };
        }

        internal static void Distribute(
            int pCount,
            MainAxisMode pMode,
            float pAvail,
            float pSpacingTotal,
            ChildInput[] pInputArray,
            float[] pOutArray)
        {
            switch (pMode)
            {
                case MainAxisMode.Capped:
                    DistributeCapped(pCount, pAvail, pSpacingTotal, pInputArray, pOutArray);
                    return;
                case MainAxisMode.Expand:
                    DistributeExpand(pCount, pAvail, pSpacingTotal, pInputArray, pOutArray);
                    return;
                default:
                    for (int i = 0; i < pCount; i++)
                        pOutArray[i] = pInputArray[i].EffectiveNatural;
                    return;
            }
        }

        /// <summary> Cross-axis size of one child. </summary>
        internal static float ResolveCross(ChildInput pInput, float pAvail, bool pExpand, bool pCapped)
        {
            if (!pInput.layoutControls)
                return pInput.natural;
            if (pExpand)
                return pInput.Clamp(pAvail);
            float natural = pInput.EffectiveNatural;
            return pCapped ? pInput.Clamp(Mathf.Min(natural, pAvail)) : natural;
        }

        internal static float Sum(int pCount, float pSpacing, float[] pSizeArray)
        {
            float total = 0f;
            for (int i = 0; i < pCount; i++)
            {
                if (i > 0)
                    total += pSpacing;
                total += pSizeArray[i];
            }
            return total;
        }

        /// <summary>
        /// Splits <paramref name="pSpace"/> among the children flagged in <see cref="_flexActiveArray"/> by
        /// <see cref="_weightArray"/>. A child that hits a bound is frozen there and the space it gave back (or
        /// took) is split again among the rest; only when nobody can absorb it does the result over- or underflow.
        /// </summary>
        private static void DistributeWeighted(int pCount, float pSpace, ChildInput[] pInputArray, float[] pOutArray)
        {
            float pool = Mathf.Max(0f, pSpace);

            // Every round freezes at least one child, so pCount rounds is enough.
            for (int round = 0; round < pCount; round++)
            {
                float weightSum = 0f;
                for (int i = 0; i < pCount; i++)
                {
                    if (_flexActiveArray[i])
                        weightSum += _weightArray[i];
                }
                if (weightSum <= SIZE_EPSILON)
                    return;

                bool frozeAny = false;
                float frozenSpace = 0f;
                for (int i = 0; i < pCount; i++)
                {
                    if (!_flexActiveArray[i])
                        continue;

                    float share = pool * (_weightArray[i] / weightSum);
                    float clamped = pInputArray[i].Clamp(share);
                    pOutArray[i] = clamped;
                    if (Mathf.Abs(clamped - share) > SIZE_EPSILON)
                    {
                        _flexActiveArray[i] = false;
                        frozenSpace += clamped;
                        frozeAny = true;
                    }
                }

                if (!frozeAny)
                    return;
                pool = Mathf.Max(0f, pool - frozenSpace);
            }
        }

        private static void EnsureScratch(int pCount)
        {
            if (pCount <= _flexActiveArray.Length)
                return;
            int capacity = Mathf.NextPowerOfTwo(pCount);
            _flexActiveArray = new bool[capacity];
            _weightArray = new float[capacity];
        }

        /// <summary>
        /// Content hit the container's cap. Flexible children (an element with weight &gt; 0) take whatever the others
        /// leave; when even natural sizes do not fit, everything the layout controls shrinks.
        /// </summary>
        private static void DistributeCapped(int pCount, float pAvail, float pSpacingTotal, ChildInput[] pInputArray, float[] pOutArray)
        {
            EnsureScratch(pCount);

            float fixedSum = 0f;
            float flexWeightSum = 0f;
            for (int i = 0; i < pCount; i++)
            {
                ChildInput input = pInputArray[i];
                bool isFlex = input.hasElement && input.layoutControls && input.flexWeight > 0f;
                _flexActiveArray[i] = isFlex;
                _weightArray[i] = isFlex ? input.flexWeight : 0f;
                if (isFlex)
                {
                    flexWeightSum += input.flexWeight;
                    continue;
                }
                pOutArray[i] = input.EffectiveNatural;
                fixedSum += pOutArray[i];
            }

            float remaining = pAvail - fixedSum - pSpacingTotal;
            if (remaining < -OVERFLOW_EPSILON)
            {
                ShrinkToFit(pCount, pAvail, pSpacingTotal, pInputArray, pOutArray);
                return;
            }
            if (flexWeightSum > SIZE_EPSILON)
                DistributeWeighted(pCount, remaining, pInputArray, pOutArray);
        }

        /// <summary>
        /// Natural sizes do not fit. Children the layout controls scale down in proportion, children at their min stop
        /// and hand the rest on; spacing and uncontrolled children keep their size.
        /// </summary>
        private static void ShrinkToFit(int pCount, float pAvail, float pSpacingTotal, ChildInput[] pInputArray, float[] pOutArray)
        {
            EnsureScratch(pCount);

            float uncontrolledSum = 0f;
            for (int i = 0; i < pCount; i++)
            {
                ChildInput input = pInputArray[i];
                float natural = input.EffectiveNatural;
                _flexActiveArray[i] = input.layoutControls;
                _weightArray[i] = input.layoutControls ? natural : 0f;
                if (input.layoutControls)
                {
                    // Weights can all be zero, in which case the distributor leaves the output untouched.
                    pOutArray[i] = input.Clamp(0f);
                }
                else
                {
                    pOutArray[i] = natural;
                    uncontrolledSum += natural;
                }
            }

            DistributeWeighted(pCount, pAvail - pSpacingTotal - uncontrolledSum, pInputArray, pOutArray);
        }

        /// <summary>
        /// Container size is given. Per child:
        /// not controlled → natural; weight &gt; 0 (plain children count as 1) → share the free space by weight;
        /// weight == 0 → keep natural size, or scale in proportion when nobody is flexible.
        /// </summary>
        private static void DistributeExpand(int pCount, float pAvail, float pSpacingTotal, ChildInput[] pInputArray, float[] pOutArray)
        {
            EnsureScratch(pCount);

            float fixedSum = 0f;
            float flexWeightSum = 0f;
            float scalableSum = 0f;
            for (int i = 0; i < pCount; i++)
            {
                ChildInput input = pInputArray[i];
                _flexActiveArray[i] = false;
                _weightArray[i] = 0f;

                if (!input.layoutControls)
                {
                    pOutArray[i] = input.natural;
                    fixedSum += pOutArray[i];
                    continue;
                }

                if (input.flexWeight > 0f)
                {
                    _flexActiveArray[i] = true;
                    _weightArray[i] = input.flexWeight;
                    flexWeightSum += input.flexWeight;
                }
                else
                {
                    scalableSum += input.EffectiveNatural;
                }
            }

            if (flexWeightSum > 0f)
            {
                for (int i = 0; i < pCount; i++)
                {
                    if (pInputArray[i].layoutControls && !_flexActiveArray[i])
                        pOutArray[i] = pInputArray[i].EffectiveNatural;
                }
                DistributeWeighted(pCount, pAvail - fixedSum - pSpacingTotal - scalableSum, pInputArray, pOutArray);
                return;
            }

            // Nobody is flexible: every controlled child scales with its natural size.
            for (int i = 0; i < pCount; i++)
            {
                ChildInput input = pInputArray[i];
                if (!input.layoutControls)
                    continue;
                _flexActiveArray[i] = true;
                _weightArray[i] = input.EffectiveNatural;
                pOutArray[i] = input.Clamp(0f);
            }
            DistributeWeighted(pCount, pAvail - fixedSum - pSpacingTotal, pInputArray, pOutArray);
        }
    }
}
