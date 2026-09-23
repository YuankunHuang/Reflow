using NUnit.Framework;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowGridTests
    {
        private RectTransform _root;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private ReflowGrid CreateGrid(Vector2 pSize, int pChildCount, out RectTransform[] pChildArray)
        {
            ReflowGrid grid = ReflowTestUtil.CreateLayout<ReflowGrid>(_root, pSize);
            grid.CellSize = new Vector2(100f, 50f);
            grid.Spacing = new Vector2(10f, 5f);
            grid.Padding = new RectOffset(4, 6, 8, 2);
            pChildArray = new RectTransform[pChildCount];
            for (int i = 0; i < pChildCount; i++)
                pChildArray[i] = ReflowTestUtil.CreateRect("C" + i, grid.transform, new Vector2(30f, 30f));
            return grid;
        }

        [Test]
        public void FixedColumns_FillRowByRow()
        {
            ReflowGrid grid = CreateGrid(new Vector2(400f, 400f), 5, out RectTransform[] children);
            grid.GridConstraint = ReflowGrid.Constraint.FixedColumnCount;
            grid.ConstraintCount = 2;
            grid.EnsureLayout();

            ReflowTestUtil.AssertRect(children[0], 4f, 8f, 100f, 50f);
            ReflowTestUtil.AssertRect(children[1], 114f, 8f, 100f, 50f);
            ReflowTestUtil.AssertRect(children[2], 4f, 63f, 100f, 50f);
            ReflowTestUtil.AssertRect(children[4], 4f, 118f, 100f, 50f);
            Assert.AreEqual(new Vector2Int(2, 3), grid.GridSize);
        }

        [Test]
        public void Flexible_ColumnsFitTheWidth()
        {
            // Inner width 350 - 10 = 340: floor((340 + 10) / 110) = 3 columns.
            ReflowGrid grid = CreateGrid(new Vector2(350f, 400f), 7, out RectTransform[] children);
            grid.EnsureLayout();

            Assert.AreEqual(new Vector2Int(3, 3), grid.GridSize);
            ReflowTestUtil.AssertRect(children[3], 4f, 63f, 100f, 50f);
        }

        [Test]
        public void StartCorner_LowerRight()
        {
            ReflowGrid grid = CreateGrid(new Vector2(400f, 400f), 4, out RectTransform[] children);
            grid.GridConstraint = ReflowGrid.Constraint.FixedColumnCount;
            grid.ConstraintCount = 2;
            grid.StartCorner = ReflowGrid.Corner.LowerRight;
            grid.EnsureLayout();

            // Block of 2x2 cells at the top-left (UpperLeft alignment); the first child sits in its bottom-right cell.
            ReflowTestUtil.AssertRect(children[0], 114f, 63f, 100f, 50f);
            ReflowTestUtil.AssertRect(children[3], 4f, 8f, 100f, 50f);
        }

        [Test]
        public void StartAxis_VerticalFillsColumns()
        {
            ReflowGrid grid = CreateGrid(new Vector2(400f, 400f), 5, out RectTransform[] children);
            grid.Axis = ReflowGrid.StartAxis.Vertical;
            grid.GridConstraint = ReflowGrid.Constraint.FixedRowCount;
            grid.ConstraintCount = 2;
            grid.EnsureLayout();

            ReflowTestUtil.AssertRect(children[1], 4f, 63f, 100f, 50f);
            ReflowTestUtil.AssertRect(children[2], 114f, 8f, 100f, 50f);
        }

        [Test]
        public void CenterSingleRow_CentersFewItems()
        {
            ReflowGrid grid = CreateGrid(new Vector2(500f, 200f), 2, out RectTransform[] children);
            grid.GridConstraint = ReflowGrid.Constraint.FixedColumnCount;
            grid.ConstraintCount = 4;
            grid.CenterSingleRow = true;
            grid.ChildAlignment = TextAnchor.UpperCenter;
            grid.EnsureLayout();

            // Inner width 490; block of 2 cells = 210; left = 4 + (490 - 210) / 2.
            ReflowTestUtil.AssertRect(children[0], 144f, 8f, 100f, 50f);
        }

        [Test]
        public void FollowHeight_FitsRows()
        {
            ReflowGrid grid = CreateGrid(new Vector2(400f, 10f), 5, out _);
            grid.GridConstraint = ReflowGrid.Constraint.FixedColumnCount;
            grid.ConstraintCount = 2;
            grid.FollowHeight = true;
            grid.EnsureLayout();

            ReflowTestUtil.AssertSize(grid.RectTransform, 400f, 8f + 3f * 50f + 2f * 5f + 2f);
        }

        [Test]
        public void CellFitNatural_CentersChildInCell()
        {
            ReflowGrid grid = CreateGrid(new Vector2(400f, 400f), 1, out RectTransform[] children);
            grid.Fit = ReflowGrid.CellFit.Natural;
            grid.EnsureLayout();

            ReflowTestUtil.AssertRect(children[0], 4f + 35f, 8f + 10f, 30f, 30f);
        }
    }
}
