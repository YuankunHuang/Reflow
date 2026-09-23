using NUnit.Framework;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowExpandableGridTests
    {
        private RectTransform _root;
        private GameObject _template;
        private ReflowExpandableGrid _grid;
        private RectTransform _detail;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
            _template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 100f));
            GameObject template = _template;
            _grid = ReflowTestUtil.CreateLayout<ReflowExpandableGrid>(_root, new Vector2(320f, 10f));
            _grid.CellSize = new Vector2(100f, 100f);
            _grid.Spacing = new Vector2(10f, 10f);
            _grid.GridConstraint = ReflowGrid.Constraint.FixedColumnCount;
            _grid.ConstraintCount = 3;
            _grid.FollowHeight = true;
            for (int i = 0; i < 7; i++)
                _grid.AddElement(template, new ReflowTestData(i));
            _detail = ReflowTestUtil.CreateRect("Detail", _root, new Vector2(50f, 80f));
            _grid.EnsureLayout();
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private RectTransform Item(int pIndex)
        {
            return _grid.GetActiveElement(pIndex).RectTransform;
        }

        [Test]
        public void Expand_InsertsBlockUnderTheRow()
        {
            _grid.SetExpandedContent(1, _detail);
            _grid.EnsureLayout();

            Assert.AreEqual(_grid.transform, _detail.parent);
            ReflowTestUtil.AssertRect(_detail, 0f, 110f, 320f, 80f);
            ReflowTestUtil.AssertRect(Item(2), 220f, 0f, 100f, 100f, "same row stays");
            ReflowTestUtil.AssertRect(Item(3), 0f, 200f, 100f, 100f, "next row moves down by block + spacing");
            ReflowTestUtil.AssertSize(_grid.RectTransform, 320f, 3f * 100f + 2f * 10f + 80f + 10f);
        }

        [Test]
        public void Clear_RestoresRowsAndHidesContent()
        {
            _grid.SetExpandedContent(1, _detail);
            _grid.EnsureLayout();
            _grid.ClearExpandedContent();
            _grid.EnsureLayout();

            Assert.IsFalse(_detail.gameObject.activeSelf);
            ReflowTestUtil.AssertRect(Item(3), 0f, 110f, 100f, 100f);
            ReflowTestUtil.AssertSize(_grid.RectTransform, 320f, 320f);
        }

        [Test]
        public void MovingToAnotherRow_MovesTheBlock()
        {
            _grid.SetExpandedContent(1, _detail);
            _grid.SetExpandedContent(4, _detail);
            _grid.EnsureLayout();

            ReflowTestUtil.AssertRect(_detail, 0f, 220f, 320f, 80f);
            ReflowTestUtil.AssertRect(Item(3), 0f, 110f, 100f, 100f);
            ReflowTestUtil.AssertRect(Item(6), 0f, 310f, 100f, 100f);
        }

        [Test]
        public void ContentGrowing_RelayoutsTheGrid()
        {
            _grid.SetExpandedContent(1, _detail);
            _grid.EnsureLayout();
            ReflowVertical detailLayout = _detail.gameObject.AddComponent<ReflowVertical>();
            detailLayout.FollowHeight = true;
            RectTransform line = ReflowTestUtil.CreateRect("Line", _detail, new Vector2(100f, 120f));
            _grid.EnsureLayout();

            ReflowTestUtil.AssertRect(_detail, 0f, 110f, 320f, 120f);
            ReflowTestUtil.AssertRect(Item(3), 0f, 240f, 100f, 100f);

            line.sizeDelta = new Vector2(100f, 60f);
            detailLayout.RefreshAllLayout();
            _grid.EnsureLayout();
            ReflowTestUtil.AssertRect(Item(3), 0f, 180f, 100f, 100f);
        }

        [Test]
        public void RemovingExpandedElement_ClosesTheBlock()
        {
            _grid.SetExpandedContent(1, _detail);
            _grid.RemoveElement(1);
            Assert.IsFalse(_grid.IsExpanded);
            Assert.IsFalse(_detail.gameObject.activeSelf);
        }

        [Test]
        public void InsertBeforeExpandedElement_KeepsIt()
        {
            _grid.SetExpandedContent(4, _detail);
            _grid.InsertElement(0, _template, new ReflowTestData(9));
            Assert.AreEqual(5, _grid.ExpandedIndex);
            _grid.EnsureLayout();
            ReflowTestUtil.AssertRect(_detail, 0f, 220f, 320f, 80f, "element 5 is still in row 1");
        }
    }
}
