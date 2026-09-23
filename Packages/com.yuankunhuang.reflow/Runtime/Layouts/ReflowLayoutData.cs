using UnityEngine;

namespace Reflow
{
    /// <summary> One laid out child for the current solve: a scene child or a managed element. </summary>
    internal struct ReflowSlot
    {
        /// <summary> Index into the scene child array, or -1. </summary>
        public int sceneIndex;
        /// <summary> Index into the entry array, or -1. </summary>
        public int entryIndex;
        /// <summary> Null for an element that is not spawned. </summary>
        public RectTransform rectTransform;
        public ReflowNode node;
        public ReflowElement element;
        /// <summary> Manual size from the element: measured as a plain rect. </summary>
        public bool isManual;
        /// <summary> Own size before the layout touches it. </summary>
        public Vector2 natural;
        /// <summary> Per axis, the size the layout imposed while measuring, or FREE. </summary>
        public Vector2 exact;
        /// <summary> Measured size. </summary>
        public Vector2 desired;
        /// <summary> Final size. </summary>
        public Vector2 size;
        /// <summary> Top-left corner in container space (x right, y down from the container's top-left). </summary>
        public Vector2 position;
        /// <summary> The cell the child sits in (whole row for linear layouts). Used for visibility and scrolling. </summary>
        public Vector2 cellPosition;
        public Vector2 cellSize;
        /// <summary> Visual row / column (linear layouts: row = visual index). </summary>
        public int row;
        public int column;
    }

    /// <summary> Cached direct child that is not a managed item. </summary>
    internal struct ReflowSceneChild
    {
        public RectTransform rectTransform;
        public GameObject gameObject;
        public ReflowElement element;
        public ReflowNode node;
        public Vector2 natural;
        public bool hasNatural;
        public Vector2 lastWritten;
        public bool hasWritten;
    }

    /// <summary> One managed (data driven) element. </summary>
    internal struct ReflowEntry
    {
        public GameObject prefab;
        public object poolKey;
        public object data;
        public Vector2 sizeHint;
        public bool hidden;
        public bool spawnedOnce;
        public ReflowItemRecord record;
        /// <summary> Slot in the last solve, or -1. </summary>
        public int slotIndex;

        // While spawned: natural size bookkeeping (see ReflowUtil.ResolveNatural).
        public Vector2 natural;
        public bool hasNatural;
        public Vector2 lastWritten;
        public bool hasWritten;

        /// <summary> Last measured size, reused as the estimate once the item is gone. </summary>
        public Vector2 measured;
        public bool hasMeasured;
    }
}
