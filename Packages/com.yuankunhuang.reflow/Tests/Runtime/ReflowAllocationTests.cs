using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reflow.Tests
{
    /// <summary> Steady-state layout and scrolling allocate nothing. </summary>
    public class ReflowAllocationTests
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

        [UnityTest]
        public IEnumerator RelayoutAndScroll_AllocateNothing()
        {
            GameObject template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 50f));
            ReflowVertical layout = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 400f), true, out _);
            layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            ReflowTestUtil.CreateRect("Header", layout.transform, new Vector2(300f, 80f));
            ReflowTestData[] dataArray = new ReflowTestData[500];
            for (int i = 0; i < dataArray.Length; i++)
            {
                dataArray[i] = new ReflowTestData(i);
                layout.AddElement(template, dataArray[i]);
            }
            yield return null;

            // Warm up every path once (pool, buffers, first spawns).
            Run(layout);
            Run(layout);

            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(layout);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "bytes allocated by relayout + scrolling");
        }

        private static void Run(ReflowVertical pLayout)
        {
            for (int i = 0; i < 20; i++)
            {
                pLayout.RefreshAllLayout();
                pLayout.EnsureLayout();
            }
            for (int i = 0; i <= 200; i++)
            {
                pLayout.RectTransform.anchoredPosition = new Vector2(0f, i * 97f % 20000f);
                pLayout.RefreshVisibility();
            }
            pLayout.RectTransform.anchoredPosition = Vector2.zero;
            pLayout.RefreshVisibility();
        }
    }
}
