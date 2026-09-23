using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary>
    /// Add to an empty GameObject and press Play. Click a cell: a detail block opens under its row (click again to
    /// close). Opening another cell moves the block; the view stays put and scrolls the block into view.
    /// </summary>
    public sealed class ReflowExpandableGridSample : MonoBehaviour
    {
        private const int CELL_COUNT = 120;

        private ReflowExpandableGrid _grid;
        private RectTransform _detail;
        private TextMeshProUGUI _detailText;

        private void Start()
        {
            RectTransform canvas = ReflowSampleUI.CreateCanvas();
            RectTransform area = ReflowSampleUI.Rect("Area", canvas, Vector2.zero);
            ReflowSampleUI.Stretch(area, 40f);

            _grid = ReflowSampleUI.ScrollView<ReflowExpandableGrid>(area, true, out _);
            _grid.CellSize = new Vector2(180f, 180f);
            _grid.Spacing = new Vector2(16f, 16f);
            _grid.Padding = new RectOffset(16, 16, 16, 16);
            _grid.ChildAlignment = TextAnchor.UpperCenter;

            ReflowSelection selection = _grid.gameObject.AddComponent<ReflowSelection>();
            selection.AllowToggleOff = true;
            selection.SelectionChanged += OnSelectionChanged;

            _detail = CreateDetail();
            GameObject template = CreateCellTemplate();
            for (int i = 0; i < CELL_COUNT; i++)
                _grid.AddElement(template, i);
        }

        private void OnSelectionChanged(int pPrevious, int pCurrent)
        {
            if (pCurrent < 0)
            {
                _grid.ClearExpandedContent();
                return;
            }
            _detailText.text = $"Cell {pCurrent + 1}\nThe block spans the whole row and pushes the rows below it down. Its height comes from this text.";
            _grid.SetExpandedContent(pCurrent, _detail);
            _grid.ScrollExpandedIntoView(24f, 0.3f);
        }

        private RectTransform CreateDetail()
        {
            RectTransform detail = ReflowSampleUI.Rect("Detail", _grid.transform, new Vector2(100f, 100f));
            ReflowSampleUI.Background(detail, ReflowSampleUI.CARD_COLOR);
            ReflowVertical layout = detail.gameObject.AddComponent<ReflowVertical>();
            layout.FollowHeight = true;
            layout.Padding = new RectOffset(32, 32, 28, 28);
            layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            _detailText = ReflowSampleUI.Text(detail, "", 32f, 600f);
            detail.gameObject.SetActive(false);
            return detail;
        }

        private static GameObject CreateCellTemplate()
        {
            RectTransform root = ReflowSampleUI.Template("Cell", new Vector2(180f, 180f));
            Image background = ReflowSampleUI.Background(root, ReflowSampleUI.CARD_COLOR);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            TextMeshProUGUI label = ReflowSampleUI.Text(root, "", 48f);
            label.alignment = TextAlignmentOptions.Center;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;

            ReflowSampleCellItem item = root.gameObject.AddComponent<ReflowSampleCellItem>();
            item.background = background;
            item.label = label;
            item.button = button;
            return root.gameObject;
        }
    }
}
