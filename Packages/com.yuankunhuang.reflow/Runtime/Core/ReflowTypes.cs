using System;
using UnityEngine;

namespace Reflow
{
    public enum ReflowScrollAlignment
    {
        Start = 0,
        Center = 1,
        End = 2
    }

    /// <summary> How a linear layout sizes its children on one axis. </summary>
    public enum ReflowChildSizeMode
    {
        /// <summary> Children keep their own size (clamped by <see cref="ReflowElement"/> min/max). </summary>
        Natural = 0,
        /// <summary> Main axis: children share the free space by flexible weight. Cross axis: children fill the container. </summary>
        Expand = 1,
        /// <summary> Every child gets the same size: the element's size hint, else the layout's default child size. </summary>
        Fixed = 2
    }

    public enum ReflowVirtualization
    {
        /// <summary> Virtualize managed elements when a viewport is found. </summary>
        Auto = 0,
        /// <summary> Same as Auto, but warns when no viewport is found. </summary>
        On = 1,
        /// <summary> Spawn every managed element. </summary>
        Off = 2
    }

    /// <summary>
    /// How a container sizes itself on one axis.
    /// With <see cref="follow"/> off the size comes from the RectTransform (or the parent layout) and the other
    /// fields are ignored. With it on the size is the content size clamped to
    /// [<see cref="min"/>, the tighter of <see cref="max"/> and the current size of <see cref="constraintSource"/>].
    /// When the cap is hit, flexible children share the capped space and the rest shrink to fit.
    /// </summary>
    [Serializable]
    public struct ReflowSizePolicy
    {
        public bool follow;
        /// <summary> Lower bound; 0 = none. Wins over the cap. </summary>
        public float min;
        /// <summary> Upper bound; 0 = none. </summary>
        public float max;
        /// <summary> Dynamic upper bound: the current size of this rect (typically a viewport or a parent slot). </summary>
        public RectTransform constraintSource;

        public static ReflowSizePolicy Follow => new ReflowSizePolicy { follow = true };
    }

    public enum ReflowElementChangeKind
    {
        Inserted = 0,
        Removed = 1,
        Reordered = 2,
        Cleared = 3
    }

    /// <summary> A structural change of the managed element list. Lets listeners remap indices they keep. </summary>
    public readonly struct ReflowElementChange
    {
        public readonly ReflowElementChangeKind kind;
        public readonly int index;
        public readonly int count;
        private readonly int[] _oldToNewArray;

        internal ReflowElementChange(ReflowElementChangeKind pKind, int pIndex, int pCount, int[] pOldToNewArray = null)
        {
            kind = pKind;
            index = pIndex;
            count = pCount;
            _oldToNewArray = pOldToNewArray;
        }

        /// <summary>
        /// New index of the element that was at <paramref name="pOldIndex"/>, or -1 when it no longer exists.
        /// For <see cref="ReflowElementChangeKind.Reordered"/> the mapping is only valid during the callback.
        /// </summary>
        public int MapIndex(int pOldIndex)
        {
            if (pOldIndex < 0)
                return -1;
            switch (kind)
            {
                case ReflowElementChangeKind.Inserted:
                    return pOldIndex >= index ? pOldIndex + count : pOldIndex;
                case ReflowElementChangeKind.Removed:
                    if (pOldIndex < index)
                        return pOldIndex;
                    return pOldIndex >= index + count ? pOldIndex - count : -1;
                case ReflowElementChangeKind.Reordered:
                    return _oldToNewArray != null && pOldIndex < _oldToNewArray.Length ? _oldToNewArray[pOldIndex] : -1;
                default:
                    return -1;
            }
        }
    }

    /// <summary> Where an element sits in the viewport; see <see cref="ReflowLayout.CaptureScrollAnchor"/>. </summary>
    public struct ReflowScrollAnchor
    {
        public int index;
        /// <summary> Distance from the viewport start to the element start along the scroll axis. </summary>
        public float offset;

        public bool IsValid => index >= 0;

        public static ReflowScrollAnchor None => new ReflowScrollAnchor { index = -1 };
    }
}
