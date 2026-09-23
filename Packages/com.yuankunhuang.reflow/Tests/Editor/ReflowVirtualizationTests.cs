using NUnit.Framework;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowVirtualizationTests
    {
        private RectTransform _root;
        private GameObject _template;
        private ReflowVertical _layout;
        private ReflowScrollRect _scrollRect;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
            _template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 50f));
            _layout = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 400f), true, out _scrollRect);
            _layout.CrossAxisMode = ReflowChildSizeMode.Expand;
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private void AddItems(int pCount)
        {
            for (int i = 0; i < pCount; i++)
                _layout.AddElement(_template, new ReflowTestData(i));
        }

        private void ScrollTo(float pOffset)
        {
            _layout.RectTransform.anchoredPosition = new Vector2(0f, pOffset);
            _layout.RefreshVisibility();
        }

        [Test]
        public void SpawnsOnlyVisibleElements()
        {
            AddItems(1000);
            _layout.EnsureLayout();

            Assert.IsTrue(_layout.IsVirtualized);
            Assert.AreEqual(8, _layout.SpawnedCount);
            ReflowTestUtil.AssertSize(_layout.RectTransform, 300f, 50000f);
            Assert.IsNotNull(_layout.GetActiveElement(7));
            Assert.IsNull(_layout.GetActiveElement(8));
        }

        [Test]
        public void Scrolling_RecyclesItems()
        {
            AddItems(1000);
            _layout.EnsureLayout();
            ReflowStats.Reset();

            for (int step = 1; step <= 50; step++)
                ScrollTo(step * 37f);
            ScrollTo(5000f);

            Assert.IsNotNull(_layout.GetActiveElement(100));
            Assert.IsNull(_layout.GetActiveElement(0));
            Assert.AreEqual(100, _layout.GetFirstVisibleIndex());
            Assert.LessOrEqual(_layout.PooledItemCount, 10, "items are reused, not created per element");
            Assert.AreEqual(0, ReflowStats.SolveCount, "scrolling fixed-size items never relayouts");
            ReflowTestUtil.AssertRect(_layout.GetActiveElement(100).RectTransform, 0f, 5000f, 300f, 50f);
        }

        [Test]
        public void VariableSizes_LandWithoutGaps()
        {
            for (int i = 0; i < 200; i++)
                _layout.AddElement(_template, new ReflowTestData(i, new Vector2(100f, i % 2 == 0 ? 50f : 90f)), new Vector2(0f, 50f));
            _layout.EnsureLayout();
            ScrollTo(3000f);
            _layout.EnsureLayout();

            float expectedTop = -1f;
            for (int i = 0; i < 200; i++)
            {
                IReflowItem item = _layout.GetActiveElement(i);
                if (item == null)
                    continue;
                Rect rect = ReflowTestUtil.GetRectInParent(item.RectTransform);
                if (expectedTop >= 0f)
                    Assert.AreEqual(expectedTop, rect.y, ReflowTestUtil.TOLERANCE, $"item {i} starts where the previous ended");
                Assert.AreEqual(i % 2 == 0 ? 50f : 90f, rect.height, ReflowTestUtil.TOLERANCE);
                expectedTop = rect.yMax;
            }
        }

        [Test]
        public void ScrollToElement_ShowsItRightAway()
        {
            AddItems(1000);
            Assert.IsTrue(_scrollRect.ScrollToElement(_layout, 500));

            Assert.IsNotNull(_layout.GetActiveElement(500));
            Assert.AreEqual(25000f, _layout.RectTransform.anchoredPosition.y, ReflowTestUtil.TOLERANCE);
            Assert.AreEqual(25000f, _layout.GetElementScrollOffset(500), ReflowTestUtil.TOLERANCE);

            _scrollRect.ScrollToElement(_layout, 999, ReflowScrollAlignment.End);
            Assert.AreEqual(50000f - 400f, _layout.RectTransform.anchoredPosition.y, ReflowTestUtil.TOLERANCE);
            Assert.IsNotNull(_layout.GetActiveElement(999));

            _scrollRect.ScrollToElement(_layout, 999, ReflowScrollAlignment.Start);
            Assert.AreEqual(50000f - 400f, _layout.RectTransform.anchoredPosition.y, ReflowTestUtil.TOLERANCE, "clamped to the scroll range");
        }

        [Test]
        public void Off_SpawnsEverything()
        {
            _layout.Virtualization = ReflowVirtualization.Off;
            AddItems(30);
            _layout.EnsureLayout();
            Assert.AreEqual(30, _layout.SpawnedCount);
        }

        [Test]
        public void Reverse_ShowsLastElementsFirst()
        {
            _layout.ReverseArrangement = true;
            AddItems(100);
            _layout.EnsureLayout();

            Assert.AreEqual(8, _layout.SpawnedCount);
            Assert.IsNotNull(_layout.GetActiveElement(99));
            Assert.IsNotNull(_layout.GetActiveElement(92));
            Assert.IsNull(_layout.GetActiveElement(91));
            Assert.AreEqual(99, _layout.GetFirstVisibleIndex());
        }

        [Test]
        public void Hidden_AreSkipped()
        {
            AddItems(20);
            for (int i = 0; i < 10; i += 2)
                _layout.SetElementHidden(i, true);
            _layout.EnsureLayout();

            Assert.IsNull(_layout.GetActiveElement(0));
            Assert.IsNotNull(_layout.GetActiveElement(1));
            ReflowTestUtil.AssertRect(_layout.GetActiveElement(3).RectTransform, 0f, 50f, 300f, 50f);
        }

        [Test]
        public void Grid_SpawnsVisibleRows()
        {
            RectTransform content = _layout.RectTransform;
            Object.DestroyImmediate(_layout);
            ReflowGrid grid = content.gameObject.AddComponent<ReflowGrid>();
            grid.FollowHeight = true;
            grid.CellSize = new Vector2(100f, 100f);
            for (int i = 0; i < 300; i++)
                grid.AddElement(_template, new ReflowTestData(i));
            grid.EnsureLayout();

            // 3 columns, 4 visible rows.
            Assert.AreEqual(12, grid.SpawnedCount);
            content.anchoredPosition = new Vector2(0f, 1000f);
            grid.RefreshVisibility();
            Assert.IsNotNull(grid.GetActiveElement(30));
            Assert.IsNull(grid.GetActiveElement(29));
            Assert.AreEqual(12, grid.SpawnedCount);
        }

        [Test]
        public void ScrollingUpIntoUnmeasuredRows_DoesNotJump()
        {
            // Estimated 50, real 90: every row that comes into view for the first time grows.
            for (int i = 0; i < 300; i++)
                _layout.AddElement(_template, new ReflowTestData(i, new Vector2(100f, 90f)), new Vector2(0f, 50f));
            _layout.EnsureLayout();
            ScrollTo(8000f);
            _layout.EnsureLayout();

            int first = _layout.GetFirstVisibleIndex();
            RectTransform firstItem = _layout.GetActiveElement(first).RectTransform;
            float firstOnScreen = ReflowTestUtil.GetRectInParent(firstItem).y - _layout.RectTransform.anchoredPosition.y;

            // Scroll up a little: rows above come into view, get measured, and push everything below down...
            ScrollTo(_layout.RectTransform.anchoredPosition.y - 100f);
            _layout.EnsureLayout();

            // ...but the content is moved with them, so the row that was on screen moved exactly as far as we scrolled.
            float nowOnScreen = ReflowTestUtil.GetRectInParent(firstItem).y - _layout.RectTransform.anchoredPosition.y;
            Assert.AreEqual(firstOnScreen + 100f, nowOnScreen, ReflowTestUtil.TOLERANCE);
        }

        [Test]
        public void ScrollAnchor_KeepsElementInPlace()
        {
            AddItems(1000);
            _layout.EnsureLayout();
            ScrollTo(5000f);
            ReflowScrollAnchor anchor = _layout.CaptureScrollAnchor();
            Assert.AreEqual(100, anchor.index);

            // Something above the viewport grows.
            _layout.SetElementSizeHint(50, new Vector2(0f, 150f));
            _layout.EnsureLayout();
            Assert.AreNotEqual(100, _layout.GetFirstVisibleIndex());

            Assert.IsTrue(_layout.RestoreScrollAnchor(anchor));
            Assert.AreEqual(100, _layout.GetFirstVisibleIndex());
            Assert.AreEqual(5100f, _layout.RectTransform.anchoredPosition.y, ReflowTestUtil.TOLERANCE);
        }
    }
}
