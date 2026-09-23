using NUnit.Framework;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowSelectionTests
    {
        private RectTransform _root;
        private GameObject _template;
        private ReflowHorizontal _layout;
        private ReflowSelection _selection;
        private int _eventCount;
        private int _lastPrevious;
        private int _lastCurrent;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
            _template = ReflowTestUtil.CreateItemTemplate(new Vector2(80f, 40f));
            _layout = ReflowTestUtil.CreateLayout<ReflowHorizontal>(_root, new Vector2(600f, 60f));
            _selection = _layout.gameObject.AddComponent<ReflowSelection>();
            _selection.SelectionChanged += (pPrevious, pCurrent) =>
            {
                _eventCount++;
                _lastPrevious = pPrevious;
                _lastCurrent = pCurrent;
            };
            for (int i = 0; i < 4; i++)
                _layout.AddElement(_template, new ReflowTestData(i));
            _layout.EnsureLayout();
            _eventCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private ReflowTestItem Item(int pIndex)
        {
            return (ReflowTestItem)_layout.GetActiveElement(pIndex);
        }

        [Test]
        public void Select_UpdatesItemsSilently()
        {
            Assert.IsTrue(_selection.Select(1, false));
            Assert.IsTrue(Item(1).isSelected);
            Assert.IsFalse(Item(0).isSelected);
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void Click_SelectsAndNotifies()
        {
            Item(2).Click();
            Assert.AreEqual(2, _selection.SelectedIndex);
            Assert.AreEqual(1, _eventCount);
            Assert.AreEqual(-1, _lastPrevious);
            Assert.AreEqual(2, _lastCurrent);

            Item(2).Click();
            Assert.AreEqual(1, _eventCount, "clicking the selected item again does nothing");
        }

        [Test]
        public void ToggleOff_ClearsOnSecondClick()
        {
            _selection.AllowToggleOff = true;
            Item(2).Click();
            Item(2).Click();
            Assert.AreEqual(-1, _selection.SelectedIndex);
            Assert.IsFalse(Item(2).isSelected);
        }

        [Test]
        public void NotSelectable_KeepsCurrentSelection()
        {
            _selection.Select(0, false);
            Item(3).selectable = false;
            Item(3).Click();
            Assert.AreEqual(0, _selection.SelectedIndex);
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void ClickFilter_CanVeto()
        {
            _selection.ClickFilter = pIndex => pIndex != 1;
            Item(1).Click();
            Assert.AreEqual(-1, _selection.SelectedIndex);
            Item(3).Click();
            Assert.AreEqual(3, _selection.SelectedIndex);
        }

        [Test]
        public void Insert_ShiftsSelection()
        {
            _selection.Select(1, false);
            _layout.InsertElement(0, _template, new ReflowTestData(9));
            Assert.AreEqual(2, _selection.SelectedIndex);
            Assert.IsTrue(Item(2).isSelected);
            Assert.IsFalse(Item(1).isSelected);
            Assert.AreEqual(0, _eventCount);
        }

        [Test]
        public void RemoveSelected_ClearsSelection()
        {
            _selection.Select(1, false);
            _layout.RemoveElement(1);
            Assert.AreEqual(-1, _selection.SelectedIndex);
        }

        [Test]
        public void Reorder_FollowsTheElement()
        {
            _selection.Select(0, false);
            _layout.ReorderElements(new[] { 3, 2, 1, 0 }, false);
            Assert.AreEqual(3, _selection.SelectedIndex);
        }

        [Test]
        public void Clear_ResetsToInitialIndex()
        {
            _selection.InitialIndex = 0;
            _selection.Select(2, false);
            _layout.Clear();
            Assert.AreEqual(0, _selection.SelectedIndex);
            _layout.AddElement(_template, new ReflowTestData(0));
            Assert.IsTrue(Item(0).isSelected, "state is applied on spawn");
        }

        [Test]
        public void SelectionKey_SelectsCopiesTogether()
        {
            _selection.SelectionKey = pIndex => pIndex % 2;
            _selection.Select(0, false);
            Assert.IsTrue(Item(2).isSelected);
            Assert.IsFalse(Item(1).isSelected);
        }
    }
}
