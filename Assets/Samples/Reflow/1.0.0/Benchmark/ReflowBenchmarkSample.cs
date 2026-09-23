using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary>
    /// Add to an empty GameObject and press Play (or run in a device build to get IL2CPP numbers). Builds the same
    /// UI with Reflow layouts and with uGUI LayoutGroup + ContentSizeFitter and times the layout work of each.
    /// The package's PlayMode test ReflowBenchmarkTests runs the full set.
    /// </summary>
    public sealed class ReflowBenchmarkSample : MonoBehaviour
    {
        private const int ITERATIONS = 40;
        private const int ROW_COUNT = 200;
        private const int TEXT_ROW_COUNT = 50;
        private const int BIG_LIST_COUNT = 1000;

        private RectTransform _stage;
        private TextMeshProUGUI _output;
        private readonly StringBuilder _report = new StringBuilder();

        private IEnumerator Start()
        {
            RectTransform canvas = ReflowSampleUI.CreateCanvas();
            _output = ReflowSampleUI.Text(canvas, "Running...", 30f, 1000f);
            _output.rectTransform.anchoredPosition = new Vector2(0f, 600f);
            _stage = ReflowSampleUI.Rect("Stage", canvas, new Vector2(1000f, 1000f));
            _stage.anchoredPosition = new Vector2(5000f, 0f);
            yield return null;

            _report.AppendLine($"{(Application.isEditor ? "Editor" : "Player")}  {SystemInfo.processorType}");
            _report.AppendLine("scenario: Reflow ms / uGUI ms");
            RelayoutRows();
            yield return null;
            OneLabel();
            yield return null;
            BuildBigList();
            _output.text = _report.ToString();
            UnityEngine.Debug.Log(_report.ToString());
        }

        private void Add(string pName, double pReflow, double pUgui)
        {
            _report.AppendLine($"{pName}: {pReflow:F3} / {pUgui:F3}  ({pUgui / pReflow:F1}x)");
        }

        private static double Measure(Action<int> pSetup, Action pAction, int pIterations = ITERATIONS)
        {
            pSetup(-1);
            pAction();
            Stopwatch stopwatch = new Stopwatch();
            double total = 0;
            for (int i = 0; i < pIterations; i++)
            {
                pSetup(i);
                stopwatch.Restart();
                pAction();
                stopwatch.Stop();
                total += stopwatch.Elapsed.TotalMilliseconds;
            }
            return total / pIterations;
        }

        private void RelayoutRows()
        {
            ReflowVertical reflow = ReflowSampleUI.Rect("Reflow", _stage, new Vector2(300f, 10f)).gameObject.AddComponent<ReflowVertical>();
            reflow.FollowHeight = true;
            RectTransform ugui = CreateUguiList(false);
            RectTransform[] reflowRows = new RectTransform[ROW_COUNT];
            RectTransform[] uguiRows = new RectTransform[ROW_COUNT];
            for (int i = 0; i < ROW_COUNT; i++)
            {
                reflowRows[i] = ReflowSampleUI.Rect("Row", reflow.transform, new Vector2(300f, 40f));
                uguiRows[i] = ReflowSampleUI.Rect("Row", ugui, new Vector2(300f, 40f));
            }

            double reflowMs = Measure(pIteration => Resize(reflowRows, pIteration), () =>
            {
                reflow.RefreshAllLayout();
                reflow.EnsureLayout();
            });
            double uguiMs = Measure(pIteration => Resize(uguiRows, pIteration), () => LayoutRebuilder.ForceRebuildLayoutImmediate(ugui));
            Add($"relayout {ROW_COUNT} resized rows", reflowMs, uguiMs);
            Destroy(reflow.gameObject);
            Destroy(ugui.gameObject);
        }

        private void OneLabel()
        {
            ReflowVertical reflow = ReflowSampleUI.Rect("Reflow", _stage, new Vector2(400f, 10f)).gameObject.AddComponent<ReflowVertical>();
            reflow.FollowHeight = true;
            RectTransform ugui = CreateUguiList(true);
            TMP_Text reflowLabel = null;
            TMP_Text uguiLabel = null;
            for (int i = 0; i < TEXT_ROW_COUNT; i++)
            {
                RectTransform reflowRow = ReflowSampleUI.Rect("Row", reflow.transform, new Vector2(10f, 10f));
                ReflowHorizontal rowLayout = reflowRow.gameObject.AddComponent<ReflowHorizontal>();
                rowLayout.FollowWidth = true;
                rowLayout.FollowHeight = true;
                rowLayout.Spacing = 8f;
                ReflowSampleUI.Rect("Icon", reflowRow, new Vector2(40f, 40f));
                TMP_Text a = ReflowSampleUI.Text(reflowRow, "Label " + i, 24f);

                RectTransform uguiRow = ReflowSampleUI.Rect("Row", ugui, new Vector2(10f, 10f));
                HorizontalLayoutGroup group = uguiRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                Configure(group, true);
                group.spacing = 8f;
                LayoutElement icon = ReflowSampleUI.Rect("Icon", uguiRow, new Vector2(40f, 40f)).gameObject.AddComponent<LayoutElement>();
                icon.preferredWidth = 40f;
                icon.preferredHeight = 40f;
                TextMeshProUGUI b = ReflowSampleUI.Rect("Text", uguiRow, new Vector2(10f, 10f)).gameObject.AddComponent<TextMeshProUGUI>();
                b.fontSize = 24f;
                b.text = "Label " + i;

                if (i == TEXT_ROW_COUNT / 2)
                {
                    reflowLabel = a;
                    uguiLabel = b;
                }
            }
            reflow.EnsureLayout();
            LayoutRebuilder.ForceRebuildLayoutImmediate(ugui);

            double reflowMs = Measure(pIteration => reflowLabel.text = Label(pIteration), () => reflow.EnsureLayout());
            double uguiMs = Measure(pIteration => uguiLabel.text = Label(pIteration), () => LayoutRebuilder.ForceRebuildLayoutImmediate(ugui));
            Add($"one label in a {TEXT_ROW_COUNT}-row text list", reflowMs, uguiMs);
            Destroy(reflow.gameObject);
            Destroy(ugui.gameObject);
        }

        private void BuildBigList()
        {
            RectTransform template = ReflowSampleUI.Template("Row", new Vector2(300f, 50f));
            RectTransform viewport = ReflowSampleUI.Rect("Viewport", _stage, new Vector2(300f, 600f));
            ReflowVertical reflow = null;
            double reflowMs = Measure(pIteration =>
            {
                if (reflow != null)
                    DestroyImmediate(reflow.gameObject);
                RectTransform content = ReflowSampleUI.Rect("Content", viewport, new Vector2(300f, 10f));
                reflow = content.gameObject.AddComponent<ReflowVertical>();
                reflow.FollowHeight = true;
                reflow.Viewport = viewport;
            }, () =>
            {
                for (int i = 0; i < BIG_LIST_COUNT; i++)
                    reflow.AddElement(template.gameObject);
                reflow.EnsureLayout();
            }, 10);

            RectTransform ugui = null;
            double uguiMs = Measure(pIteration =>
            {
                if (ugui != null)
                    DestroyImmediate(ugui.gameObject);
                ugui = CreateUguiList(false);
            }, () =>
            {
                for (int i = 0; i < BIG_LIST_COUNT; i++)
                    Instantiate(template, ugui, false).gameObject.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(ugui);
            }, 10);
            Add($"build a {BIG_LIST_COUNT}-row list", reflowMs, uguiMs);
        }

        private static string Label(int pIteration)
        {
            return pIteration % 2 == 0 ? "Short label" : "A considerably longer label text";
        }

        private static void Resize(RectTransform[] pRowArray, int pIteration)
        {
            for (int i = 0; i < pRowArray.Length; i++)
                pRowArray[i].sizeDelta = new Vector2(300f, 30f + (i + pIteration + 1) % 7 * 5f);
        }

        private RectTransform CreateUguiList(bool pControlChildSize)
        {
            RectTransform list = ReflowSampleUI.Rect("UGUI", _stage, new Vector2(400f, 10f));
            Configure(list.gameObject.AddComponent<VerticalLayoutGroup>(), pControlChildSize);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return list;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup pGroup, bool pControlChildSize)
        {
            pGroup.childControlWidth = pControlChildSize;
            pGroup.childControlHeight = pControlChildSize;
            pGroup.childForceExpandWidth = false;
            pGroup.childForceExpandHeight = false;
        }
    }
}
