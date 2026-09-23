using UnityEngine;

namespace Reflow
{
    /// <summary> Children in equal cells, filled row by row (or column by column) from a start corner. </summary>
    [AddComponentMenu("Layout/Reflow/Grid")]
    public class ReflowGrid : ReflowLayout
    {
        public enum Corner
        {
            UpperLeft = 0,
            UpperRight = 1,
            LowerLeft = 2,
            LowerRight = 3
        }

        public enum StartAxis
        {
            /// <summary> Fill a row, then the next row. </summary>
            Horizontal = 0,
            /// <summary> Fill a column, then the next column. </summary>
            Vertical = 1
        }

        public enum Constraint
        {
            /// <summary> As many columns (rows) as fit the width (height). Following content: one row (column), or as many as fit the cap. </summary>
            Flexible = 0,
            FixedColumnCount = 1,
            FixedRowCount = 2
        }

        public enum CellFit
        {
            /// <summary> Children take the cell size (axes their element lets the layout control). </summary>
            Stretch = 0,
            /// <summary> Children keep their size, centered in the cell. </summary>
            Natural = 1,
            /// <summary> Children keep their size and are scaled to the cell. </summary>
            ScaleToFit = 2
        }

        [SerializeField] protected Vector2 _cellSize = new Vector2(100f, 100f);
        [SerializeField] protected Vector2 _spacing;
        [SerializeField] protected Corner _startCorner = Corner.UpperLeft;
        [SerializeField] protected StartAxis _startAxis = StartAxis.Horizontal;
        [SerializeField] protected Constraint _constraint = Constraint.Flexible;
        [SerializeField] protected int _constraintCount = 2;
        [Tooltip("Where the block of cells sits when the container is larger.")]
        [SerializeField] protected TextAnchor _childAlignment = TextAnchor.UpperLeft;
        [Tooltip("With a single row (column), size the block to the items so the alignment centers them.")]
        [SerializeField] protected bool _centerSingleRow;
        [SerializeField] protected CellFit _cellFit = CellFit.Stretch;

        /// <summary> Block inserted under a row by <see cref="ReflowExpandableGrid"/>, container space (x right, y down). </summary>
        internal Rect _expansionRect;
        internal bool _hasExpansion;

        public Vector2 CellSize
        {
            get => _cellSize;
            set { _cellSize = value; MarkDirty(); }
        }

        public Vector2 Spacing
        {
            get => _spacing;
            set { _spacing = value; MarkDirty(); }
        }

        public Corner StartCorner
        {
            get => _startCorner;
            set { _startCorner = value; MarkDirty(); }
        }

        public StartAxis Axis
        {
            get => _startAxis;
            set { _startAxis = value; MarkDirty(); }
        }

        public Constraint GridConstraint
        {
            get => _constraint;
            set { _constraint = value; MarkDirty(); }
        }

        public int ConstraintCount
        {
            get => _constraintCount;
            set { _constraintCount = Mathf.Max(1, value); MarkDirty(); }
        }

        public TextAnchor ChildAlignment
        {
            get => _childAlignment;
            set { _childAlignment = value; MarkDirty(); }
        }

        public bool CenterSingleRow
        {
            get => _centerSingleRow;
            set { _centerSingleRow = value; MarkDirty(); }
        }

        public CellFit Fit
        {
            get => _cellFit;
            set { _cellFit = value; SetDirty(true); }
        }

        /// <summary> Columns and rows of the last layout. </summary>
        public Vector2Int GridSize { get; private set; }

        internal override int PrimaryAxis => _startAxis == StartAxis.Horizontal ? 1 : 0;

        internal override bool IsPrimaryAscending => _startAxis == StartAxis.Horizontal
            ? _startCorner == Corner.UpperLeft || _startCorner == Corner.UpperRight
            : _startCorner == Corner.UpperLeft || _startCorner == Corner.LowerLeft;

        internal override bool UsesScaleToFit => _cellFit == CellFit.ScaleToFit;

        /// <summary> A full-width block to insert under the row of an element (see <see cref="ReflowExpandableGrid"/>). </summary>
        internal virtual bool TryGetExpansion(float pInnerWidth, out int pEntryIndex, out float pHeight)
        {
            pEntryIndex = -1;
            pHeight = 0f;
            return false;
        }

        internal override Vector2 Solve(Vector2 pFixedSize)
        {
            int count = _slotCount;
            GetGridSize(pFixedSize, count, out int columns, out int rows);
            GridSize = new Vector2Int(columns, rows);

            int usedColumns = columns;
            int usedRows = rows;
            if (_centerSingleRow && count > 0)
            {
                if (_startAxis == StartAxis.Horizontal && rows == 1)
                    usedColumns = count;
                else if (_startAxis == StartAxis.Vertical && columns == 1)
                    usedRows = count;
            }

            float stepX = _cellSize.x + _spacing.x;
            float stepY = _cellSize.y + _spacing.y;
            bool rightToLeft = _startCorner == Corner.UpperRight || _startCorner == Corner.LowerRight;
            bool bottomToTop = _startCorner == Corner.LowerLeft || _startCorner == Corner.LowerRight;

            // Width.
            float gridWidth = columns > 0 ? columns * stepX - _spacing.x : 0f;
            float contentWidth = gridWidth + _padding.horizontal;
            bool followX = FollowsContent(0);
            float followedWidth = followX ? ApplyPolicy(0, contentWidth) : contentWidth;
            float width = pFixedSize.x >= 0f ? pFixedSize.x : followedWidth;
            SetContentSize(0, followX ? followedWidth : width);
            float innerWidth = Mathf.Max(0f, width - _padding.horizontal);

            // Optional block under one row.
            _hasExpansion = false;
            int expansionRow = int.MaxValue;
            float expansionHeight = 0f;
            float extra = 0f;
            if (count > 0 && _startAxis == StartAxis.Horizontal && TryGetExpansion(innerWidth, out int expandedEntry, out expansionHeight)
                && TryGetEntrySlot(expandedEntry, out int expandedSlot))
            {
                int logicalRow = expandedSlot / columns;
                expansionRow = bottomToTop ? usedRows - 1 - logicalRow : logicalRow;
                extra = expansionHeight + _spacing.y;
                _hasExpansion = true;
            }

            // Height.
            float gridHeight = rows > 0 ? rows * stepY - _spacing.y : 0f;
            float contentHeight = gridHeight + extra + _padding.vertical;
            bool followY = FollowsContent(1);
            float followedHeight = followY ? ApplyPolicy(1, contentHeight) : contentHeight;
            float height = pFixedSize.y >= 0f ? pFixedSize.y : followedHeight;
            SetContentSize(1, followY ? followedHeight : height);
            float innerHeight = Mathf.Max(0f, height - _padding.vertical);

            // Block of cells inside the padding, placed by the alignment.
            float blockWidth = usedColumns > 0 ? usedColumns * stepX - _spacing.x : 0f;
            float blockHeight = (usedRows > 0 ? usedRows * stepY - _spacing.y : 0f) + extra;
            float originX = _padding.left + (innerWidth - blockWidth) * ReflowUtil.AlignFactor(_childAlignment, 0);
            float originY = _padding.top + (innerHeight - blockHeight) * ReflowUtil.AlignFactor(_childAlignment, 1);
            if (_hasExpansion)
                _expansionRect = new Rect(_padding.left, originY + expansionRow * stepY + _cellSize.y + _spacing.y, innerWidth, expansionHeight);

            bool stretch = _cellFit == CellFit.Stretch;
            for (int i = 0; i < count; i++)
            {
                int column = _startAxis == StartAxis.Horizontal ? i % columns : i / rows;
                int row = _startAxis == StartAxis.Horizontal ? i / columns : i % rows;
                if (rightToLeft)
                    column = usedColumns - 1 - column;
                if (bottomToTop)
                    row = usedRows - 1 - row;

                Vector2 cellPosition = new Vector2(originX + column * stepX, originY + row * stepY + (row > expansionRow ? extra : 0f));

                ref ReflowSlot slot = ref _slotArray[i];
                ReflowElement element = slot.element;
                Vector2 exact = ReflowUtil.FREE_SIZE;
                if (stretch)
                {
                    if (element == null || element.LayoutControls(0))
                        exact.x = _cellSize.x;
                    if (element == null || element.LayoutControls(1))
                        exact.y = _cellSize.y;
                }
                Vector2 desired = MeasureSlot(ref slot, exact);

                Vector2 childSize;
                if (_cellFit == CellFit.ScaleToFit)
                    childSize = _cellSize;
                else if (element != null && !stretch)
                    childSize = new Vector2(element.Clamp(0, desired.x), element.Clamp(1, desired.y));
                else
                    childSize = desired;

                slot.size = childSize;
                slot.position = cellPosition + (_cellSize - childSize) * 0.5f;
                slot.cellPosition = cellPosition;
                slot.cellSize = _cellSize;
                slot.row = row;
                slot.column = column;
            }

            return new Vector2(width, height);
        }

        private void GetGridSize(Vector2 pFixedSize, int pCount, out int pColumns, out int pRows)
        {
            if (pCount == 0)
            {
                pColumns = 0;
                pRows = 0;
                return;
            }

            switch (_constraint)
            {
                case Constraint.FixedColumnCount:
                    pColumns = Mathf.Max(1, _constraintCount);
                    pRows = CeilDivide(pCount, pColumns);
                    return;
                case Constraint.FixedRowCount:
                    pRows = Mathf.Max(1, _constraintCount);
                    pColumns = CeilDivide(pCount, pRows);
                    return;
            }

            if (_startAxis == StartAxis.Horizontal)
            {
                pColumns = Mathf.Min(pCount, FitCount(0, pFixedSize.x, _cellSize.x, _spacing.x, _padding.horizontal, pCount));
                pRows = CeilDivide(pCount, pColumns);
            }
            else
            {
                pRows = Mathf.Min(pCount, FitCount(1, pFixedSize.y, _cellSize.y, _spacing.y, _padding.vertical, pCount));
                pColumns = CeilDivide(pCount, pRows);
            }
        }

        /// <summary> Cells that fit on <paramref name="pAxis"/>: in the fixed size, else in the follow cap, else all of them. </summary>
        private int FitCount(int pAxis, float pFixed, float pCell, float pSpacing, float pPadding, int pCount)
        {
            float size = pFixed >= 0f ? pFixed : GetCap(pAxis);
            if (size <= ReflowUtil.EPSILON)
                return pCount;
            float step = pCell + pSpacing;
            if (step <= ReflowUtil.EPSILON)
                return pCount;
            return Mathf.Max(1, Mathf.FloorToInt((size - pPadding + pSpacing) / step + ReflowUtil.EPSILON));
        }

        private static int CeilDivide(int pValue, int pDivisor)
        {
            return (pValue + pDivisor - 1) / pDivisor;
        }
    }
}
