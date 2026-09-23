using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reflow.Tests
{
    public class ReflowManagedElementTests
    {
        private RectTransform _root;
        private GameObject _template;
        private ReflowVertical _layout;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
            _template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 40f));
            _layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(300f, 1000f));
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private void AddItems(int pCount, int pFirstId = 0)
        {
            for (int i = 0; i < pCount; i++)
                _layout.AddElement(_template, new ReflowTestData(pFirstId + i));
        }

        private ReflowTestItem Item(int pIndex)
        {
            return (ReflowTestItem)_layout.GetActiveElement(pIndex);
        }

        [Test]
        public void Add_SpawnsBindsAndStacks()
        {
            AddItems(3);
            Assert.AreEqual(3, _layout.ElementCount);
            Assert.AreEqual(1, Item(1).Id, "GetActiveElement runs the pending layout first");
            Assert.AreEqual(3, _layout.SpawnedCount);
            ReflowTestUtil.AssertRect(Item(2).RectTransform, 0f, 80f, 100f, 40f);
        }

        [Test]
        public void Insert_ShiftsSpawnedItemsAndRaisesChange()
        {
            AddItems(3);
            _layout.EnsureLayout();
            ReflowTestItem second = Item(1);
            ReflowElementChange lastChange = default;
            _layout.ElementsChanged += pChange => lastChange = pChange;

            _layout.InsertElement(1, _template, new ReflowTestData(9));

            Assert.AreEqual(ReflowElementChangeKind.Inserted, lastChange.kind);
            Assert.AreEqual(2, lastChange.MapIndex(1));
            Assert.AreSame(second, Item(2), "the spawned item moved with its element");
            Assert.AreEqual(9, Item(1).Id);
            ReflowTestUtil.AssertRect(second.RectTransform, 0f, 80f, 100f, 40f);
        }

        [Test]
        public void Remove_DespawnsAndShifts()
        {
            AddItems(3);
            ReflowTestItem first = Item(0);
            _layout.RemoveElement(0);

            Assert.AreEqual(1, first.hideCount);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(1, Item(0).Id);
            ReflowTestUtil.AssertRect(Item(1).RectTransform, 0f, 40f, 100f, 40f);
        }

        [Test]
        public void Hidden_TakesNoSpaceAndIsNotSpawned()
        {
            AddItems(3);
            _layout.SetElementHidden(1, true);

            Assert.IsNull(_layout.GetActiveElement(1));
            ReflowTestUtil.AssertRect(Item(2).RectTransform, 0f, 40f, 100f, 40f);

            _layout.SetElementHidden(1, false);
            ReflowTestUtil.AssertRect(Item(2).RectTransform, 0f, 80f, 100f, 40f);
        }

        [Test]
        public void Reorder_MovesItemsWithoutRebinding()
        {
            AddItems(3);
            ReflowTestItem last = Item(2);
            int showCount = last.showCount;

            _layout.ReorderElements(new List<int> { 2, 0, 1 });

            Assert.AreSame(last, Item(0));
            Assert.AreEqual(showCount, last.showCount);
            ReflowTestUtil.AssertRect(last.RectTransform, 0f, 0f, 100f, 40f);
            Assert.Throws<System.ArgumentException>(() => _layout.ReorderElements(new List<int> { 0, 0, 1 }));
        }

        [Test]
        public void ClearAndAddAgain_ReusesPooledItems()
        {
            AddItems(3);
            _layout.EnsureLayout();
            ReflowStats.Reset();

            _layout.Clear();
            Assert.AreEqual(0, _layout.SpawnedCount);
            AddItems(3, 10);
            Assert.AreEqual(10, Item(0).Id);

            Assert.AreEqual(0, ReflowStats.CreateCount);
            Assert.AreEqual(3, _layout.PooledItemCount);
        }

        [Test]
        public void ClearPool_DestroysFreeItems()
        {
            AddItems(3);
            _layout.EnsureLayout();
            _layout.RemoveElement(0);
            _layout.ClearPool();

            Assert.AreEqual(2, _layout.PooledItemCount);
            Assert.AreEqual(2, _layout.transform.childCount);
        }

        [Test]
        public void UpdateElementData_ShowsAgain()
        {
            AddItems(2);
            ReflowTestItem item = Item(0);
            int showCount = item.showCount;

            _layout.UpdateElementData(0, new ReflowTestData(42));

            Assert.AreEqual(showCount + 1, item.showCount);
            Assert.AreEqual(42, item.Id);
            Assert.IsTrue(_layout.TryGetElementData(0, out ReflowTestData data) && data.id == 42);
        }

        [Test]
        public void ItemThatSizesItself_PushesNextItemDown()
        {
            _layout.AddElement(_template, new ReflowTestData(0, new Vector2(100f, 70f)));
            _layout.AddElement(_template, new ReflowTestData(1));

            ReflowTestUtil.AssertRect(Item(1).RectTransform, 0f, 70f, 100f, 40f);
        }

        [Test]
        public void ReusedItem_DoesNotKeepPreviousSize()
        {
            _layout.AddElement(_template, new ReflowTestData(0, new Vector2(100f, 70f)));
            _layout.EnsureLayout();
            _layout.Clear();
            _layout.AddElement(_template, new ReflowTestData(1));

            ReflowTestUtil.AssertRect(Item(0).RectTransform, 0f, 0f, 100f, 40f);
        }

        [Test]
        public void FixedMode_UsesSizeHint()
        {
            _layout.MainAxisMode = ReflowChildSizeMode.Fixed;
            _layout.AddElement(_template, new ReflowTestData(0), new Vector2(0f, 55f));
            _layout.AddElement(_template, new ReflowTestData(1));

            ReflowTestUtil.AssertRect(Item(0).RectTransform, 0f, 0f, 100f, 55f);
            ReflowTestUtil.AssertRect(Item(1).RectTransform, 0f, 55f, 100f, 100f);
        }

        [Test]
        public void SceneChildrenComeFirst()
        {
            RectTransform header = ReflowTestUtil.CreateRect("Header", _layout.transform, new Vector2(300f, 30f));
            AddItems(1);
            _layout.EnsureLayout();

            ReflowTestUtil.AssertRect(header, 0f, 0f, 300f, 30f);
            ReflowTestUtil.AssertRect(Item(0).RectTransform, 0f, 30f, 100f, 40f);
        }

        [Test]
        public void ModifyingFromSpawnCallback_IsRejected()
        {
            _layout.ElementSpawned += (pIndex, pItem) => _layout.AddElement(_template, new ReflowTestData(99));
            AddItems(1);

            LogAssert.Expect(LogType.Exception, new Regex("cannot be added"));
            _layout.EnsureLayout();
            Assert.AreEqual(1, _layout.ElementCount);
        }
    }
}
