using System.Collections;
using Reflow.Samples;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Reflow.DevTests
{
    /// <summary> Runs every package sample (imported under Assets/Samples) and pokes it. Any error log fails the test. </summary>
    public class ReflowSampleSmokeTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("Sample Host");
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_host);
            foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas)
                    Object.Destroy(canvas.gameObject);
            }
            foreach (EventSystem eventSystem in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                Object.Destroy(eventSystem.gameObject);
        }

        private static IEnumerator Frames(int pCount)
        {
            for (int i = 0; i < pCount; i++)
                yield return null;
        }

        private static void Click(string pLabel)
        {
            foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                TMP_Text text = button.GetComponentInChildren<TMP_Text>();
                if (text != null && text.text == pLabel)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
            Assert.Fail($"No button labelled '{pLabel}'");
        }

        [UnityTest]
        public IEnumerator Basics()
        {
            _host.AddComponent<ReflowBasicsSample>();
            yield return Frames(3);
            RectTransform card = GameObject.Find("Card").GetComponent<RectTransform>();
            float height = card.rect.height;
            Assert.Greater(height, 100f);

            Click("Add text");
            yield return Frames(1);
            Assert.Greater(card.rect.height, height, "the card grew with its text in the same frame");
            Click("Add cell");
            yield return Frames(1);
        }

        [UnityTest]
        public IEnumerator VirtualizedList()
        {
            _host.AddComponent<ReflowVirtualizedListSample>();
            yield return Frames(3);
            ReflowVertical list = null;
            foreach (ReflowVertical layout in Object.FindObjectsByType<ReflowVertical>(FindObjectsSortMode.None))
            {
                if (layout.ElementCount == 10000)
                    list = layout;
            }
            Assert.IsNotNull(list);
            Assert.Less(list.SpawnedCount, 40);

            float expected = list.GetElementScrollOffset(5000, ReflowScrollAlignment.Center);
            Click("Jump to 5000");
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(list.GetActiveElement(5000),
                $"first visible {list.GetFirstVisibleIndex()}, content y {list.RectTransform.anchoredPosition.y:F0}, offset before {expected:F0} after {list.GetElementScrollOffset(5000, ReflowScrollAlignment.Center):F0}, height {list.RectTransform.rect.height:F0}");

            Click("Insert on top");
            Click("Remove first");
            yield return Frames(2);
            Assert.AreEqual(10000, list.ElementCount);
        }

        [UnityTest]
        public IEnumerator ExpandableGrid()
        {
            _host.AddComponent<ReflowExpandableGridSample>();
            yield return Frames(3);
            ReflowExpandableGrid grid = Object.FindFirstObjectByType<ReflowExpandableGrid>();
            ReflowSelection selection = grid.GetComponent<ReflowSelection>();

            selection.Select(5);
            yield return Frames(2);
            Assert.IsTrue(grid.IsExpanded);
            Assert.Greater(grid.ExpandedContent.rect.height, 50f);

            selection.Select(-1);
            yield return Frames(1);
            Assert.IsFalse(grid.IsExpanded);
        }

        [UnityTest]
        public IEnumerator Selection()
        {
            _host.AddComponent<ReflowSelectionSample>();
            yield return Frames(3);
            ReflowSelection selection = Object.FindFirstObjectByType<ReflowSelection>();
            Assert.AreEqual(0, selection.SelectedIndex);

            ((ReflowSampleTabItem)selection.Layout.GetActiveElement(2)).button.onClick.Invoke();
            Assert.AreEqual(0, selection.SelectedIndex, "Locked tab is not selectable");
            ((ReflowSampleTabItem)selection.Layout.GetActiveElement(4)).button.onClick.Invoke();
            Assert.AreEqual(0, selection.SelectedIndex, "Settings waits for a confirm");
            Click("Confirm pending tab");
            Assert.AreEqual(4, selection.SelectedIndex);
            yield return Frames(1);
        }

        [UnityTest]
        public IEnumerator Benchmark()
        {
            _host.AddComponent<ReflowBenchmarkSample>();
            yield return Frames(10);
            TMP_Text output = null;
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (text.text.Contains("Reflow ms"))
                    output = text;
            }
            Assert.IsNotNull(output, "benchmark printed its results");
            Debug.Log(output.text);
        }
    }
}
