using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Reflow.Tests
{
    /// <summary>
    /// Same UI built with Reflow layouts and with uGUI LayoutGroup + ContentSizeFitter, layout cost measured with a
    /// stopwatch (graphics rebuild excluded). Explicit: run it on purpose, e.g.
    /// -runTests -testPlatform PlayMode -testFilter Reflow.Tests.ReflowBenchmarkTests
    /// Writes Logs/ReflowBenchmark.md (Temp is wiped when the editor quits).
    /// </summary>
    [Explicit]
    [Category("Benchmark")]
    public class ReflowBenchmarkTests
    {
        private const int ITERATIONS = 40;
        private const int ROW_COUNT = 200;
        private const int TEXT_ROW_COUNT = 50;
        private const int SECTION_COUNT = 10;
        private const int ROWS_PER_SECTION = 10;
        private const int BIG_LIST_COUNT = 1000;

        private RectTransform _root;
        private readonly StringBuilder _report = new StringBuilder();

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
        public IEnumerator Benchmark()
        {
            _report.AppendLine($"Unity {Application.unityVersion}, {(Application.isEditor ? "Editor (Mono)" : "Player")}, {SystemInfo.processorType}");
            _report.AppendLine();
            _report.AppendLine("| Scenario | Reflow ms | uGUI ms | uGUI / Reflow |");
            _report.AppendLine("|---|---:|---:|---:|");

            RelayoutFixedRows();
            yield return null;
            OneLabelInTextList();
            yield return null;
            OneLabelInNestedSections();
            yield return null;
            BuildBigList();
            yield return null;
            ScrollBigList();
            yield return null;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/ReflowBenchmark.md"));
            File.WriteAllText(path, _report.ToString());
            Debug.Log("[Reflow] Benchmark\n" + _report);
        }

        private void AddRow(string pName, double pReflow, double pUgui)
        {
            string ratio = pUgui > 0 && pReflow > 0 ? (pUgui / pReflow).ToString("F1") + "x" : "-";
            string ugui = pUgui >= 0 ? pUgui.ToString("F3") : "-";
            _report.AppendLine($"| {pName} | {pReflow:F3} | {ugui} | {ratio} |");
        }

        /// <summary> Average time of <paramref name="pAction"/>; <paramref name="pSetup"/> runs untimed before each call. </summary>
        private static double Measure(Action<int> pSetup, Action pAction, int pIterations = ITERATIONS)
        {
            pSetup?.Invoke(-1);
            pAction();
            Stopwatch stopwatch = new Stopwatch();
            double total = 0;
            for (int i = 0; i < pIterations; i++)
            {
                pSetup?.Invoke(i);
                stopwatch.Restart();
                pAction();
                stopwatch.Stop();
                total += stopwatch.Elapsed.TotalMilliseconds;
            }
            return total / pIterations;
        }

        #region Scenarios

        /// <summary> 200 fixed-size rows all change height; the list relayouts. </summary>
        private void RelayoutFixedRows()
        {
            ReflowVertical reflow = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(300f, 10f), "Reflow");
            reflow.FollowHeight = true;
            RectTransform[] reflowRowArray = CreatePlainRows(reflow.transform, ROW_COUNT);

            RectTransform ugui = CreateUguiList(_root, false);
            RectTransform[] uguiRowArray = CreatePlainRows(ugui, ROW_COUNT);

            double reflowMs = Measure(pIteration => ResizeRows(reflowRowArray, pIteration), () =>
            {
                reflow.RefreshAllLayout();
                reflow.EnsureLayout();
            });
            double uguiMs = Measure(pIteration => ResizeRows(uguiRowArray, pIteration), () => LayoutRebuilder.ForceRebuildLayoutImmediate(ugui));
            Assert.AreEqual(reflow.RectTransform.rect.height, ugui.rect.height, 0.5f, "both lists end up the same height");
            AddRow($"Relayout {ROW_COUNT} rows after resizing them", reflowMs, uguiMs);
        }

        /// <summary> 50 rows of icon + label; one label changes. </summary>
        private void OneLabelInTextList()
        {
            ReflowVertical reflow = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 10f), "Reflow");
            reflow.FollowHeight = true;
            TMP_Text[] reflowLabelArray = new TMP_Text[TEXT_ROW_COUNT];
            for (int i = 0; i < TEXT_ROW_COUNT; i++)
                reflowLabelArray[i] = CreateReflowTextRow(reflow.transform, i);
            reflow.EnsureLayout();

            RectTransform ugui = CreateUguiList(_root, true);
            TMP_Text[] uguiLabelArray = new TMP_Text[TEXT_ROW_COUNT];
            for (int i = 0; i < TEXT_ROW_COUNT; i++)
                uguiLabelArray[i] = CreateUguiTextRow(ugui, i);
            LayoutRebuilder.ForceRebuildLayoutImmediate(ugui);

            double reflowMs = Measure(pIteration => reflowLabelArray[TEXT_ROW_COUNT / 2].text = Label(pIteration), () => reflow.EnsureLayout());
            double uguiMs = Measure(pIteration => uguiLabelArray[TEXT_ROW_COUNT / 2].text = Label(pIteration), () => LayoutRebuilder.ForceRebuildLayoutImmediate(ugui));
            AddRow($"One label changed in a {TEXT_ROW_COUNT}-row text list", reflowMs, uguiMs);
        }

        /// <summary> 10 sections x 10 text rows (3 levels); one label changes. </summary>
        private void OneLabelInNestedSections()
        {
            ReflowVertical reflow = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 10f), "Reflow");
            reflow.FollowHeight = true;
            TMP_Text reflowLabel = null;
            for (int s = 0; s < SECTION_COUNT; s++)
            {
                ReflowVertical section = ReflowTestUtil.CreateLayout<ReflowVertical>(reflow.transform, new Vector2(400f, 10f), "Section");
                section.FollowHeight = true;
                section.Padding = new RectOffset(10, 10, 10, 10);
                for (int r = 0; r < ROWS_PER_SECTION; r++)
                {
                    TMP_Text label = CreateReflowTextRow(section.transform, r);
                    if (s == SECTION_COUNT / 2 && r == ROWS_PER_SECTION / 2)
                        reflowLabel = label;
                }
            }
            reflow.EnsureLayout();

            RectTransform ugui = CreateUguiList(_root, true);
            TMP_Text uguiLabel = null;
            for (int s = 0; s < SECTION_COUNT; s++)
            {
                RectTransform section = ReflowTestUtil.CreateRect("Section", ugui, new Vector2(400f, 10f));
                VerticalLayoutGroup group = section.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureUguiGroup(group, true);
                group.padding = new RectOffset(10, 10, 10, 10);
                for (int r = 0; r < ROWS_PER_SECTION; r++)
                {
                    TMP_Text label = CreateUguiTextRow(section, r);
                    if (s == SECTION_COUNT / 2 && r == ROWS_PER_SECTION / 2)
                        uguiLabel = label;
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(ugui);

            double reflowMs = Measure(pIteration => reflowLabel.text = Label(pIteration), () => reflow.EnsureLayout());
            double uguiMs = Measure(pIteration => uguiLabel.text = Label(pIteration), () => LayoutRebuilder.ForceRebuildLayoutImmediate(ugui));
            AddRow($"One label changed, {SECTION_COUNT} sections x {ROWS_PER_SECTION} rows (3 levels)", reflowMs, uguiMs);
        }

        /// <summary> Build a 1000-row list in a 600-high viewport: Reflow spawns what is visible, uGUI creates every row. </summary>
        private void BuildBigList()
        {
            GameObject template = ReflowTestUtil.CreateItemTemplate(new Vector2(300f, 50f));
            RectTransform uguiTemplate = ReflowTestUtil.CreateRect("UguiRow", _root, new Vector2(300f, 50f));
            uguiTemplate.gameObject.SetActive(false);

            ReflowVertical reflow = null;
            double reflowMs = Measure(pIteration =>
            {
                if (reflow != null)
                    UnityEngine.Object.DestroyImmediate(reflow.transform.parent.parent.gameObject);
                reflow = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 600f), true, out _);
            }, () =>
            {
                for (int i = 0; i < BIG_LIST_COUNT; i++)
                    reflow.AddElement(template, null);
                reflow.EnsureLayout();
            }, 10);

            RectTransform ugui = null;
            double uguiMs = Measure(pIteration =>
            {
                if (ugui != null)
                    UnityEngine.Object.DestroyImmediate(ugui.gameObject);
                ugui = CreateUguiList(_root, false);
            }, () =>
            {
                for (int i = 0; i < BIG_LIST_COUNT; i++)
                {
                    RectTransform row = UnityEngine.Object.Instantiate(uguiTemplate, ugui, false);
                    row.gameObject.SetActive(true);
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(ugui);
            }, 10);

            AddRow($"Build a {BIG_LIST_COUNT}-row list (Reflow spawns {reflow.SpawnedCount})", reflowMs, uguiMs);
        }

        /// <summary> Scroll the 1000-row list: Reflow recycles items, uGUI has nothing to do (every row exists). </summary>
        private void ScrollBigList()
        {
            GameObject template = ReflowTestUtil.CreateItemTemplate(new Vector2(300f, 50f));
            ReflowVertical reflow = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 600f), true, out _);
            for (int i = 0; i < BIG_LIST_COUNT; i++)
                reflow.AddElement(template, null);
            reflow.EnsureLayout();

            double reflowMs = Measure(pIteration => reflow.RectTransform.anchoredPosition = new Vector2(0f, (pIteration + 1) * 173f % 40000f), () => reflow.RefreshVisibility(), 200);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                reflow.RectTransform.anchoredPosition = new Vector2(0f, i * 211f % 40000f);
                reflow.RefreshVisibility();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            AddRow($"Scroll one step in the {BIG_LIST_COUNT}-row list (Reflow allocates {allocated} B over 200 steps)", reflowMs, -1);
        }

        #endregion

        #region Builders

        private static string Label(int pIteration)
        {
            return pIteration % 2 == 0 ? "Short label" : "A considerably longer label text";
        }

        private static void ResizeRows(RectTransform[] pRowArray, int pIteration)
        {
            for (int i = 0; i < pRowArray.Length; i++)
                pRowArray[i].sizeDelta = new Vector2(300f, 30f + (i + pIteration + 1) % 7 * 5f);
        }

        private static RectTransform[] CreatePlainRows(Transform pParent, int pCount)
        {
            RectTransform[] rowArray = new RectTransform[pCount];
            for (int i = 0; i < pCount; i++)
                rowArray[i] = ReflowTestUtil.CreateRect("Row", pParent, new Vector2(300f, 40f));
            return rowArray;
        }

        private static TMP_Text CreateReflowTextRow(Transform pParent, int pIndex)
        {
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(pParent, new Vector2(10f, 10f), "Row");
            row.FollowWidth = true;
            row.FollowHeight = true;
            row.Spacing = 8f;
            ReflowTestUtil.CreateRect("Icon", row.transform, new Vector2(40f, 40f));
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(row.transform, "Label " + pIndex, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            return fitter.GetComponent<TMP_Text>();
        }

        private static TMP_Text CreateUguiTextRow(Transform pParent, int pIndex)
        {
            RectTransform row = ReflowTestUtil.CreateRect("Row", pParent, new Vector2(10f, 10f));
            HorizontalLayoutGroup group = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            ConfigureUguiGroup(group, true);
            group.spacing = 8f;
            group.childAlignment = TextAnchor.MiddleLeft;
            RectTransform icon = ReflowTestUtil.CreateRect("Icon", row, new Vector2(40f, 40f));
            LayoutElement element = icon.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 40f;
            element.preferredHeight = 40f;
            return ReflowTestUtil.CreateText(row, "Label " + pIndex, new Vector2(10f, 10f));
        }

        /// <summary> A top-level uGUI list: vertical group + content size fitter. </summary>
        private static RectTransform CreateUguiList(Transform pParent, bool pControlChildSize)
        {
            RectTransform list = ReflowTestUtil.CreateRect("UGUI", pParent, new Vector2(400f, 10f));
            VerticalLayoutGroup group = list.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureUguiGroup(group, pControlChildSize);
            ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return list;
        }

        private static void ConfigureUguiGroup(HorizontalOrVerticalLayoutGroup pGroup, bool pControlChildSize)
        {
            pGroup.childControlWidth = pControlChildSize;
            pGroup.childControlHeight = pControlChildSize;
            pGroup.childForceExpandWidth = false;
            pGroup.childForceExpandHeight = false;
            pGroup.childAlignment = TextAnchor.UpperLeft;
        }

        #endregion
    }
}
